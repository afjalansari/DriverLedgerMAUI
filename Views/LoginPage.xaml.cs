using DriverLedger.ViewModels;

namespace DriverLedger.Views
{
    public partial class LoginPage : ContentPage
    {
        private readonly LoginViewModel _vm;

        public LoginPage(LoginViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            BindingContext = _vm;

            _vm.RequestFocusPassword = () => EntryPassword.Focus();
        }

        private void OnPageTapped(object? sender, TappedEventArgs e)
        {
            EntryMobile.Unfocus();
            EntryPassword.Unfocus();
        }
    }
}

