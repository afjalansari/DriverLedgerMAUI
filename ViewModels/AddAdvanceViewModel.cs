using System.Collections.ObjectModel;
using System.Windows.Input;
using DriverLedger.Models;
using DriverLedger.Repositories;
using DriverLedger.Services;

namespace DriverLedger.ViewModels
{
    // BUG-5: [QueryProperty] so QuickAdvanceCommand from DriverLedgerListViewModel
    // can pass ?driverId=X and have the driver auto-selected.
    [QueryProperty(nameof(PreselectedDriverId), "driverId")]
    public class AddAdvanceViewModel : BaseViewModel
    {
        private readonly IDriverRepository       _driverRepo;
        private readonly IDriverLedgerRepository _ledgerRepo;
        private readonly INavigationService      _nav;
        private readonly IDialogService          _dialog;

        private Driver?  _selectedDriver;
        private decimal  _amount;
        private string   _amountText   = string.Empty;
        private string   _description  = string.Empty;
        private DateTime _date         = DateTime.Today;
        private int      _preselectedDriverId;

        public ObservableCollection<Driver> Drivers { get; } = new();

        public Driver? SelectedDriver
        {
            get => _selectedDriver;
            set => SetProperty(ref _selectedDriver, value);
        }

        public decimal Amount
        {
            get => _amount;
            set => SetProperty(ref _amount, value);
        }

        /// <summary>
        /// String-typed proxy for the Amount Entry so Android decimal binding works reliably.
        /// Parses to decimal on set; clears to empty on reset.
        /// </summary>
        public string AmountText
        {
            get => _amountText;
            set
            {
                SetProperty(ref _amountText, value);
                Amount = decimal.TryParse(value, out var d) ? Math.Max(0m, d) : 0m;
            }
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public DateTime Date
        {
            get => _date;
            set => SetProperty(ref _date, value);
        }

        /// <summary>
        /// Set via [QueryProperty] when navigating from DriverLedgerListViewModel.
        /// Auto-selects the matching driver after the driver list is loaded. (BUG-5)
        /// </summary>
        public int PreselectedDriverId
        {
            get => _preselectedDriverId;
            set
            {
                _preselectedDriverId = value;
                // Attempt immediate selection if drivers are already loaded
                if (value > 0 && Drivers.Count > 0)
                    SelectedDriver = Drivers.FirstOrDefault(d => d.Id == value);
            }
        }

        public ICommand SaveCommand   { get; }
        public ICommand CancelCommand { get; }

        public AddAdvanceViewModel(
            IDriverRepository       driverRepo,
            IDriverLedgerRepository ledgerRepo,
            INavigationService      nav,
            IDialogService          dialog)
        {
            _driverRepo = driverRepo;
            _ledgerRepo = ledgerRepo;
            _nav        = nav;
            _dialog     = dialog;
            Title       = "Add Advance";

            SaveCommand   = new Command(async () => await OnSaveAsync());
            CancelCommand = new Command(async () => await _nav.GoBackAsync());
        }

        public async Task LoadDriversAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var drivers = await _driverRepo.GetAllDriversAsync();
                Drivers.Clear();
                foreach (var d in drivers.Where(d => d.Status == DriverStatus.Active))
                    Drivers.Add(d);

                // BUG-5: apply pre-selection after list is populated
                if (_preselectedDriverId > 0)
                    SelectedDriver = Drivers.FirstOrDefault(d => d.Id == _preselectedDriverId);
            }
            finally { IsBusy = false; }
        }

        private async Task OnSaveAsync()
        {
            // BUG-4: validate before setting IsBusy so failures don't leave the button locked
            if (_selectedDriver is null)
            {
                await _dialog.ShowAlertAsync("Validation", "Please select a driver.");
                return;
            }
            if (Amount <= 0)
            {
                await _dialog.ShowAlertAsync("Validation", "Amount must be greater than 0.");
                return;
            }

            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var desc  = string.IsNullOrWhiteSpace(Description) ? "Advance" : Description.Trim();
                var entry = new DriverLedgerEntry
                {
                    DriverId        = _selectedDriver.Id,
                    Date            = Date.ToUniversalTime(),
                    TransactionType = TransactionTypes.Advance,
                    Description     = desc,
                    Debit           = Amount,  // BUG-1 fix: advance = owner gives TO driver → Debit
                    Credit          = 0m
                };

                await _ledgerRepo.AddLedgerEntryAsync(entry);
                await _dialog.ShowAlertAsync("✅ Saved", "Advance recorded successfully.");

                // Reset form
                SelectedDriver = null;
                AmountText     = string.Empty;
                Description    = string.Empty;
                Date           = DateTime.Today;

                await _nav.GoBackAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AddAdvanceViewModel] Save error: {ex.Message}");
                await _dialog.ShowAlertAsync("Error", "Failed to save advance. Please try again.");
            }
            finally { IsBusy = false; }
        }
    }
}
