using System.Collections.Generic;

namespace Pinball.Core
{
    public static class SetupRegistry
    {
        private static readonly List<ISetupInitializer> initializerList = new List<ISetupInitializer>();
        private static readonly List<ISetupRunner> runnerList = new List<ISetupRunner>();

        public static void RegisterInitializer(ISetupInitializer initializer)
        {
            if (initializer == null) return;

            if (!initializerList.Contains(initializer))
            {
                initializerList.Add(initializer);
            }
        }

        public static void RegisterRunner(ISetupRunner runner)
        {
            if (runner == null) return;

            if (!runnerList.Contains(runner))
            {
                runnerList.Add(runner);
            }
        }

        public static ISetupInitializer[] ConsumeInitializers()
        {
            ISetupInitializer[] result = initializerList.ToArray();
            initializerList.Clear();
            return result;
        }

        public static ISetupRunner[] ConsumeRunners()
        {
            ISetupRunner[] result = runnerList.ToArray();
            runnerList.Clear();
            return result;
        }
    }
}
