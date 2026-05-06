using DriverLedger.ViewModels;

namespace DriverLedger.Views
{
    public partial class AddVehiclePage : ContentPage
    {
        public AddVehiclePage(AddVehicleViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}

