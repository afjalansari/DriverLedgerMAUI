using System.Diagnostics;

namespace DriverLedger.Domain
{
    /// <summary>
    /// Immutable value object representing a monetary amount in Indian Rupees (INR).
    ///
    /// DESIGN DECISIONS:
    ///   • Uses <c>decimal</c> internally — NEVER float or double.
    ///   • All arithmetic results are rounded to 2 decimal places using
    ///     <see cref="MidpointRounding.AwayFromZero"/> (e.g., ₹0.005 → ₹0.01).
    ///     This is the standard for financial calculations (banker's rounding NOT used).
    ///   • Immutable: every operation returns a new <see cref="Money"/> instance.
    ///   • Scope: used ONLY for in-memory calculations in <see cref="Services.SettlementCalculator"/>.
    ///     Models, ViewModels, and SQLite columns remain <c>decimal</c> to preserve
    ///     backward compatibility and avoid custom SQLite type converters.
    ///
    /// USAGE PATTERN:
    ///   var income  = Money.Of(12500m);
    ///   var share   = income.Percent(50m);       // → ₹6,250.00
    ///   var net     = share - Money.Of(500m);     // → ₹5,750.00
    ///   decimal stored = net.Amount;              // → back to decimal for SQLite
    /// </summary>
    [DebuggerDisplay("₹{Amount}")]
    public readonly struct Money
        : IEquatable<Money>, IComparable<Money>
    {
        // ── Constants ─────────────────────────────────────────────────────────

        /// <summary>Canonical zero-rupee instance.</summary>
        public static readonly Money Zero = new(0m);

        /// <summary>Rounding strategy used for ALL Money arithmetic.</summary>
        private const MidpointRounding Rounding = MidpointRounding.AwayFromZero;

        // ── Core Value ───────────────────────────────────────────────────────

        /// <summary>
        /// The underlying decimal amount, rounded to exactly 2 decimal places.
        /// Use this when persisting to SQLite or passing to a ViewModel property.
        /// </summary>
        public decimal Amount { get; }

        // ── Construction ─────────────────────────────────────────────────────

        /// <summary>Creates a Money instance, immediately rounding to 2dp AwayFromZero.</summary>
        /// <param name="amount">Raw decimal value (can be negative for net payable results).</param>
        public Money(decimal amount)
        {
            Amount = Math.Round(amount, 2, Rounding);
        }

        /// <summary>Named constructor — preferred over <c>new Money(d)</c> at call sites.</summary>
        public static Money Of(decimal amount) => new(amount);

        // ── Arithmetic Operators ─────────────────────────────────────────────

        /// <summary>Adds two Money values. Result is rounded to 2dp.</summary>
        public static Money operator +(Money a, Money b) => new(a.Amount + b.Amount);

        /// <summary>Subtracts b from a. Result MAY be negative (e.g., NetDriverPayable).</summary>
        public static Money operator -(Money a, Money b) => new(a.Amount - b.Amount);

        /// <summary>Unary negation — flips sign (e.g., for display of driver-owes-owner).</summary>
        public static Money operator -(Money a) => new(-a.Amount);

        // ── Percentage Helper ────────────────────────────────────────────────

        /// <summary>
        /// Computes <c>this * pct / 100</c>, rounded to 2dp.
        /// This is the ONLY correct way to compute income/CNG percentage splits —
        /// using a percentage stored as 0–100 (not 0.0–1.0).
        ///
        /// Examples:
        ///   Money.Of(12500m).Percent(50m)  → ₹6,250.00
        ///   Money.Of(800m).Percent(40m)    → ₹320.00
        ///   Money.Of(100m).Percent(33.33m) → ₹33.33
        /// </summary>
        /// <param name="percentage">Value in range 0–100 (inclusive).</param>
        public Money Percent(decimal percentage)
        {
            if (percentage < 0m || percentage > 100m)
                throw new ArgumentOutOfRangeException(nameof(percentage),
                    $"Percentage must be 0–100. Received: {percentage}");

            return new Money(Amount * percentage / 100m);
        }

        // ── State Queries ────────────────────────────────────────────────────

        /// <summary>True when Amount &gt; 0 (owner owes driver).</summary>
        public bool IsPositive => Amount > 0m;

        /// <summary>True when Amount &lt; 0 (driver owes owner).</summary>
        public bool IsNegative => Amount < 0m;

        /// <summary>True when Amount == 0 exactly (perfectly settled).</summary>
        public bool IsZero => Amount == 0m;

        /// <summary>Absolute value — useful for display when direction is shown separately.</summary>
        public Money Abs() => new(Math.Abs(Amount));

        // ── Comparison ───────────────────────────────────────────────────────

        public static bool operator ==(Money a, Money b) => a.Amount == b.Amount;
        public static bool operator !=(Money a, Money b) => a.Amount != b.Amount;
        public static bool operator >(Money a, Money b)  => a.Amount >  b.Amount;
        public static bool operator <(Money a, Money b)  => a.Amount <  b.Amount;
        public static bool operator >=(Money a, Money b) => a.Amount >= b.Amount;
        public static bool operator <=(Money a, Money b) => a.Amount <= b.Amount;

        /// <inheritdoc />
        public int CompareTo(Money other) => Amount.CompareTo(other.Amount);

        /// <inheritdoc />
        public bool Equals(Money other) => Amount == other.Amount;

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is Money m && Equals(m);

        /// <inheritdoc />
        public override int GetHashCode() => Amount.GetHashCode();

        // ── Display ──────────────────────────────────────────────────────────

        /// <summary>Formats as Indian Rupee with 2dp: "₹12,500.00"</summary>
        public override string ToString() => $"₹{Amount:N2}";

        /// <summary>Formats without paise for compact UI display: "₹12,500"</summary>
        public string ToDisplayString() => $"₹{Amount:N0}";

        /// <summary>
        /// Returns a human-readable arithmetic expression for audit traces.
        /// Example: Money.Of(12500m).PercentTrace(50m) → "₹12,500 × 50% = ₹6,250"
        /// </summary>
        public string PercentTrace(decimal percentage)
        {
            var result = Percent(percentage);
            return $"₹{Amount:N0} × {percentage}% = ₹{result.Amount:N0}";
        }
    }
}
