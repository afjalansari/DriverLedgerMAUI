using DriverLedger.ViewModels;

namespace DriverLedger.Views
{
    public partial class DriverListPage : ContentPage
    {
        private readonly DriverListViewModel _viewModel;

        public DriverListPage(DriverListViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.LoadDriversAsync();
        }
    }
}

