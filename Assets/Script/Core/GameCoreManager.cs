namespace Pinball.Core
{
    public class GameCoreManager : Singleton<GameCoreManager>, ISetupInitializer
    {
        public DeterministicRng rng;

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this) return;

            SetupRegistry.RegisterInitializer(this);
        }

        public void Setup(ISetupReporter reporter)
        {
            if (rng == null)
            {
                rng = new DeterministicRng(12345);
            }

            reporter.ReportReady(this);
        }
    }
}
