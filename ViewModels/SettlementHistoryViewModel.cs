using System.Collections.ObjectModel;
using System.Windows.Input;
using DriverLedger.Models;
using DriverLedger.Services;

namespace DriverLedger.ViewModels
{
    /// <summary>
    /// Fleet-wide chronological audit feed.
    /// Shows the last 50 events (create/update/delete) across all drivers and settlements.
    /// Supports filter chips: All | Settlements | Ledger | Today.
    /// </summary>
    public class SettlementHistoryViewModel : BaseViewModel
    {
        private readonly IAuditService _audit;

        // ── Backing fields ────────────────────────────────────────────────────
        private List<AuditLogDisplay> _allEntries = new();
        private string  _selectedFilter = FilterAll;
        private bool    _isRefreshing;

        // ── Filter constants ──────────────────────────────────────────────────
        private const string FilterAll         = "All";
        private const string FilterSettlements = "Settlements";
        private const string FilterLedger      = "Ledger";
        private const string FilterToday       = "Today";

        // ── Public Collections ────────────────────────────────────────────────
        public ObservableCollection<AuditLogDisplay> Entries { get; } = new();

        // ── Filter props ──────────────────────────────────────────────────────
        public List<string> FilterOptions { get; } =
            new() { FilterAll, FilterSettlements, FilterLedger, FilterToday };

        public string SelectedFilter
        {
            get => _selectedFilter;
            set { SetProperty(ref _selectedFilter, value); ApplyFilter(); }
        }

        public bool IsRefreshing
        {
            get => _isRefreshing;
            set => SetProperty(ref _isRefreshing, value);
        }

        public int    EntryCount      => Entries.Count;
        public string EntryCountLabel => $"{EntryCount} event{(EntryCount == 1 ? "" : "s")}";

        // ── Commands ──────────────────────────────────────────────────────────
        public ICommand RefreshCommand              { get; }
        public ICommand BackCommand                 { get; }
        public ICommand SetFilterAllCommand         { get; }
        public ICommand SetFilterSettlementsCommand { get; }
        public ICommand SetFilterLedgerCommand      { get; }
        public ICommand SetFilterTodayCommand       { get; }

        public SettlementHistoryViewModel(
            IAuditService     audit,
            INavigationService nav)
        {
            _audit  = audit ?? throw new ArgumentNullException(nameof(audit));

            Title          = "Activity Feed";
            RefreshCommand              = new Command(async () => await OnRefreshAsync());
            BackCommand                 = new Command(async () => await nav.GoBackAsync());
            SetFilterAllCommand         = new Command(() => SelectedFilter = FilterAll);
            SetFilterSettlementsCommand = new Command(() => SelectedFilter = FilterSettlements);
            SetFilterLedgerCommand      = new Command(() => SelectedFilter = FilterLedger);
            SetFilterTodayCommand       = new Command(() => SelectedFilter = FilterToday);
        }

        public async Task LoadAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            try
            {
                var raw = await _audit.GetRecentAsync(100);
                _allEntries = raw.Select(a => new AuditLogDisplay(a)).ToList();
                ApplyFilter();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SettlementHistoryViewModel] Load error: {ex.Message}");
            }
            finally { IsBusy = false; }
        }

        private async Task OnRefreshAsync()
        {
            IsRefreshing = true;
            try   { await LoadAsync(); }
            finally { IsRefreshing = false; }
        }

        private void ApplyFilter()
        {
            IEnumerable<AuditLogDisplay> filtered = _selectedFilter switch
            {
                FilterSettlements => _allEntries.Where(e => e.EntityType == AuditEntities.Settlement),
                FilterLedger      => _allEntries.Where(e => e.EntityType == AuditEntities.LedgerEntry),
                FilterToday       => _allEntries.Where(e => e.Timestamp.ToLocalTime().Date == DateTime.Today),
                _                 => _allEntries
            };

            Entries.Clear();
            foreach (var e in filtered) Entries.Add(e);
            OnPropertyChanged(nameof(EntryCount));
            OnPropertyChanged(nameof(EntryCountLabel));
        }
    }

    /// <summary>Display-layer wrapper for <see cref="AuditLog"/> — pure presentation logic.</summary>
    public class AuditLogDisplay
    {
        private readonly AuditLog _log;
        public AuditLogDisplay(AuditLog log) => _log = log;

        public DateTime Timestamp  => _log.Timestamp;
        public string EntityType   => _log.EntityType;
        public int    EntityId     => _log.EntityId;
        public string DriverName   => string.IsNullOrWhiteSpace(_log.DriverName) ? "—" : _log.DriverName;
        public string ChangeSummary => _log.ChangeSummary;

        public string DateDisplay => _log.Timestamp.ToLocalTime() switch
        {
            var d when d.Date == DateTime.Today              => $"Today {d:hh:mm tt}",
            var d when d.Date == DateTime.Today.AddDays(-1)  => $"Yesterday {d:hh:mm tt}",
            var d                                            => d.ToString("dd MMM yyyy, hh:mm tt")
        };

        public string ActionIcon => _log.Action switch
        {
            AuditActions.Create => "🟢",
            AuditActions.Update => "✏️",
            AuditActions.Delete => "🗑️",
            _                   => "❓"
        };

        public string EntityIcon => _log.EntityType switch
        {
            AuditEntities.Settlement  => "📋",
            AuditEntities.LedgerEntry => "💰",
            _                         => "📄"
        };

        public Color ActionColor => _log.Action switch
        {
            AuditActions.Create => Color.FromArgb("#4CAF50"),
            AuditActions.Update => Color.FromArgb("#FFA726"),
            AuditActions.Delete => Color.FromArgb("#EF5350"),
            _                   => Color.FromArgb("#78909C")
        };

        public string ActionLabel => _log.Action switch
        {
            AuditActions.Create => "Created",
            AuditActions.Update => "Edited",
            AuditActions.Delete => "Deleted",
            _                   => _log.Action
        };

        public string EntityLabel => _log.EntityType switch
        {
            AuditEntities.Settlement  => "Settlement",
            AuditEntities.LedgerEntry => "Ledger Entry",
            _                         => _log.EntityType
        };
    }
}
