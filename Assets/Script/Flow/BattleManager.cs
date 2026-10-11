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
using Pinball.Fx;

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

        [Header("落袋四拍儀式（2.7.4）")]
        [Tooltip("第 1 拍：球上分數停在袋口。")]
        public float pocketBallScoreHold = 0.4f;
        [Tooltip("第 2 拍：袋口倍率彈出。")]
        public float pocketMultiplierHold = 0.3f;
        [Tooltip("第 3 拍：兩者相撞、結果炸開。")]
        public float pocketCollideHold = 0.3f;
        [Tooltip("第 4 拍：結果飛向充能條。")]
        public float pocketTextFlyDuration = 0.9f;
        public Color pocketBallScoreColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        public Color pocketMultiplierColor = new Color(1f, 0.9f, 0.25f, 1f);

        [Header("充能條滾動（2.7.1 / 2.7.3）")]
        [Tooltip("每條充能的基準時長 k（秒）。")]
        public float chargeCountDurationPerPipe = 0.8f;
        [Tooltip("時長成長次方。0.5 = √n → 分數越大滾得越快（爽感根源）；1 = 舊版常數速度。")]
        public float chargeRollExponent = 0.5f;
        [Tooltip("單次滾動時長上限（秒），避免一顆超大的球播太久。")]
        public float maxChargeRollDuration = 8f;

        [Header("蓄力 / 釋放（2.8 新能量系統）")]
        [Tooltip("最後一次下袋之後，多久沒有新球下袋就釋放攻擊（秒）。滾動播放期間不倒數。")]
        public float chargeIdleSeconds = 1.2f;
        [Tooltip("手動提早釋放的冷卻（秒）。")]
        public float manualReleaseCooldown = 0.5f;
        [Tooltip("手動提早釋放的按鍵。")]
        public Key manualReleaseKey = Key.Space;
        [Tooltip("完成的層數 → 傷害倍率。第 1 層（紅條填滿）為 ×1；超過表長度就以最後一個為上限。")]
        public float[] tierMultipliers = new float[] { 1f, 1.5f, 2.5f, 4f, 8f };
        public float releaseTextDuration = 1.2f;
        public float releaseFontSizeBase = 1.8f;
        public float releaseFontSizePerTier = 0.4f;
        public float releasePunchBase = 0.5f;
        public float releasePunchPerTier = 0.18f;
        public float monsterDamageTextDuration = 0.8f;
        public Key cancelAnimKey = Key.F;

        [Header("飄字分級（2.7.5 / 2.7.9）")]
        public Color pegTextColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        public Color monsterDamageTextColor = new Color(1f, 0.35f, 0.35f, 1f);
        public float floatingTextFontSize = 36f;
        public float pegTextPunch = 0.15f;

        [Header("釋放鏡頭抖動")]
        public float shakeAmplitudeLight = 0.06f;
        public float shakeAmplitudeMedium = 0.16f;
        public float shakeAmplitudeHeavy = 0.32f;
        public float shakeDurationLight = 0.18f;
        public float shakeDurationMedium = 0.32f;
        public float shakeDurationHeavy = 0.5f;
        [Tooltip("鏡頭抖動元件。留空會自動找 Camera.main 並加上。")]
        public CameraShake cameraShake;

        private enum PocketPhase
        {
            BallScore,
            Multiplier,
            Collide
        }

        private class PendingPocketText
        {
            public PocketPhase phase;
            public float timer;
            public BigNumber amount;
            public string ballScoreText;
            public string multiplierText;
            public string resultText;
            public Vector3 worldPos;
            public Color resultColor;
            public float resultFontSize;
            public float resultPunch;
        }

        private readonly List<PendingPocketText> pendingPocketTexts = new List<PendingPocketText>();
        private readonly Queue<BigNumber> energyAddQueue = new Queue<BigNumber>();

        private bool rollActive;
        private BigNumber rollCurrent;
        private BigNumber rollBase;
        private BigNumber rollAmount;
        private float rollTimer;
        private float rollDuration;
        private bool settleBusy;
        private bool chargeEnabled = true;
        private bool charging;
        private float chargeIdleTimer;
        private float releaseCooldown;

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
            bool skipped = false;

            if (Keyboard.current != null && Keyboard.current[cancelAnimKey].wasPressedThisFrame)
            {
                SkipAllAnimations();
                skipped = true;
            }

            if (!skipped)
            {
                TickPendingPocketTexts();
                TickEnergyRoll();
                TickCharge();
            }

            if (Keyboard.current != null && Keyboard.current[manualReleaseKey].wasPressedThisFrame)
            {
                TryManualRelease();
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
            EnsureShake();

            ClearSettleState();
            chargeEnabled = true;
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
            RefreshBallsLeftUI();

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
            chargeEnabled = true;
            RefreshBallsLeftUI();

            RefreshMonsterUI();
            RefreshChargeUI();
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
            RefreshBallsLeftUI();
        }

        private void HandleBallScoreChanged(int ballId, BigNumber ballScore, BigNumber delta, Vector2 ballWorldPos)
        {
            if (!battleStarted || battleOver) return;

            RefreshBallScoreUI(ballScore);

            if (FloatingTextManager.IsInitialized)
            {
                Vector3 worldPos = new Vector3(ballWorldPos.x, ballWorldPos.y + 0.5f, 0f);
                FloatingTextManager.Instance.SpawnFadeWorld(worldPos, "+" + delta.ToDisplayString(), pegTextColor, pegTextDuration, 30f, floatingTextFontSize, pegTextPunch);
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
            double multiplierValue = multiplier.ToDouble();

            PendingPocketText pending = new PendingPocketText
            {
                phase = PocketPhase.BallScore,
                timer = pocketBallScoreHold,
                amount = finalScore,
                ballScoreText = ballScore.ToDisplayString(),
                multiplierText = "×" + multiplier.ToDisplayString(),
                resultText = finalScore.ToDisplayString(),
                worldPos = new Vector3(worldPos.x, worldPos.y, 0f),
                resultColor = GetMultiplierTierColor(multiplierValue),
                resultFontSize = floatingTextFontSize * GetMultiplierTierSizeScale(multiplierValue),
                resultPunch = GetMultiplierTierPunch(multiplierValue)
            };

            if (FloatingTextManager.IsInitialized)
            {
                FloatingTextManager.Instance.SpawnTimedWorld(pending.worldPos, pending.ballScoreText, pocketBallScoreColor, pocketBallScoreHold, floatingTextFontSize, 0.1f);
            }

            pendingPocketTexts.Add(pending);
            RefreshScoreUI();
        }

        private static Color GetMultiplierTierColor(double multiplier)
        {
            if (multiplier < 1.5) return Color.white;
            if (multiplier < 3.0) return new Color(1f, 0.9f, 0.25f, 1f);
            if (multiplier < 8.0) return new Color(1f, 0.55f, 0.1f, 1f);
            if (multiplier < 15.0) return new Color(1f, 0.85f, 0.1f, 1f);
            return new Color(1f, 0.35f, 0.9f, 1f);
        }

        private static float GetMultiplierTierSizeScale(double multiplier)
        {
            if (multiplier < 1.5) return 1f;
            if (multiplier < 3.0) return 1.2f;
            if (multiplier < 8.0) return 1.5f;
            if (multiplier < 15.0) return 1.9f;
            return 2.4f;
        }

        private static float GetMultiplierTierPunch(double multiplier)
        {
            if (multiplier < 1.5) return 0.1f;
            if (multiplier < 3.0) return 0.25f;
            if (multiplier < 8.0) return 0.4f;
            if (multiplier < 15.0) return 0.55f;
            return 0.7f;
        }

        private void TickPendingPocketTexts()
        {
            for (int i = pendingPocketTexts.Count - 1; i >= 0; i--)
            {
                PendingPocketText pending = pendingPocketTexts[i];
                pending.timer -= Time.deltaTime;

                if (pending.timer > 0f)
                {
                    continue;
                }

                switch (pending.phase)
                {
                    case PocketPhase.BallScore:
                        if (FloatingTextManager.IsInitialized)
                        {
                            Vector3 multiplierPos = new Vector3(pending.worldPos.x + 0.5f, pending.worldPos.y + 0.3f, 0f);
                            FloatingTextManager.Instance.SpawnTimedWorld(multiplierPos, pending.multiplierText, pocketMultiplierColor, pocketMultiplierHold, floatingTextFontSize * 1.15f, 0.25f);
                        }

                        pending.phase = PocketPhase.Multiplier;
                        pending.timer = pocketMultiplierHold;
                        break;

                    case PocketPhase.Multiplier:
                        if (FloatingTextManager.IsInitialized)
                        {
                            FloatingTextManager.Instance.SpawnHoldThenFlyWorld(
                                pending.worldPos,
                                GetChargeScreenPosition(),
                                pending.resultText,
                                pending.resultColor,
                                pocketCollideHold,
                                pocketTextFlyDuration,
                                pending.resultFontSize,
                                pending.resultPunch);
                        }

                        pending.phase = PocketPhase.Collide;
                        pending.timer = pocketCollideHold + pocketTextFlyDuration;
                        break;

                    default:
                        energyAddQueue.Enqueue(pending.amount);
                        pendingPocketTexts.RemoveAt(i);
                        break;
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

                rollBase = ScoringManager.Instance.CurrentCharge - amount;
                rollAmount = amount;
                rollCurrent = rollBase;
                rollDuration = ChargeRollDuration(amount);
                rollTimer = rollDuration;
                rollActive = true;
                chargeIdleTimer = chargeIdleSeconds;

                UpdateChargeVisual(rollCurrent);
                return;
            }

            if (rollDuration <= 0f)
            {
                FinishRoll();
                return;
            }

            rollTimer -= Time.deltaTime;
            float progress = Mathf.Clamp01(1f - rollTimer / rollDuration);
            ApplyRollValue(rollBase + rollAmount * progress);

            if (rollTimer <= 0f)
            {
                FinishRoll();
            }
        }

        private void ApplyRollValue(BigNumber raw)
        {
            rollCurrent = raw;
            UpdateChargeVisual(rollCurrent);
        }

        private void FinishRoll()
        {
            ApplyRollValue(rollBase + rollAmount);
            rollActive = false;
        }

        /// <summary>
        /// 蓄力的倒數：最後一次下袋之後，只要有一段時間沒有新球下袋就釋放。
        /// 滾動還在播、或還有下袋事件排隊時不倒數，避免把一段滾動的能量腰斬。
        /// </summary>
        private void TickCharge()
        {
            if (releaseCooldown > 0f)
            {
                releaseCooldown -= Time.deltaTime;
            }

            if (!charging || !chargeEnabled)
            {
                return;
            }

            if (rollActive || energyAddQueue.Count > 0)
            {
                return;
            }

            chargeIdleTimer -= Time.deltaTime;
            if (chargeIdleTimer <= 0f)
            {
                ReleaseCharge();
            }
        }

        private void TryManualRelease()
        {
            if (!battleStarted || battleOver) return;
            if (!charging || !chargeEnabled) return;
            if (releaseCooldown > 0f) return;

            if (rollActive)
            {
                FinishRoll();
            }

            ReleaseCharge();
        }

        /// <summary>
        /// 能量條的顯示值更新，並在「填滿的瞬間」點火進入蓄力。
        /// 點火看的是顯示值（滾動動畫推到哪裡），不是立即跳到的實際值。
        /// </summary>
        private void UpdateChargeVisual(BigNumber displayed)
        {
            BigNumber capacity = ScoringManager.Instance.ChargeCapacity;
            bool full = chargeEnabled && capacity > BigNumber.Zero && displayed >= capacity;

            if (full && !charging)
            {
                charging = true;
                chargeIdleTimer = chargeIdleSeconds;
            }

            PushChargeUI(displayed);
        }

        private void PushChargeUI(BigNumber displayed)
        {
            if (!BattleUIManager.IsInitialized)
            {
                return;
            }

            // 分層 Image 由 UI 端自動生成；倍率表加長時這裡會跟著長出新的層。
            int tierCount = (tierMultipliers != null && tierMultipliers.Length > 0) ? tierMultipliers.Length : 1;
            BattleUIManager.Instance.SetOverchargeLayerCount(tierCount);

            int tier = GetTier(displayed);
            float multiplier = GetTierMultiplier(tier);
            BattleUIManager.Instance.ShowEnergy(displayed, ScoringManager.Instance.ChargeCapacity, tier, multiplier, GetReleaseDamage(displayed, tier, multiplier));
        }

        /// <summary>完成的層數（1 起算）。0 代表還沒點火。</summary>
        private int GetTier(BigNumber energy)
        {
            BigNumber capacity = ScoringManager.Instance.ChargeCapacity;
            if (capacity <= BigNumber.Zero || energy < capacity)
            {
                return 0;
            }

            int maxTier = (tierMultipliers != null && tierMultipliers.Length > 0) ? tierMultipliers.Length : 1;
            int tier = (energy / capacity).ToIntClamped(maxTier);
            return tier < 1 ? 1 : tier;
        }

        private float GetTierMultiplier(int tier)
        {
            if (tierMultipliers == null || tierMultipliers.Length == 0)
            {
                return 1f;
            }

            return tierMultipliers[Mathf.Clamp(tier - 1, 0, tierMultipliers.Length - 1)];
        }

        private BigNumber GetReleaseDamage(BigNumber energy, int tier, float multiplier)
        {
            if (tier <= 0 || energy <= BigNumber.Zero)
            {
                return BigNumber.Zero;
            }

            BigNumber capacity = ScoringManager.Instance.ChargeCapacity;
            if (capacity <= BigNumber.Zero)
            {
                return BigNumber.Zero;
            }

            GameConfigSO config = DataManager.Instance.gameConfig;
            if (config == null)
            {
                return BigNumber.Zero;
            }

            // 滿一條（1 層）且 ×1 時，傷害剛好等於 damagePerShot。
            BigNumber ratio = config.damagePerShot / capacity;
            return energy * ratio * multiplier;
        }

        /// <summary>把整條累積的能量一次打出去，然後歸零重新累積。</summary>
        private void ReleaseCharge()
        {
            BigNumber energy = ScoringManager.Instance.CurrentCharge;
            int tier = GetTier(energy);

            if (tier <= 0)
            {
                charging = false;
                chargeIdleTimer = 0f;
                PushChargeUI(energy);
                return;
            }

            float multiplier = GetTierMultiplier(tier);
            BigNumber damage = GetReleaseDamage(energy, tier, multiplier);

            charging = false;
            chargeIdleTimer = 0f;
            releaseCooldown = manualReleaseCooldown;

            ScoringManager.Instance.ConsumeAllCharge();
            rollActive = false;
            rollBase = BigNumber.Zero;
            rollAmount = BigNumber.Zero;
            rollCurrent = BigNumber.Zero;
            PushChargeUI(BigNumber.Zero);

            if (!battleStarted || battleOver || !chargeEnabled) return;
            if (damage <= BigNumber.Zero) return;

            CombatShotResult shot = CombatManager.Instance.ApplyRelease(damage);

            if (shot.target != null && FloatingTextManager.IsInitialized)
            {
                Vector2 screen = GetMonsterHpScreenPosition() + Vector2.up * 40f;
                float size = floatingTextFontSize * (releaseFontSizeBase + releaseFontSizePerTier * (tier - 1));
                float punch = releasePunchBase + releasePunchPerTier * (tier - 1);
                FloatingTextManager.Instance.SpawnFadeScreen(screen, "-" + shot.actualDamage.ToDisplayString(), monsterDamageTextColor, releaseTextDuration, 30f, size, punch);
                RefreshMonsterUI();
            }

            ShakeCamera(tier);
            RefreshPlayerHpUI();

            if (CombatManager.Instance.IsPlayerDead())
            {
                EndBattle(false);
                return;
            }

            if (CombatManager.Instance.IsLevelCleared())
            {
                levelClearPending = true;
                chargeEnabled = false;
            }
        }

        private void ShakeCamera(int tier)
        {
            EnsureShake();
            if (cameraShake == null)
            {
                return;
            }

            float amplitude = shakeAmplitudeLight;
            float duration = shakeDurationLight;

            if (tier >= 3)
            {
                amplitude = shakeAmplitudeHeavy;
                duration = shakeDurationHeavy;
            }
            else if (tier == 2)
            {
                amplitude = shakeAmplitudeMedium;
                duration = shakeDurationMedium;
            }

            cameraShake.Shake(amplitude, duration);
        }

        private void EnsureShake()
        {
            if (cameraShake != null)
            {
                return;
            }

            cameraShake = FindObjectOfType<CameraShake>();
            if (cameraShake == null && Camera.main != null)
            {
                cameraShake = Camera.main.gameObject.AddComponent<CameraShake>();
            }
        }

        private float ChargeRollDuration(BigNumber amount)
        {
            BigNumber capacity = ScoringManager.Instance.ChargeCapacity;
            if (capacity <= BigNumber.Zero)
            {
                return 0f;
            }

            double pipes = (amount / capacity).ToDouble();
            if (double.IsNaN(pipes) || pipes <= 0.0)
            {
                return 0.01f;
            }

            if (double.IsInfinity(pipes))
            {
                return maxChargeRollDuration;
            }

            float exponent = Mathf.Max(0.1f, chargeRollExponent);
            float duration = Mathf.Pow((float)pipes, exponent) * chargeCountDurationPerPipe;
            return Mathf.Clamp(duration, 0.05f, maxChargeRollDuration);
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

            rollActive = false;
            rollBase = BigNumber.Zero;
            rollAmount = BigNumber.Zero;
            rollCurrent = ScoringManager.Instance.CurrentCharge;

            if (chargeEnabled)
            {
                ReleaseCharge();
            }
            else
            {
                UpdateChargeVisual(rollCurrent);
            }

            if (CombatManager.Instance.IsLevelCleared())
            {
                levelClearPending = true;
                chargeEnabled = false;
            }

            RefreshChargeUI();
            RefreshMonsterUI();
            RefreshPlayerHpUI();
        }

        private bool HasPendingSettleWork()
        {
            return pendingPocketTexts.Count > 0 || energyAddQueue.Count > 0 || rollActive || (charging && chargeEnabled);
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
                RefreshBallsLeftUI();
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
            rollActive = false;
            rollCurrent = BigNumber.Zero;
            rollBase = BigNumber.Zero;
            rollAmount = BigNumber.Zero;
            charging = false;
            chargeIdleTimer = 0f;
            releaseCooldown = 0f;
            settleBusy = false;
            chargeEnabled = false;
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
            UpdateChargeVisual(ScoringManager.Instance.CurrentCharge);
        }

        private void RefreshBallsLeftUI()
        {
            if (!BattleUIManager.IsInitialized) return;

            BattleUIManager.Instance.ShowBallsLeft(ballsLeft);
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
