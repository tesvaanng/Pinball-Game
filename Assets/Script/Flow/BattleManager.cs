using UnityEngine;
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

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this) return;

            SetupRegistry.RegisterRunner(this);
        }

        private void Start()
        {
            SubscribeEvents();

            if (!SetupManager.IsInitialized)
            {
                StartBattle();
            }
        }

        public void RunAfterSetup()
        {
            StartBattle();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
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

            CombatManager.Instance.CreateMonsters(levelConfig, levelIndex);

            ballsLeft = config.ballsPerRound;
            round = 1;
            levelClearPending = false;

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

        private void HandleBallScoreChanged(int ballId, BigNumber ballScore)
        {
            if (!battleStarted || battleOver) return;

            RefreshBallScoreUI(ballScore);
        }

        private void HandlePocketEntered(int ballId, BigNumber multiplier)
        {
            if (!battleStarted || battleOver) return;

            SettleBall(ballId, multiplier);
        }

        private void HandleBallTimedOut(int ballId)
        {
            if (!battleStarted || battleOver) return;

            SettleBall(ballId, BigNumber.One);
        }

        private void SettleBall(int ballId, BigNumber multiplier)
        {
            if (!battleStarted || battleOver) return;
            if (!BoardManager.IsInitialized) return;

            GameConfigSO config = DataManager.Instance.gameConfig;
            BigNumber ballScore = BoardManager.Instance.GetBallScore(ballId);
            BoardManager.Instance.RemoveBall(ballId);

            BallResult result = ScoringManager.Instance.SettleBall(ballScore, multiplier);
            bool levelCleared = CombatManager.Instance.ApplyFire(result.fireCount, config.damagePerShot);

            RefreshScoreUI();
            RefreshChargeUI();
            RefreshMonsterUI();
            RefreshPlayerHpUI();

            if (CombatManager.Instance.IsPlayerDead())
            {
                EndBattle(false);
                return;
            }

            if (levelCleared)
            {
                levelClearPending = true;
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

            if (BoardManager.IsInitialized)
            {
                BoardManager.Instance.ClearBalls();
            }

            if (BattleUIManager.IsInitialized)
            {
                BattleUIManager.Instance.ShowResult(win ? "Win" : "Lose");
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

            BattleUIManager.Instance.ShowCharge(ScoringManager.Instance.CurrentCharge);
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
