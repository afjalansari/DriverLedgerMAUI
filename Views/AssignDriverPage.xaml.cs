using DriverLedger.ViewModels;

namespace DriverLedger.Views
{
    public partial class AssignDriverPage : ContentPage
    {
        private readonly AssignDriverViewModel _viewModel;

        public AssignDriverPage(AssignDriverViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.LoadDataAsync();
        }
    }
}

