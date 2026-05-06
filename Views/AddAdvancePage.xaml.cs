using DriverLedger.ViewModels;

namespace DriverLedger.Views;

public partial class AddAdvancePage : ContentPage
{
    private readonly AddAdvanceViewModel _vm;

    public AddAdvancePage(AddAdvanceViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadDriversAsync();
    }
}

