using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Pinball.Core;
using Pinball.Data;
using Pinball.Scoring;
using Pinball.Combat;
using Pinball.Board;
using Pinball.Input;
using Pinball.UI;

namespace Pinball.Flow
{
    public class BattleManager : Singleton<BattleManager>, ISetupRunner
    {
        public int ballsLeft;
        public int round;
        public int currentLevelIndex;
        public bool battleStarted;
        public bool battleOver;
        private bool levelClearPending;

        [Header("飄字 / 動畫優先")]
        public float pegTextDuration = 0.7f;
        public float pocketTextHoldDuration = 0.8f;
        public float pocketTextFlyDuration = 0.9f;
        public float chargeCountDurationPerPipe = 0.8f;
        public float consumeTextInterval = 0.4f;
        public float monsterDamageTextDuration = 0.8f;
        public Key cancelAnimKey = Key.F;

        public Color pegTextColor = new Color(1f, 0.95f, 0.6f, 1f);
        public Color pocketTextColor = new Color(1f, 0.9f, 0.5f, 1f);
        public Color consumeTextColor = new Color(1f, 0.5f, 0.4f, 1f);
        public Color monsterDamageTextColor = new Color(1f, 0.4f, 0.4f, 1f);
        public float floatingTextFontSize = 36f;

        private class PendingPocketText
        {
            public float timer;
            public BigNumber amount;
        }

        private readonly List<PendingPocketText> pendingPocketTexts = new List<PendingPocketText>();
        private readonly Queue<BigNumber> energyAddQueue = new Queue<BigNumber>();
        private readonly List<float> attackTimers = new List<float>();

        private bool rollActive;
        private BigNumber rollCurrent;
        private BigNumber rollFrom;
        private BigNumber rollTarget;
        private float rollTimer;
        private float rollDuration;
        private bool settleBusy;

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this) return;

