using UnityEngine;
using Pinball.Core;

namespace Pinball.Data
{
    public class DataManager : Singleton<DataManager>, ISetupInitializer
    {
        public GameConfigSO gameConfig;

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this) return;

            SetupRegistry.RegisterInitializer(this);
        }

        public void Setup(ISetupReporter reporter)
        {
            EnsureGameConfig();
            reporter.ReportReady(this);
        }

        public LevelConfigSO GetLevelConfig()
        {
            EnsureGameConfig();
            return gameConfig.levelConfig;
        }

        public int GetLevelCount()
        {
            LevelConfigSO levelConfig = GetLevelConfig();
            if (levelConfig == null) return 0;
            return levelConfig.LevelCount;
        }

        public bool IsBossLevel(int levelIndex)
        {
            LevelConfigSO levelConfig = GetLevelConfig();
            if (levelConfig == null) return false;
            return levelConfig.IsBossLevel(levelIndex);
        }

        private void EnsureGameConfig()
        {
            if (gameConfig == null)
            {
                gameConfig = ScriptableObject.CreateInstance<GameConfigSO>();
            }
        }
    }
}
