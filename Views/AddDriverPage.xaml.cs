using DriverLedger.ViewModels;

namespace DriverLedger.Views
{
    public partial class AddDriverPage : ContentPage
    {
        public AddDriverPage(AddDriverViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}

