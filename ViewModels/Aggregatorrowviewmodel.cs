using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace DriverLedger.ViewModels
{
    /// <summary>
    /// Represents a single operator bill row in the dynamic list.
    /// Each row has: operator name, bill amount (string proxy), cash amount (string proxy).
    /// When BillText or CashText changes, it fires OnChanged so the parent
    /// SettlementEntryViewModel can call Recalculate().
    /// </summary>
    public class AggregatorRowViewModel : BaseViewModel
    {
        // -- Preset operator names shown in the Picker ---------------------
        public static readonly List<string> PresetOperators = new()
        {
            "Ola", "Uber", "Rapido", "Offline", "InDrive", "Namma Yatri", "Other"
        };

        private readonly Action _onChanged;

        private string _operatorName = "Ola";
        private string _billText = string.Empty;
        private string _cashText = string.Empty;
        private decimal _bill;
        private decimal _cash;

        public string OperatorName
        {
            get => _operatorName;
            set => SetProperty(ref _operatorName, value);
        }

        /// <summary>Bill amount as string — bound to Entry.Text. Parses on set.</summary>
        public string BillText
        {
            get => _billText;
            set
            {
                SetProperty(ref _billText, value);
                _bill = Parse(value);
                _onChanged();
            }
        }

        /// <summary>Cash amount as string — bound to Entry.Text. Parses on set.</summary>
        public string CashText
        {
            get => _cashText;
            set
            {
                SetProperty(ref _cashText, value);
                _cash = Parse(value);
                _onChanged();
            }
        }

        /// <summary>Parsed decimal bill value — used by parent for calculation.</summary>
        public decimal Bill => _bill;

        /// <summary>Parsed decimal cash value — used by parent for calculation.</summary>
        public decimal Cash => _cash;

        public List<string> OperatorOptions => PresetOperators;

        public ICommand RemoveCommand { get; }

        public AggregatorRowViewModel(Action onChanged, Action<AggregatorRowViewModel> onRemove,
                                      string operatorName = "Ola")
        {
            _onChanged = onChanged;
            _operatorName = operatorName;
            RemoveCommand = new Command(() => onRemove(this));
        }

        private static decimal Parse(string? text)
            => decimal.TryParse(text, out var d) ? Math.Max(0m, d) : 0m;
    }
}

