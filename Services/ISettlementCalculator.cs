using DriverLedger.Models;

namespace DriverLedger.Services
{
    /// <summary>
    /// Abstraction over the financial settlement calculation engine.
    ///
    /// Exposing <see cref="SettlementCalculator"/> behind this interface allows:
    ///   1. ViewModel unit tests that mock the calculator without touching SQLite.
    ///   2. Future formula versioning — a v2 engine can be swapped in via DI.
    ///   3. Consistent injection pattern aligned with all other services.
    ///
    /// The concrete implementation is <see cref="SettlementCalculator"/> (Singleton in DI).
    /// </summary>
    public interface ISettlementCalculator
    {
        /// <summary>
        /// Computes a settlement AND returns the human-readable audit trace.
        /// Use when you need the trace for receipt generation.
        /// </summary>
        SettlementCalculator.CalculationResult CalculateWithTrace(
            SettlementCalculator.CalculationRequest request);

        /// <summary>
        /// Convenience overload — computes and returns the Settlement only.
        /// Equivalent to <c>CalculateWithTrace(request).Settlement</c>.
        /// </summary>
        Settlement Calculate(SettlementCalculator.CalculationRequest request);

        /// <summary>
        /// Reconstructs a human-readable calculation trace from a persisted
        /// <see cref="Settlement"/> record (no re-calculation needed).
        /// Used in PDF generation and receipt sharing.
        /// </summary>
        SettlementCalculator.CalculationTrace TraceFromSettlement(Settlement s);
    }
}
