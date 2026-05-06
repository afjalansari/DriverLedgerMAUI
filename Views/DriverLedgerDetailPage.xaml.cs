using DriverLedger.ViewModels;

namespace DriverLedger.Views;

public partial class DriverLedgerDetailPage : ContentPage
{
    private readonly DriverLedgerDetailViewModel _vm;

    public DriverLedgerDetailPage(DriverLedgerDetailViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }
}

