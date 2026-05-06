using System.Collections.ObjectModel;
using System.Windows.Input;
using DriverLedger.Models;
using DriverLedger.Repositories;
using DriverLedger.Services;
using DriverLedger.Views;

namespace DriverLedger.ViewModels
{
    public class VehicleListViewModel : BaseViewModel
    {
        private readonly IVehicleRepository _vehicleRepo;
        private readonly INavigationService _nav;

        private List<Vehicle> _allVehicles = new();
        private string _searchText = string.Empty;

        public ObservableCollection<Vehicle> Vehicles { get; } = new();

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    ApplyFilter();
            }
        }

        public int    VehicleCount      => Vehicles.Count;
        public string VehicleCountLabel => $"{VehicleCount} vehicle{(VehicleCount == 1 ? "" : "s")}";

        public ICommand AddVehicleCommand   { get; }
        public ICommand EditVehicleCommand  { get; }
        public ICommand AssignDriverCommand { get; }
        public ICommand RefreshCommand      { get; }

        public VehicleListViewModel(IVehicleRepository vehicleRepo, INavigationService nav)
        {
            _vehicleRepo = vehicleRepo;
            _nav         = nav;
            Title        = "Vehicles";

            AddVehicleCommand  = new Command(async () => await _nav.GoToAsync(nameof(AddVehiclePage)));
            EditVehicleCommand = new Command<Vehicle>(async (vehicle) =>
            {
                if (vehicle is null) return;
                await _nav.GoToAsync($"{nameof(AddVehiclePage)}?vehicleId={vehicle.Id}");
            });
            AssignDriverCommand = new Command<Vehicle>(async (vehicle) =>
            {
                if (vehicle is null) return;
                await _nav.GoToAsync($"{nameof(AssignDriverPage)}?vehicleId={vehicle.Id}");
            });
            RefreshCommand = new Command(async () => await LoadVehiclesAsync());
        }

        public async Task LoadVehiclesAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                _allVehicles = await _vehicleRepo.GetAllVehiclesAsync();
                ApplyFilter();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VehicleListViewModel] Load error: {ex.Message}");
            }
            finally { IsBusy = false; }
        }

        private void ApplyFilter()
        {
            var term = _searchText.Trim().ToLowerInvariant();
            var filtered = string.IsNullOrEmpty(term)
                ? _allVehicles
                : _allVehicles.Where(v =>
                    v.VehicleNumber.ToLowerInvariant().Contains(term) ||
                    v.VehicleModel.ToLowerInvariant().Contains(term)).ToList();

            Vehicles.Clear();
            foreach (var v in filtered) Vehicles.Add(v);
            OnPropertyChanged(nameof(VehicleCount));
            OnPropertyChanged(nameof(VehicleCountLabel));
        }
    }
}
