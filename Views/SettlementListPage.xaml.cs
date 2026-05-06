using DriverLedger.ViewModels;

namespace DriverLedger.Views
{
    public partial class SettlementListPage : ContentPage
    {
        private readonly SettlementListViewModel _viewModel;
        public SettlementListPage(SettlementListViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.LoadAsync();
        }
    }
}

