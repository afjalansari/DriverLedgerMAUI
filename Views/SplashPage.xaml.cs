using DriverLedger.Services;

namespace DriverLedger.Views
{
    public partial class SplashPage : ContentPage
    {
        private readonly StartupService _startupService;

        public SplashPage(StartupService startupService)
        {
            InitializeComponent();
            _startupService = startupService;
        }

        protected override async void OnNavigatedTo(NavigatedToEventArgs args)
        {
            base.OnNavigatedTo(args);
            // Artificial small delay to allow Splash screen to render before locking thread
            await Task.Delay(500);
            await _startupService.RunAsync();
        }
    }
}

