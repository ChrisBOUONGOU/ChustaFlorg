using Microsoft.Extensions.DependencyInjection;
using ChustaFlorg.Services;

namespace ChustaFlorg
{
    public partial class App : Application
    {
        private readonly ReliabilityService _reliabilityService = new();

        public App()
        {
            InitializeComponent();
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
                _reliabilityService.LogMessage("Unhandled exception", args.ExceptionObject?.ToString() ?? "Exception inconnue");
            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                _reliabilityService.LogError("Unobserved task exception", args.Exception);
                args.SetObserved();
            };
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}