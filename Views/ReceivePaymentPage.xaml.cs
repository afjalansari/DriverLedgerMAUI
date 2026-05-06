using DriverLedger.ViewModels;

namespace DriverLedger.Views;

public partial class ReceivePaymentPage : ContentPage
{
    private readonly ReceivePaymentViewModel _vm;

    public ReceivePaymentPage(ReceivePaymentViewModel vm)
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

