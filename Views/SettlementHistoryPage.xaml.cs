using DriverLedger.ViewModels;

namespace DriverLedger.Views
{
    public partial class SettlementHistoryPage : ContentPage
    {
        private readonly SettlementHistoryViewModel _vm;

        public SettlementHistoryPage(SettlementHistoryViewModel vm)
        {
            InitializeComponent();
            _vm          = vm;
            BindingContext = vm;
        }

        protected override async void OnNavigatedTo(NavigatedToEventArgs args)
        {
            base.OnNavigatedTo(args);
            await _vm.LoadAsync();
        }
    }
}
