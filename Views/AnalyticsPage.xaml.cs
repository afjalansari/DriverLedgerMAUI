using DriverLedger.ViewModels;

namespace DriverLedger.Views;

public partial class AnalyticsPage : ContentPage
{
    private readonly AnalyticsViewModel _vm;

    public AnalyticsPage(AnalyticsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
        // Force GraphicsView to redraw after data loads
        EarningsGraphicsView.Invalidate();
        FuelGraphicsView.Invalidate();
        OwnerGraphicsView.Invalidate();
    }
}

