using DriverLedger.ViewModels;

namespace DriverLedger.Views
{
    public partial class SettlementDetailPage : ContentPage
    {
        private readonly SettlementDetailViewModel _vm;

        public SettlementDetailPage(SettlementDetailViewModel viewModel)
        {
            InitializeComponent();
            _vm = viewModel;
            BindingContext = _vm;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            // Always reload to ensure newest data is shown (especially if returning from Edit)
            if (_vm.SettlementId > 0)
                await _vm.LoadAsync(_vm.SettlementId);
        }
    }
}