            SetupRegistry.RegisterRunner(this);
        }

        private void Start()
        {
            FloatingTextManager.EnsureExists();
            SubscribeEvents();

            if (!SetupManager.IsInitialized)
            {
                StartBattle();
            }
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current[cancelAnimKey].wasPressedThisFrame)
            {
                SkipAllAnimations();
            }
            else
            {
                TickPendingPocketTexts();
                TickEnergyRoll();
                TickAttackTimers();
            }

            if (HasPendingSettleWork())
            {
                settleBusy = true;
            }
            else if (settleBusy)
            {
                settleBusy = false;
                RefreshSettleUI();
                TryAdvanceFlow();
            }
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        public void RunAfterSetup()
        {
            StartBattle();
        }

        public void StartBattle()
        {
            if (!DataManager.IsInitialized)
            {
                Debug.LogError("BattleManager: DataManager is missing.");
                return;
            }

            GameConfigSO config = DataManager.Instance.gameConfig;
            if (config == null)
            {
                Debug.LogError("BattleManager: GameConfigSO is missing.");
                return;
            }

            if (BoardManager.IsInitialized)
            {
                BoardManager.Instance.ClearBalls();
            }

            FloatingTextManager.EnsureExists();

            ClearSettleState();
            if (FloatingTextManager.IsInitialized)
            {
                FloatingTextManager.Instance.CancelAll();
            }

            ScoringManager.Instance.Init(config.chargeCapacity);
            ScoringManager.Instance.ResetAll();

            CombatManager.Instance.CreatePlayer(config.playerMaxHp);

            RefreshBallScoreUI(BigNumber.Zero);
            RefreshChargeUI();
            RefreshPlayerHpUI();

            battleStarted = true;
            battleOver = false;
            levelClearPending = false;
            currentLevelIndex = 0;

            StartLevel(currentLevelIndex);
        }

        private void StartLevel(int levelIndex)
        {
            GameConfigSO config = DataManager.Instance.gameConfig;
            LevelConfigSO levelConfig = DataManager.Instance.GetLevelConfig();

            if (levelConfig == null || levelConfig.LevelCount <= 0)
            {
                Debug.LogError("BattleManager: LevelConfigSO is missing or empty.");
                EndBattle(false);
                return;
            }

            if (levelIndex < 0 || levelIndex >= levelConfig.LevelCount)
            {
                EndBattle(true);
                return;
            }

            if (DataManager.Instance.IsBossLevel(levelIndex))
            {
                ScoringManager.Instance.ClearCharge();
                RefreshChargeUI();
            }

            CombatManager.Instance.CreateMonsters(levelConfig, levelIndex);

            ballsLeft = config.ballsPerRound;
            round = 1;
            levelClearPending = false;
            ClearSettleState();

            RefreshMonsterUI();
        }

        private void StartNextLevel()
        {
            currentLevelIndex++;

            int levelCount = DataManager.Instance.GetLevelCount();
            if (currentLevelIndex >= levelCount)
            {
                EndBattle(true);
                return;
            }

            StartLevel(currentLevelIndex);
        }

        private void HandleLaunch(float power)
        {
            if (!battleStarted || battleOver) return;
            if (levelClearPending) return;
            if (ballsLeft <= 0) return;
            if (!BoardManager.IsInitialized) return;

            BallController ball = BoardManager.Instance.ShootBall(power);
            if (ball == null) return;

            ballsLeft--;
            RefreshBallScoreUI(ball.score);
        }

        private void HandleBallScoreChanged(int ballId, BigNumber ballScore, BigNumber delta, Vector2 ballWorldPos)
        {
            if (!battleStarted || battleOver) return;

            RefreshBallScoreUI(ballScore);

            if (FloatingTextManager.IsInitialized)
            {
                Vector3 worldPos = new Vector3(ballWorldPos.x, ballWorldPos.y + 0.5f, 0f);
                FloatingTextManager.Instance.SpawnFadeWorld(worldPos, "+" + delta.ToDisplayString(), pegTextColor, pegTextDuration, 30f, floatingTextFontSize);
            }
        }

        private void HandlePocketEntered(int ballId, BigNumber multiplier, Vector2 worldPos)
        {
            if (!battleStarted || battleOver) return;
            if (!BoardManager.IsInitialized) return;

            SpawnPocketText(ballId, multiplier, worldPos);
        }

        private void HandleBallTimedOut(int ballId, Vector2 worldPos)
        {
            if (!battleStarted || battleOver) return;
            if (!BoardManager.IsInitialized) return;

            SpawnPocketText(ballId, BigNumber.One, worldPos);
        }

        private void SpawnPocketText(int ballId, BigNumber multiplier, Vector2 worldPos)
        {
            BigNumber ballScore = BoardManager.Instance.GetBallScore(ballId);
            BoardManager.Instance.RemoveBall(ballId);

            BigNumber finalScore = ballScore * multiplier;

            if (FloatingTextManager.IsInitialized)
            {
                Vector2 targetScreen = GetChargeScreenPosition();
                Vector3 pos = new Vector3(worldPos.x, worldPos.y, 0f);
                FloatingTextManager.Instance.SpawnHoldThenFlyWorld(pos, targetScreen, "+" + finalScore.ToDisplayString(), pocketTextColor, pocketTextHoldDuration, pocketTextFlyDuration, floatingTextFontSize);
            }

            pendingPocketTexts.Add(new PendingPocketText
            {
                timer = pocketTextHoldDuration + pocketTextFlyDuration,
                amount = finalScore
            });

            RefreshScoreUI();
        }

        private void TickPendingPocketTexts()
        {
            for (int i = pendingPocketTexts.Count - 1; i >= 0; i--)
            {
                PendingPocketText pending = pendingPocketTexts[i];
                pending.timer -= Time.deltaTime;
                if (pending.timer <= 0f)
                {
                    energyAddQueue.Enqueue(pending.amount);
                    pendingPocketTexts.RemoveAt(i);
                }
            }
        }

        private void TickEnergyRoll()
        {
            if (!rollActive)
            {
                if (energyAddQueue.Count == 0)
                {
                    return;
                }

                BigNumber amount = energyAddQueue.Dequeue();
                ScoringManager.Instance.AddCharge(amount);

                rollFrom = ScoringManager.Instance.CurrentCharge - amount;
                rollCurrent = rollFrom;
                rollTarget = rollFrom + amount;
                rollDuration = ChargeRollDuration(amount);
                rollTimer = rollDuration;
                rollActive = true;
                return;
            }

            if (rollDuration <= 0f)
            {
                FinishRoll();
                return;
            }

            rollTimer -= Time.deltaTime;
            float t = Mathf.Clamp01(1f - rollTimer / rollDuration);
            rollCurrent = rollFrom + (rollTarget - rollFrom) * t;
            ShowCharge(rollCurrent);

            BigNumber capacity = ScoringManager.Instance.ChargeCapacity;
            int consumed = 0;
            while (capacity > BigNumber.Zero && rollCurrent >= capacity && consumed < 8)
            {
                ConsumeOneCharge();
                rollTarget -= capacity;
                rollFrom = rollCurrent;
                consumed++;
            }

            if (consumed > 0)
            {
                rollDuration = ChargeRollDuration(rollTarget - rollFrom);
                rollTimer = rollDuration;

                if (rollDuration <= 0f || rollTarget <= rollFrom)
                {
                    FinishRoll();
                }
                return;
            }

            if (rollTimer <= 0f)
            {
                FinishRoll();
            }
        }

        private void FinishRoll()
        {
            if (rollTarget > rollCurrent)
            {
                rollCurrent = rollTarget;
            }

            ShowCharge(rollCurrent);
            rollActive = false;
            rollTarget = BigNumber.Zero;
        }

        private void ConsumeOneCharge()
        {
            BigNumber capacity = ScoringManager.Instance.ChargeCapacity;
            ScoringManager.Instance.TryConsumeOneCharge();

            rollCurrent -= capacity;
            if (rollCurrent < BigNumber.Zero)
            {
                rollCurrent = BigNumber.Zero;
            }

            if (FloatingTextManager.IsInitialized)
            {
                Vector2 screen = GetChargeScreenPosition();
                FloatingTextManager.Instance.SpawnFadeScreen(screen + Vector2.up * 20f, "-" + capacity.ToDisplayString(), consumeTextColor, consumeTextInterval, 20f, floatingTextFontSize);
            }

            attackTimers.Add(consumeTextInterval);
        }

        private float ChargeRollDuration(BigNumber amount)
        {
            BigNumber capacity = ScoringManager.Instance.ChargeCapacity;
            if (capacity <= BigNumber.Zero)
            {
                return 0f;
            }

            double ratio = (amount / capacity).ToDouble();
            if (double.IsNaN(ratio) || double.IsInfinity(ratio))
            {
                ratio = 1.0;
            }

            return Mathf.Clamp((float)ratio * chargeCountDurationPerPipe, 0.01f, 10f);
        }

        private void TickAttackTimers()
        {
            for (int i = 0; i < attackTimers.Count; i++)
            {
                attackTimers[i] -= Time.deltaTime;
            }

            while (attackTimers.Count > 0 && attackTimers[0] <= 0f)
            {
                attackTimers.RemoveAt(0);
                ApplyOneAttack();

                if (battleOver)
                {
                    attackTimers.Clear();
                    return;
                }
            }
        }

        private void ApplyOneAttack()
        {
            if (!battleStarted || battleOver) return;

            GameConfigSO config = DataManager.Instance.gameConfig;
            if (config == null) return;

            CombatShotResult shot = CombatManager.Instance.ApplySingleShot(config.damagePerShot);

            if (shot.target != null)
            {
                if (FloatingTextManager.IsInitialized)
                {
                    Vector2 screen = GetMonsterHpScreenPosition() + Vector2.up * 40f;
                    FloatingTextManager.Instance.SpawnFadeScreen(screen, "-" + shot.actualDamage.ToDisplayString(), monsterDamageTextColor, monsterDamageTextDuration, 30f, floatingTextFontSize);
                }

                RefreshMonsterUI();
            }

            RefreshPlayerHpUI();

            if (CombatManager.Instance.IsPlayerDead())
            {
                EndBattle(false);
                return;
            }

            if (CombatManager.Instance.IsLevelCleared())
            {
                levelClearPending = true;
            }
        }

        private void SkipAllAnimations()
        {
            if (!battleStarted || battleOver)
            {
                return;
            }

            if (FloatingTextManager.IsInitialized)
            {
                FloatingTextManager.Instance.CancelAll();
            }

            for (int i = 0; i < pendingPocketTexts.Count; i++)
            {
                energyAddQueue.Enqueue(pendingPocketTexts[i].amount);
            }
            pendingPocketTexts.Clear();

            while (energyAddQueue.Count > 0)
            {
                ScoringManager.Instance.AddCharge(energyAddQueue.Dequeue());
            }

            const int maxFires = 1000;
            int extraFires = 0;
            while (extraFires < maxFires && ScoringManager.Instance.TryConsumeOneCharge())
            {
                extraFires++;
            }

            int pendingFires = attackTimers.Count;
            attackTimers.Clear();

            int totalFires = extraFires + pendingFires;
            if (totalFires > maxFires)
            {
                totalFires = maxFires;
            }
            if (totalFires > 0 && !CombatManager.Instance.IsPlayerDead())
            {
                GameConfigSO config = DataManager.Instance.gameConfig;
                if (config != null)
                {
                    CombatManager.Instance.ApplyFire(totalFires, config.damagePerShot);
                }
            }

            rollActive = false;
            rollFrom = BigNumber.Zero;
            rollTarget = BigNumber.Zero;
            rollCurrent = ScoringManager.Instance.CurrentCharge;
            ShowCharge(rollCurrent);

            if (CombatManager.Instance.IsLevelCleared())
            {
                levelClearPending = true;
            }

            RefreshChargeUI();
            RefreshMonsterUI();
            RefreshPlayerHpUI();
        }

        private bool HasPendingSettleWork()
        {
            return pendingPocketTexts.Count > 0 || energyAddQueue.Count > 0 || rollActive || attackTimers.Count > 0;
        }

        private void TryAdvanceFlow()
        {
            if (!battleStarted || battleOver) return;

            if (CombatManager.Instance.IsPlayerDead())
            {
                EndBattle(false);
                return;
            }

            if (levelClearPending)
            {
                if (BoardManager.Instance.ActiveBallCount <= 0)
                {
                    StartNextLevel();
                }
                return;
            }

            if (ballsLeft <= 0 && BoardManager.Instance.ActiveBallCount <= 0)
            {
                GameConfigSO config = DataManager.Instance.gameConfig;
                if (config == null)
                {
                    return;
                }

                bool playerDied = CombatManager.Instance.MonsterAttack();
                round++;
                ballsLeft = config.ballsPerRound;
                RefreshPlayerHpUI();

                if (playerDied)
                {
                    EndBattle(false);
                    return;
                }

                if (round > config.maxRounds)
                {
                    EndBattle(false);
                }
            }
        }

        private void EndBattle(bool win)
        {
            battleOver = true;
            battleStarted = false;
            levelClearPending = false;
            ClearSettleState();

            if (BoardManager.IsInitialized)
            {
                BoardManager.Instance.ClearBalls();
            }

            if (FloatingTextManager.IsInitialized)
            {
                FloatingTextManager.Instance.CancelAll();
            }

            if (BattleUIManager.IsInitialized)
            {
                BattleUIManager.Instance.ShowResult(win ? "Win" : "Lose");
            }
        }

        private void ClearSettleState()
        {
            pendingPocketTexts.Clear();
            energyAddQueue.Clear();
            attackTimers.Clear();
            rollActive = false;
            rollCurrent = BigNumber.Zero;
            rollFrom = BigNumber.Zero;
            rollTarget = BigNumber.Zero;
            settleBusy = false;
        }

        private Vector2 GetChargeScreenPosition()
        {
            if (!BattleUIManager.IsInitialized)
            {
                return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }

            return BattleUIManager.Instance.GetChargeScreenPosition();
        }

        private Vector2 GetMonsterHpScreenPosition()
        {
            if (!BattleUIManager.IsInitialized)
            {
                return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }

            return BattleUIManager.Instance.GetMonsterHpScreenPosition();
        }

        private void ShowCharge(BigNumber charge)
        {
            if (BattleUIManager.IsInitialized)
            {
                BattleUIManager.Instance.ShowCharge(charge, ScoringManager.Instance.ChargeCapacity);
            }
        }

        private void RefreshBallScoreUI(BigNumber score)
        {
            if (!BattleUIManager.IsInitialized) return;

            BattleUIManager.Instance.ShowBallScore(score);
        }

        private void RefreshScoreUI()
        {
            BigNumber score = BigNumber.Zero;
            if (BoardManager.IsInitialized)
            {
                score = BoardManager.Instance.GetRepresentativeBallScore();
            }

            RefreshBallScoreUI(score);
        }

        private void RefreshChargeUI()
        {
            if (!BattleUIManager.IsInitialized) return;

            BattleUIManager.Instance.ShowCharge(ScoringManager.Instance.CurrentCharge, ScoringManager.Instance.ChargeCapacity);
        }

        private void RefreshMonsterUI()
        {
            if (!BattleUIManager.IsInitialized) return;

            BattleUIManager.Instance.ShowMonsterName(CombatManager.Instance.GetMonsterName());
            BattleUIManager.Instance.ShowMonsterHp(CombatManager.Instance.GetMonsterHp());
        }

        private void RefreshPlayerHpUI()
        {
            if (!BattleUIManager.IsInitialized) return;

            BattleUIManager.Instance.ShowPlayerHp(CombatManager.Instance.GetPlayerHp());
        }

        private void RefreshSettleUI()
        {
            RefreshScoreUI();
            RefreshChargeUI();
            RefreshMonsterUI();
            RefreshPlayerHpUI();
        }

        private void SubscribeEvents()
        {
            if (BoardManager.IsInitialized)
            {
                BoardManager.Instance.OnBallScoreChanged += HandleBallScoreChanged;
                BoardManager.Instance.OnPocketEntered += HandlePocketEntered;
                BoardManager.Instance.OnBallTimedOut += HandleBallTimedOut;
            }

            if (InputManager.IsInitialized)
            {
                InputManager.Instance.OnLaunch += HandleLaunch;
            }
        }

        private void UnsubscribeEvents()
        {
            if (BoardManager.IsInitialized)
            {
                BoardManager.Instance.OnBallScoreChanged -= HandleBallScoreChanged;
                BoardManager.Instance.OnPocketEntered -= HandlePocketEntered;
                BoardManager.Instance.OnBallTimedOut -= HandleBallTimedOut;
            }

            if (InputManager.IsInitialized)
            {
                InputManager.Instance.OnLaunch -= HandleLaunch;
            }
        }
    }
}
