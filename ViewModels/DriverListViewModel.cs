using System.Collections.ObjectModel;
using System.Windows.Input;
using DriverLedger.Models;
using DriverLedger.Repositories;
using DriverLedger.Services;
using DriverLedger.Views;

namespace DriverLedger.ViewModels
{
    public class DriverListViewModel : BaseViewModel
    {
        private readonly IDriverRepository  _driverRepo;
        private readonly INavigationService _nav;

        private List<Driver> _allDrivers = new();
        private string _searchText = string.Empty;

        public ObservableCollection<Driver> Drivers { get; } = new();

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    ApplyFilter();
            }
        }

        public int    DriverCount      => Drivers.Count;
        public string DriverCountLabel => $"{DriverCount} driver{(DriverCount == 1 ? "" : "s")}";

        public ICommand AddDriverCommand  { get; }
        public ICommand EditDriverCommand { get; }
        public ICommand RefreshCommand    { get; }

        public DriverListViewModel(IDriverRepository driverRepo, INavigationService nav)
        {
            _driverRepo = driverRepo;
            _nav        = nav;
            Title       = "Drivers";

            AddDriverCommand  = new Command(async () => await _nav.GoToAsync(nameof(AddDriverPage)));
            EditDriverCommand = new Command<Driver>(async (driver) =>
            {
                if (driver is null) return;
                await _nav.GoToAsync($"{nameof(AddDriverPage)}?driverId={driver.Id}");
            });
            RefreshCommand = new Command(async () => await LoadDriversAsync());
        }

        public async Task LoadDriversAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                _allDrivers = await _driverRepo.GetAllDriversAsync();
                ApplyFilter();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DriverListViewModel] Load error: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ApplyFilter()
        {
            var term = _searchText.Trim().ToLowerInvariant();
            var filtered = string.IsNullOrEmpty(term)
                ? _allDrivers
                : _allDrivers.Where(d =>
                    d.DriverName.ToLowerInvariant().Contains(term) ||
                    d.MobileNumber.Contains(term)).ToList();

            Drivers.Clear();
            foreach (var d in filtered) Drivers.Add(d);
            OnPropertyChanged(nameof(DriverCount));
            OnPropertyChanged(nameof(DriverCountLabel));
        }
    }
}
