using System.Security.Cryptography;
using System.Text;
using DriverLedger.Models;

namespace DriverLedger.Services
{
    /// <summary>
    /// Computes and verifies the immutability hash for <see cref="Settlement"/> records.
    ///
    /// Design:
    ///   The hash is a SHA-256 digest of a deterministic canonical string built from
    ///   the financial fields that define the settlement's accounting result.
    ///   If ANY of these fields are modified outside the application (e.g. by a direct
    ///   SQLite editor), the hash will no longer match and the UI shows a warning.
    ///
    /// Canonical input format (pipe-delimited, culture-invariant decimals):
    ///   "{Date:yyyyMMdd}|{DriverId}|{VehicleId}|{TotalIncome:F4}|{DriverShare:F4}|
    ///    {OwnerCngShare:F4}|{DriverCngShare:F4}|{DriverChallanTotal:F4}|
    ///    {TotalOwnerExpenses:F4}|{NetDriverPayable:F4}|{CalculatorVersion}"
    ///
    /// Why F4 (4 decimal places)?
    ///   Indian taxi accounting uses amounts like ₹12.50 (halves). 4 decimal places
    ///   gives headroom while remaining human-readable in diagnostic logs.
    ///
    /// Legacy rows (CalculationHash == "" after Migration_008):
    ///   VerifyHash returns <see cref="HashVerificationResult.NotStamped"/> — no warning shown.
    ///   Hash is stamped on next legitimate edit via <see cref="StampIntegrity"/>.
    /// </summary>
    public sealed class SettlementIntegrityService
    {
        // ── Hash computation ──────────────────────────────────────────────────

        /// <summary>
        /// Computes the canonical SHA-256 hex hash for a settlement's financial fields.
        /// </summary>
        public string ComputeHash(Settlement s)
        {
            string canonical = BuildCanonical(s);
            byte[] bytes     = Encoding.UTF8.GetBytes(canonical);
            byte[] hash      = SHA256.HashData(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        /// <summary>
        /// Verifies whether a settlement's stored hash matches its current field values.
        /// </summary>
        public HashVerificationResult VerifyHash(Settlement s)
        {
            if (string.IsNullOrEmpty(s.CalculationHash))
                return HashVerificationResult.NotStamped;

            string expected = ComputeHash(s);
            return string.Equals(expected, s.CalculationHash, StringComparison.OrdinalIgnoreCase)
                ? HashVerificationResult.Valid
                : HashVerificationResult.Tampered;
        }

        /// <summary>
        /// Sets <see cref="Settlement.CalculationHash"/>, <see cref="Settlement.FormulaVersion"/>,
        /// and (if this is a new record) leaves <see cref="Settlement.RevisionNumber"/> at 0.
        /// For edits, the caller is responsible for incrementing RevisionNumber before calling this.
        /// </summary>
        public void StampIntegrity(Settlement s, int calculatorVersion)
        {
            s.FormulaVersion   = calculatorVersion;
            s.CalculationHash  = ComputeHash(s);
        }

        /// <summary>
        /// Increments RevisionNumber and re-stamps the hash.
        /// Call this from the repository on every UPDATE.
        /// </summary>
        public void StampRevision(Settlement s, int calculatorVersion)
        {
            s.RevisionNumber++;
            StampIntegrity(s, calculatorVersion);
        }

        // ── Private ───────────────────────────────────────────────────────────

        private static string BuildCanonical(Settlement s) =>
            string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"{s.Date:yyyyMMdd}|{s.DriverId}|{s.VehicleId}" +
                $"|{s.TotalIncome:F4}|{s.DriverShare:F4}" +
                $"|{s.OwnerCngShare:F4}|{s.DriverCngShare:F4}" +
                $"|{s.DriverChallanTotal:F4}|{s.TotalOwnerExpenses:F4}" +
                $"|{s.NetDriverPayable:F4}|{s.CalculatorVersion}");
    }

    /// <summary>Result of a settlement hash verification check.</summary>
    public enum HashVerificationResult
    {
        /// <summary>Hash matched — data is intact since last legitimate save.</summary>
        Valid,

        /// <summary>Hash mismatch — financial fields were modified outside the app.</summary>
        Tampered,

        /// <summary>
        /// Hash is empty — settlement was created before Migration_008
        /// (or by a very old version). No tampering can be inferred.
        /// </summary>
        NotStamped
    }
}
