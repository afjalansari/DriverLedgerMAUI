using DriverLedger.ViewModels;

namespace DriverLedger.Views
{
    public partial class CompanyPage : ContentPage
    {
        private readonly CompanyCreationViewModel _viewModel;

        public CompanyPage(CompanyCreationViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;

            // Wire focus-advance callbacks so the keyboard Return key
            // moves focus to the next field (same pattern used in LoginPage).
            _viewModel.RequestFocusOwnerName   = () => OwnerNameEntry.Focus();
            _viewModel.RequestFocusMobile      = () => MobileEntry.Focus();
            _viewModel.RequestFocusCity        = () => CityEntry.Focus();
            _viewModel.RequestFocusPassword    = () => PasswordEntry.Focus();
            _viewModel.RequestFocusConfirmPass = () => ConfirmPasswordEntry.Focus();
        }
    }
}
