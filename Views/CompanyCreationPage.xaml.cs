using DriverLedger.ViewModels;

namespace DriverLedger.Views
{
    public partial class CompanyCreationPage : ContentPage
    {
        private readonly CompanyCreationViewModel _vm;

        public CompanyCreationPage(CompanyCreationViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            BindingContext = _vm;

            // Wire focus-advance commands to named entries
            _vm.RequestFocusOwnerName     = () => EntryOwnerName.Focus();
            _vm.RequestFocusMobile        = () => EntryMobile.Focus();
            _vm.RequestFocusCity          = () => EntryCity.Focus();
            _vm.RequestFocusPassword      = () => EntryPassword.Focus();
            _vm.RequestFocusConfirmPass   = () => EntryConfirmPassword.Focus();
        }

        private void OnPageTapped(object? sender, TappedEventArgs e)
        {
            // Dismiss keyboard on background tap
            EntryCompanyName.Unfocus();
            EntryOwnerName.Unfocus();
            EntryMobile.Unfocus();
            EntryCity.Unfocus();
            EntryPassword.Unfocus();
            EntryConfirmPassword.Unfocus();
        }
    }
}

