using DriverLedger.ViewModels;

namespace DriverLedger.Views
{
    public partial class ForgotPasswordPage : ContentPage
    {
        private readonly ForgotPasswordViewModel _vm;

        public ForgotPasswordPage(ForgotPasswordViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            BindingContext = _vm;

            // Wire focus-advance callbacks
            _vm.RequestFocusNewPassword     = () => EntryNewPassword.Focus();
            _vm.RequestFocusConfirmPassword = () => EntryConfirmPassword.Focus();
        }

        private void OnPageTapped(object? sender, TappedEventArgs e)
        {
            EntryMobile.Unfocus();
            EntryNewPassword.Unfocus();
            EntryConfirmPassword.Unfocus();
        }
    }
}

