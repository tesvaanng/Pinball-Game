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
        public float chargeCountDurationPerPipe = 0.45f;
        [Tooltip("時長成長次方。0.5 = √n → 分數越大滾得越快（爽感根源）；1 = 舊版常數速度。")]
        public float chargeRollExponent = 0.5f;
        [Tooltip("峰值膝點（條）。條數超過這裡之後時長開始縮短，數字越大滾得越快，避免大數字全部卡在上限等很久。")]
        public float chargeRollDecayKnee = 20f;
        [Tooltip("膝點之後的衰減次方。越大越快收斂到「大數字漸近時長」。0.4 是平滑收斂。")]
        public float chargeRollDecayExponent = 0.4f;
        [Tooltip("小額的硬地板（秒）。只擋太短的滾動，不影響大數字。")]
        public float minChargeRollDuration = 0.25f;
        [Tooltip("大數字的「漸近時長」（秒）。數字越大時長平滑趨近這個值，不會被夾成同一個秒數，也不會無限變短。")]
        public float chargeRollFloorDuration = 1.2f;
        [Tooltip("單次滾動時長上限（秒），避免一顆超大的球播太久。")]
        public float maxChargeRollDuration = 3f;

        [Header("蓄力 / 釋放（2.8 新能量系統）")]
        [Tooltip("最後一次下袋之後，多久沒有新球下袋就釋放攻擊（秒）。滾動播放期間不倒數。")]
        public float chargeIdleSeconds = 1.2f;
        [Tooltip("手動提早釋放的冷卻（秒）。")]
        public float manualReleaseCooldown = 0.5f;
        [Tooltip("手動提早釋放的按鍵。")]
        public Key manualReleaseKey = Key.Space;
        [Tooltip("完成的位數 → 傷害倍率。每 ×10 能量升一階；超過表長度就以最後一個為上限，之後由肉鴿遺物擴充。")]
        public float[] tierMultipliers = new float[] { 1f, 1.5f, 2.5f, 4f, 8f, 16f, 32f, 64f };

        [Header("能量條視覺刻度")]
        [Tooltip("單次滾動最少讓條子推進幾層。確保再小的能量進來也看得見。")]
        public float barRollMinLayers = 0.35f;
        [Tooltip("單次滾動最多讓條子推進幾層。超過就固定推進這麼多層，避免大數字時條子狂閃。")]
        public float barRollMaxLayers = 10f;

        [Header("釋放：基數 UI 飛到倍率 UI 合併（參照落袋）")]
        [Tooltip("基數在自己的 UI 上停頓多久才起飛（秒）。")]
        public float releaseBaseHold = 0.1f;
        [Tooltip("基數從基數 UI 飛到倍率 UI 的時長（秒）。")]
        public float releaseBaseFlyDuration = 0.25f;
        [Tooltip("結果在倍率 UI 上炸開並停留的時長（秒）。")]
        public float releaseCollideHold = 0.25f;
        [Tooltip("結果飛向怪物血條的時長；飛到時才扣血並抖動鏡頭。")]
        public float releaseDamageFlyDuration = 0.5f;
        public Color releaseBaseColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        public float releaseBaseFontScale = 0.9f;
        public float releaseFontSizeBase = 1.8f;
        public float releaseFontSizePerTier = 0.4f;
        public float releasePunchBase = 0.5f;
        public float releasePunchPerTier = 0.18f;
        public Key cancelAnimKey = Key.F;

        [Header("飄字分級（2.7.5 / 2.7.9）")]
        public Color pegTextColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        public Color monsterDamageTextColor = new Color(1f, 0.35f, 0.35f, 1f);
        [Tooltip("紅色扣血飄字的停留秒數。")]
        public float monsterDamageTextDuration = 0.8f;
        [Tooltip("紅色扣血飄字的彈跳幅度。")]
        public float monsterDamageTextPunch = 0.25f;
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

        private enum ReleasePhase
        {
            Base,
            Fly
        }

        /// <summary>釋放：基數 UI 飛到倍率 UI 合併 → 結果飛向怪物血條（飛到時才扣血並抖動）。</summary>
        private class PendingRelease
        {
            public ReleasePhase phase;
            public float timer;
            public string baseText;
            public string resultText;
            public Color resultColor;
            public float resultFontSize;
            public float resultPunch;
            public BigNumber damage;
            public int tier;
            public Vector2 mergePos;
        }

        private readonly List<PendingPocketText> pendingPocketTexts = new List<PendingPocketText>();
        private readonly List<PendingRelease> pendingReleases = new List<PendingRelease>();
        private readonly Queue<BigNumber> energyAddQueue = new Queue<BigNumber>();

        private bool rollActive;
        private BigNumber rollCurrent;
        private BigNumber rollFrom;
        private BigNumber rollTarget;
        private float rollTimer;
        private float rollDuration;
        private double rollLayers;
        private double rollLayersAtStart;
        private double rollLayerSpan;
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
                TickPendingReleases();
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

        /// <summary>
        /// 充能條的滾動。
        /// - 只要佇列裡還有新球下袋的量，就一起吸進來，從「現在顯示的值」重新起算這一段，
        ///   所以前後接著來的分數會連成一段連續的動畫，新分數進來時速度也會跟著重新算。
        /// - 顯示值用對數內插（位數等速成長），數字看起來才是穩定地爬，
        ///   不會出現「前面超快衝上去、後面慢慢磨」的怪異感。
        /// </summary>
        private void TickEnergyRoll()
        {
            if (energyAddQueue.Count > 0)
            {
                AbsorbQueuedEnergy();

                rollActive = true;
                rollFrom = rollCurrent;
                rollTarget = ScoringManager.Instance.CurrentCharge;
                rollDuration = ChargeRollDuration(rollTarget - rollFrom);
                rollTimer = rollDuration;
                chargeIdleTimer = chargeIdleSeconds;

                // 條子的刻度跟著「本次滾動的量」走，不是跟著總量：
                // 總量當刻度的話，量越大每一條越貴，新能量就推不動條子。
                rollLayersAtStart = rollLayers;
                rollLayerSpan = ComputeRollLayerSpan(rollTarget - rollFrom);

                UpdateChargeVisual(rollCurrent);
            }

            if (!rollActive)
            {
                return;
            }

            if (rollDuration <= 0f)
            {
                FinishRoll();
                return;
            }

            rollTimer -= Time.deltaTime;
            float progress = Mathf.Clamp01(1f - rollTimer / rollDuration);
            rollLayers = rollLayersAtStart + rollLayerSpan * progress;
            ApplyRollValue(InterpolateCharge(rollFrom, rollTarget, progress));

            if (rollTimer <= 0f)
            {
                FinishRoll();
            }
        }

        /// <summary>
        /// 這一次滾動要讓條子推進幾層：以「本次增量」為尺度，最少 0.35 層、最多 10 層。
        /// 所以不論總能量多大，每一次能量進來都會有看得見的推進。
        /// </summary>
        private double ComputeRollLayerSpan(BigNumber delta)
        {
            BigNumber capacity = ScoringManager.Instance.ChargeCapacity;
            if (capacity <= BigNumber.Zero || delta <= BigNumber.Zero)
            {
                return 0.0;
            }

            double pipes = (delta / capacity).ToDouble();
            if (double.IsNaN(pipes) || pipes <= 0.0)
            {
                return 0.0;
            }

            if (double.IsInfinity(pipes))
            {
                return Mathf.Max(0.01f, barRollMaxLayers);
            }

            return System.Math.Min(System.Math.Max(pipes, Mathf.Max(0.01f, barRollMinLayers)), Mathf.Max(0.01f, barRollMaxLayers));
        }

        private void AbsorbQueuedEnergy()
        {
            BigNumber total = BigNumber.Zero;

            while (energyAddQueue.Count > 0)
            {
                total += energyAddQueue.Dequeue();
            }

            if (total > BigNumber.Zero)
            {
                ScoringManager.Instance.AddCharge(total);
            }
        }

        private void ApplyRollValue(BigNumber raw)
        {
            rollCurrent = raw;
            UpdateChargeVisual(rollCurrent);
        }

        private void FinishRoll()
        {
            ApplyRollValue(rollTarget);
            rollActive = false;
        }

        /// <summary>對數內插：讓「位數」等速成長。from 是 0 時退回線性。</summary>
        private static BigNumber InterpolateCharge(BigNumber from, BigNumber to, float t)
        {
            if (t <= 0f) return from;
            if (t >= 1f) return to;
            if (from <= BigNumber.Zero) return to * t;
            if (to <= from) return to;

            double logFrom = Log10Big(from);
            double logTo = Log10Big(to);
            if (logTo <= logFrom) return to;

            return FromLog10(logFrom + (logTo - logFrom) * t);
        }

        private static double Log10Big(BigNumber value)
        {
            if (value.mantissa <= 0.0) return 0.0;
            return System.Math.Log10(value.mantissa) + value.exponent;
        }

        private static BigNumber FromLog10(double log)
        {
            if (double.IsNaN(log) || double.IsInfinity(log)) return BigNumber.Zero;

            double maxLog = int.MaxValue - 2;
            if (log > maxLog) log = maxLog;
            if (log < -300.0) return BigNumber.Zero;

            double floor = System.Math.Floor(log);
            double mantissa = System.Math.Pow(10.0, log - floor);
            return new BigNumber(mantissa, (int)floor);
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

            // 分層 Image 由 UI 端自動生成。條子只顯示尾數（最多 10 條），所以下限取 10。
            int tierCount = (tierMultipliers != null && tierMultipliers.Length > 0) ? tierMultipliers.Length : 1;
            BattleUIManager.Instance.SetOverchargeLayerCount(Mathf.Max(10, tierCount));

            int tier = GetTier(displayed);
            float multiplier = GetTierMultiplier(tier);
            BattleUIManager.Instance.ShowEnergy(displayed, ScoringManager.Instance.ChargeCapacity, tier, multiplier, GetBaseDamage(displayed), rollLayers);
        }

        /// <summary>基數：能量換算成傷害，但還沒乘倍率。</summary>
        private BigNumber GetBaseDamage(BigNumber energy)
        {
            if (energy <= BigNumber.Zero)
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

            return energy * (config.damagePerShot / capacity);
        }

        /// <summary>
        /// 倍率階數 = 能量的「位數」（每 ×10 能量升一階），1 起算。0 代表還沒點火。
        /// 條子只顯示尾數（1~10 層），位數單獨用階數表示，所以大數字也能一眼讀懂。
        /// </summary>
        private int GetTier(BigNumber energy)
        {
            BigNumber capacity = ScoringManager.Instance.ChargeCapacity;
            if (capacity <= BigNumber.Zero || energy < capacity)
            {
                return 0;
            }

            int maxTier = (tierMultipliers != null && tierMultipliers.Length > 0) ? tierMultipliers.Length : 1;
            double bars = (energy / capacity).ToDouble();

            if (double.IsNaN(bars) || bars < 1.0)
            {
                return 1;
            }

            if (double.IsInfinity(bars))
            {
                return maxTier;
            }

            int decade = (int)System.Math.Floor(System.Math.Log10(bars));
            if (decade < 0) decade = 0;

            int tier = decade + 1;
            return Mathf.Clamp(tier, 1, maxTier);
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
            rollFrom = BigNumber.Zero;
            rollTarget = BigNumber.Zero;
            rollCurrent = BigNumber.Zero;
            rollLayers = 0.0;
            rollLayersAtStart = 0.0;
            rollLayerSpan = 0.0;

            // 能量條與基數當下就清掉，但倍率文字要留到基數飛來合併時才消失。
            if (BattleUIManager.IsInitialized)
            {
                BattleUIManager.Instance.ResetChargeForRelease();
            }

            if (!battleStarted || battleOver || !chargeEnabled) return;
            if (damage <= BigNumber.Zero) return;

            StartReleaseRitual(energy, tier, multiplier, damage);
        }

        /// <summary>釋放演出：基數 UI 飛到倍率 UI 合併，結果再飛向怪物血條；扣血與鏡頭抖動都在飛到時才發生。</summary>
        private void StartReleaseRitual(BigNumber energy, int tier, float multiplier, BigNumber damage)
        {
            Vector2 basePos = GetChargeBaseScreenPosition();
            Vector2 mergePos = GetChargeTierScreenPosition();

            PendingRelease pending = new PendingRelease
            {
                phase = ReleasePhase.Base,
                timer = releaseBaseHold + releaseBaseFlyDuration,
                baseText = GetBaseDamage(energy).ToDisplayString(),
                resultText = "-" + damage.ToDisplayString(),
                resultColor = GetTierDisplayColor(tier),
                resultFontSize = floatingTextFontSize * (releaseFontSizeBase + releaseFontSizePerTier * (tier - 1)),
                resultPunch = releasePunchBase + releasePunchPerTier * (tier - 1),
                damage = damage,
                tier = tier,
                mergePos = mergePos
            };

            if (FloatingTextManager.IsInitialized)
            {
                FloatingTextManager.Instance.SpawnHoldThenFlyScreen(basePos, mergePos, pending.baseText, releaseBaseColor, releaseBaseHold, releaseBaseFlyDuration, floatingTextFontSize * releaseBaseFontScale, 0.2f);
            }

            pendingReleases.Add(pending);
        }

        private void TickPendingReleases()
        {
            int index = 0;
            while (index < pendingReleases.Count)
            {
                PendingRelease pending = pendingReleases[index];
                pending.timer -= Time.deltaTime;

                if (pending.timer > 0f)
                {
                    index++;
                    continue;
                }

                switch (pending.phase)
                {
                    case ReleasePhase.Base:
                        // 基數已抵達並合併：倍率文字此刻才消失。
                        // 若期間已開始新的一輪蓄力，就不要清掉它的倍率。
                        if (BattleUIManager.IsInitialized && ScoringManager.Instance.CurrentCharge <= BigNumber.Zero)
                        {
                            BattleUIManager.Instance.ClearChargeTierText();
                        }

                        // 結果在合併點炸開，再飛向怪物血條。
                        if (FloatingTextManager.IsInitialized)
                        {
                            FloatingTextManager.Instance.SpawnHoldThenFlyScreen(pending.mergePos, GetMonsterHpScreenPosition(), pending.resultText, pending.resultColor, releaseCollideHold, releaseDamageFlyDuration, pending.resultFontSize, pending.resultPunch);
                        }

                        pending.phase = ReleasePhase.Fly;
                        pending.timer = releaseCollideHold + releaseDamageFlyDuration;
                        index++;
                        break;

                    default:
                        // 先移除再結算：結算可能觸發 EndBattle，屆時整份清單會被清空。
                        pendingReleases.RemoveAt(index);
                        ApplyReleaseDamage(pending);
                        break;
                }
            }
        }

        private void ApplyReleaseDamage(PendingRelease pending)
        {
            if (pending == null || pending.damage <= BigNumber.Zero) return;
            if (!battleStarted || battleOver || !chargeEnabled) return;

            ShakeCamera(pending.tier);

            CombatShotResult shot = CombatManager.Instance.ApplyRelease(pending.damage);

            if (shot.target != null)
            {
                // 傷害真的落到怪物血條上時，彈出紅色扣血飄字。
                if (FloatingTextManager.IsInitialized && shot.actualDamage > BigNumber.Zero)
                {
                    Vector2 screen = GetMonsterHpScreenPosition() + Vector2.up * 40f;
                    FloatingTextManager.Instance.SpawnFadeScreen(screen, "-" + shot.actualDamage.ToDisplayString(), monsterDamageTextColor, monsterDamageTextDuration, 30f, pending.resultFontSize, monsterDamageTextPunch);
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
                chargeEnabled = false;
            }
        }

        private Color GetTierDisplayColor(int tier)
        {
            if (!BattleUIManager.IsInitialized)
            {
                return monsterDamageTextColor;
            }

            return BattleUIManager.Instance.GetTierDisplayColor(tier);
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
            float hardFloor = Mathf.Max(0.01f, minChargeRollDuration);
            float ceiling = Mathf.Max(hardFloor, maxChargeRollDuration);

            if (capacity <= BigNumber.Zero)
            {
                return hardFloor;
            }

            double pipes = (amount / capacity).ToDouble();

            if (double.IsNaN(pipes) || pipes <= 0.0)
            {
                return hardFloor;
            }

            if (double.IsInfinity(pipes))
            {
                return Mathf.Clamp(chargeRollFloorDuration, hardFloor, ceiling);
            }

            float exponent = Mathf.Max(0.1f, chargeRollExponent);
            float knee = Mathf.Max(1f, chargeRollDecayKnee);
            float duration;

            if (pipes <= knee)
            {
                duration = Mathf.Pow((float)pipes, exponent) * chargeCountDurationPerPipe;
            }
            else
            {
                // 膝點是峰值；之後平滑「趨近」大數字漸近時長（不是硬夾住），
                // 所以超大數字不會全部變成同一個秒數，小額也不會被地板撐慢。
                float peak = Mathf.Max(hardFloor, Mathf.Pow(knee, exponent) * chargeCountDurationPerPipe);
                float asymptote = Mathf.Clamp(chargeRollFloorDuration, hardFloor, peak);
                float decay = Mathf.Max(0f, chargeRollDecayExponent);
                float t = Mathf.Pow((float)(knee / pipes), decay);
                duration = asymptote + (peak - asymptote) * t;
            }

            return Mathf.Clamp(duration, hardFloor, ceiling);
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

            while (pendingReleases.Count > 0)
            {
                PendingRelease pending = pendingReleases[0];
                pendingReleases.RemoveAt(0);
                ApplyReleaseDamage(pending);
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
            rollFrom = ScoringManager.Instance.CurrentCharge;
            rollTarget = rollFrom;
            rollCurrent = rollFrom;

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
            return pendingPocketTexts.Count > 0 || pendingReleases.Count > 0 || energyAddQueue.Count > 0 || rollActive || (charging && chargeEnabled);
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
            pendingReleases.Clear();
            energyAddQueue.Clear();
            rollActive = false;
            rollCurrent = BigNumber.Zero;
            rollFrom = BigNumber.Zero;
            rollTarget = BigNumber.Zero;
            rollLayers = 0.0;
            rollLayersAtStart = 0.0;
            rollLayerSpan = 0.0;
            charging = false;
            chargeIdleTimer = 0f;
            releaseCooldown = 0f;
            settleBusy = false;
            chargeEnabled = false;

            if (BattleUIManager.IsInitialized)
            {
                BattleUIManager.Instance.ClearAllChargeTexts();
            }
        }

        private Vector2 GetChargeScreenPosition()
        {
            if (!BattleUIManager.IsInitialized)
            {
                return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }

            return BattleUIManager.Instance.GetChargeScreenPosition();
        }

        private Vector2 GetChargeBaseScreenPosition()
        {
            if (!BattleUIManager.IsInitialized)
            {
                return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }

            return BattleUIManager.Instance.GetChargeBaseScreenPosition();
        }

        private Vector2 GetChargeTierScreenPosition()
        {
            if (!BattleUIManager.IsInitialized)
            {
                return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }

            return BattleUIManager.Instance.GetChargeTierScreenPosition();
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
