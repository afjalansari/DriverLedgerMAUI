using DriverLedger.ViewModels;

namespace DriverLedger.Views
{
    public partial class SettlementEntryPage : ContentPage
    {
        private readonly SettlementEntryViewModel _viewModel;
        public SettlementEntryPage(SettlementEntryViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.LoadVehiclesAsync();
        }
    }
}

