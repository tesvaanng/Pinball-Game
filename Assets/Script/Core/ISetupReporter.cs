namespace Pinball.Core
{
    public interface ISetupReporter
    {
        void ReportReady(ISetupInitializer initializer);
    }
}
