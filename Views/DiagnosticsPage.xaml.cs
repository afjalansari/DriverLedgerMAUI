using DriverLedger.ViewModels;

namespace DriverLedger.Views;

public partial class DiagnosticsPage : ContentPage
{
    private readonly DiagnosticsViewModel _vm;

    public DiagnosticsPage(DiagnosticsViewModel vm)
    {
        InitializeComponent();
        _vm         = vm;
        BindingContext = vm;
    }

    protected override async void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        await _vm.LoadAsync();
    }
}
