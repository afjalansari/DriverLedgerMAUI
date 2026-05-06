using DriverLedger.ViewModels;

namespace DriverLedger.Views;

public partial class DriverLedgerListPage : ContentPage
{
    private readonly DriverLedgerListViewModel _vm;

    public DriverLedgerListPage(DriverLedgerListViewModel vm)
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

