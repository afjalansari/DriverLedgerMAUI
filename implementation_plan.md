# DriverLedger — Hardening & Refactor Plan

> **Constraint (never negotiable):**  
> NEVER change settlement calculation behavior. NEVER break existing SQLite data.  
> Historical settlements must remain mathematically identical forever.  
> App is 100% offline. No cloud. No analytics. No telemetry.

---

## Approach: 8 Independent Phases

Each phase is independently mergeable, backward-compatible, and does not require the next phase to function. Phases are ordered by risk/impact — high impact + low risk first.

---

## Phase 0 — Immediate Critical Fixes *(no new files, 1–2 hours)*

These are confirmed bugs identified in the codebase report. No design changes.

### 0A — Delete `Helpers/BackupService.cs` (legacy duplicate)

`Helpers/BackupService.cs` is a legacy class (namespace `DriverLedger.Helpers`, no interface, direct UI alerts) that is **NOT registered in DI** and not used anywhere in the current ViewModel layer. The production code path uses `Services/DatabaseBackupService.cs`.

**Action:** Delete `Helpers/BackupService.cs`.

> [!WARNING]
> Before deleting, confirm zero references using grep. If any XAML code-behind references it, replace with the DI-injected `IBackupService` call.

### 0B — Fix `ExportOrchestrator` running balance bug

[ExportOrchestrator.cs](file:///g:/Application%20MAUI/New%20folder/Prototype%20DriverLedger/P1/DriverLedger/Services/ExportOrchestrator.cs#L133)

**Current (wrong):**
```csharp
RunningBalance = e.Debit - e.Credit  // per-row subtraction ≠ running total
```

**Fix:**
```csharp
RunningBalance = e.Balance  // use the Balance field maintained by RebalanceInTransaction
```

### 0C — Fix `SettlementDetailViewModel` — `_driverFaultChallan` always 0

[SettlementDetailViewModel.cs](file:///g:/Application%20MAUI/New%20folder/Prototype%20DriverLedger/P1/DriverLedger/ViewModels/SettlementDetailViewModel.cs#L152)

The model now has `DriverChallanTotal` (added in the BUG-2 fix). Update:

```csharp
// REPLACE
// challans are not stored in ExpenseItems, so _driverFaultChallan stays 0.
_driverFaultChallan = 0m;

// WITH
_driverFaultChallan = _settlement.DriverChallanTotal;
```

Also add the missing public property:
```csharp
public decimal DriverChallanTotal { get => _driverFaultChallan; ... }
public decimal DriverCngShare     { get => _settlement?.DriverCngShare ?? 0m; ... }
```

### 0D — Fix `BackupService` sync-over-async anti-pattern

[BackupService.cs](file:///g:/Application%20MAUI/New%20folder/Prototype%20DriverLedger/P1/DriverLedger/Services/BackupService.cs#L63)

**Current:**
```csharp
var conn = _db.GetRawConnectionAsync().GetAwaiter().GetResult(); // WRONG — sync-over-async
```

**Fix:**
```csharp
var conn = await _db.GetRawConnectionAsync(); // await it BEFORE Task.Run
await Task.Run(() =>
{
    conn.Execute("PRAGMA wal_checkpoint(TRUNCATE);");
});
```

### 0E — Unify `RecentSettlementRow` / `RecentSettlementItem` DTO duplication

Two identical DTOs exist: `DashboardSummaryService.RecentSettlementRow` (private inner type) and `DTOs/RecentSettlementItem.cs`. 

**Fix:** Remove the private `RecentSettlementRow` class from `DashboardSummaryService`. Change `GetDailySummaryAsync` to produce `List<RecentSettlementItem>` directly. Remove the manual mapping loop in `DashboardViewModel.LoadAsync`.

---

## Phase 1 — Database Migration Framework *(new folder, ~400 LOC)*

> [!IMPORTANT]
> This is the foundation for all future schema changes. Must be in place before any new columns are added.

### Problem
`DatabaseService.InitializeAsync()` calls `CreateTableAsync<T>()` at every launch. SQLite's `CreateTableAsync` is additive (it never drops columns), but there is:
- No version tracking
- No ability to rename columns or create indexes
- No history of what has run
- No rollback safety

### New Folder Structure

```
Database/
  DatabaseService.cs           (existing — no changes)
  Migrations/
    IMigration.cs              [NEW]
    MigrationBase.cs           [NEW]
    MigrationRunner.cs         [NEW]
    001_InitialSchema.cs       [NEW]
    002_AddDriverCngFields.cs  [NEW]
    003_AddAuditIndexes.cs     [NEW]
```

### `SchemaVersion` Table (new SQLite table)

```csharp
[Table("SchemaVersion")]
public class SchemaVersion
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    public int Version { get; set; }
    public string MigrationName { get; set; } = string.Empty;
    public DateTime AppliedAt { get; set; }
    public bool Success { get; set; }
}
```

### `IMigration` Interface

```csharp
public interface IMigration
{
    int Version { get; }
    string Name { get; }
    void Up(SQLiteConnection conn);   // always sync — runs inside RunInTransaction
    void Down(SQLiteConnection conn); // rollback — optional for safety
}
```

### `MigrationRunner` Logic

```
1. Ensure SchemaVersion table exists
2. Read all applied versions from SchemaVersion
3. Filter pending migrations (registered but not yet applied)
4. For each pending migration (ordered by Version):
   a. Begin transaction
   b. Execute migration.Up(conn)
   c. Insert SchemaVersion record
   d. Commit
5. Log result
```

### Migrations

**001_InitialSchema** — creates all 8 tables (idempotent, uses `CREATE TABLE IF NOT EXISTS`)  
**002_AddDriverCngFields** — `ALTER TABLE Settlements ADD COLUMN DriverCngShare REAL DEFAULT 0`  
                           — `ALTER TABLE Settlements ADD COLUMN DriverChallanTotal REAL DEFAULT 0`  
**003_AddAuditIndexes** — creates composite indexes:
```sql
CREATE INDEX IF NOT EXISTS idx_settlements_date ON Settlements(Date);
CREATE INDEX IF NOT EXISTS idx_settlements_driver ON Settlements(DriverId);
CREATE INDEX IF NOT EXISTS idx_settlements_vehicle ON Settlements(VehicleId);
CREATE INDEX IF NOT EXISTS idx_ledger_driver_date ON DriverLedgerEntries(DriverId, Date);
CREATE INDEX IF NOT EXISTS idx_platform_settlement ON PlatformIncomes(SettlementId);
CREATE INDEX IF NOT EXISTS idx_expenses_settlement ON SettlementExpenses(SettlementId);
```

### Integration Point

Replace `DatabaseService.InitializeAsync()` content:
```csharp
// Old: CreateTableAsync calls for each model
// New:
await _migrationRunner.RunPendingMigrationsAsync(conn);
```

**All existing `CreateTableAsync` calls move into `001_InitialSchema.Up()`.**

> [!NOTE]
> Existing databases that have already run `CreateTableAsync` will skip Migration 001 only if the SchemaVersion table records it. For first-run on existing databases: Migration 001 detects tables already exist via `CREATE TABLE IF NOT EXISTS` (idempotent), marks itself as applied, and continues to 002+.

---

## Phase 2 — `Money` Value Object *(new file, ~80 LOC)*

### Problem
Raw `decimal` is used everywhere. There's no protection against:
- Accidentally using `float` math
- Inconsistent rounding (MidpointRounding varies by context)
- Negative money where not permitted

### New File: `Domain/Money.cs`

```csharp
[DebuggerDisplay("₹{Amount}")]
public readonly struct Money : IEquatable<Money>, IComparable<Money>
{
    public static readonly Money Zero = new(0m);
    private static readonly MidpointRounding Rounding = MidpointRounding.AwayFromZero;

    public decimal Amount { get; }

    public Money(decimal amount) => Amount = Math.Round(amount, 2, Rounding);

    public static Money operator +(Money a, Money b) => new(a.Amount + b.Amount);
    public static Money operator -(Money a, Money b) => new(a.Amount - b.Amount);
    public static Money operator *(Money a, decimal factor) => new(a.Amount * factor);

    public Money Percent(decimal pct) => new(Amount * pct / 100m);
    public bool IsPositive => Amount > 0m;
    public bool IsNegative => Amount < 0m;
    public bool IsZero     => Amount == 0m;

    public static Money FromDecimal(decimal d) => new(d);
    public override string ToString() => $"₹{Amount:N2}";

    // IEquatable, IComparable, GetHashCode omitted for brevity
}
```

### Usage Scope

**Phase 2 only wraps `SettlementCalculator` and its result DTO.** ViewModels and models remain `decimal`-based (SQLite requires decimal storage). `Money` is used as an in-memory computation type only — converted back to `decimal` before persisting.

> [!NOTE]
> Full replacement of all raw `decimal` usage across 137 files is intentionally deferred. The risk of introducing regressions is too high to do in one pass. Phase 2 is scoped to the calculation engine only.

---

## Phase 3 — Harden `SettlementCalculator` with Audit Trace *(modify 2 files)*

### Problem
- No record of which formula version produced a result
- No human-readable audit explanation ("Driver Share = ₹12,500 × 50% = ₹6,250")
- Immutability of the request is not enforced

### Changes

#### [MODIFY] `Services/SettlementCalculator.cs`

1. **Seal `CalculationRequest`** — add `init`-only setters or use a record
2. **Add `CalculationTrace` output** alongside the Settlement result
3. **Add `CalculatorVersion` constant** — stored in Settlement

```csharp
// Immutable request
public sealed record CalculationRequest(
    DateTime Date,
    Driver Driver,
    Vehicle Vehicle,
    string ShiftType,
    decimal DriverIncomePercent,
    decimal DriverCngPercent,
    IReadOnlyList<PlatformIncome> Incomes,
    IReadOnlyList<SettlementExpense> Expenses
);

// Audit trace
public sealed class CalculationTrace
{
    public List<string> Steps { get; } = new();
    internal void Add(string step) => Steps.Add(step);
}

// In Calculate():
trace.Add($"Total Income  = {incomes} (sum of operator bills)");
trace.Add($"Driver Share  = {totalIncome} × {driverPct}% = {driverShare}");
trace.Add($"Driver Challan = -{driverChallan} (driver-fault deduction)");
trace.Add($"Net Driver Payable = {netDriverPayable}");
```

#### [MODIFY] `Models/Settlement.cs`

Add:
```csharp
/// <summary>Version of SettlementCalculator that produced this record.</summary>
public int CalculatorVersion { get; set; } = 1;
```

Add a **Migration 004** for this column.

### Result

Historical settlements retain `CalculatorVersion = 0` (default from SQLite migration). New settlements get `CalculatorVersion = 1`. Future formula changes increment this version, making it audit-proof.

---

## Phase 4 — Unit Test Project *(new project)*

### New Project: `DriverLedger.Tests`

```
DriverLedger.Tests/
  DriverLedger.Tests.csproj    (xUnit + Moq)
  Domain/
    SettlementCalculatorTests.cs
    MoneyTests.cs
  Infrastructure/
    MigrationRunnerTests.cs
  Security/
    AuthServiceTests.cs
```

### `SettlementCalculatorTests.cs` — Coverage Targets

| Test Class | Scenarios |
|---|---|
| `BasicCalculation` | Standard 50/50 split |
| `DriverChallanDeduction` | Challan correctly reduces driver haq |
| `CngSplitVariations` | 60/40, 100/0, 0/100 CNG splits |
| `CashCollectedAboveBill` | Validation exception expected |
| `ZeroIncome` | Validation exception expected |
| `NegativeExpense` | Validation exception expected |
| `HighValuePrecision` | ₹99,999.99 — verify 2dp rounding |
| `MultiPlatformIncome` | Ola + Rapido + Uber summed correctly |
| `SelfDrivenDriver` | DriverIncomePercent = 100 → driver gets all |
| `OwnerChallanClassification` | OwnerChallan goes to TotalOwnerExpenses |
| `FormulaTrace` | CalculationTrace contains all steps |

### `MoneyTests.cs`

- Addition, subtraction, multiplication precision
- MidpointRounding.AwayFromZero (e.g. ₹0.005 → ₹0.01 not ₹0.00)
- Equality + comparison operators
- Zero identity

### `MigrationRunnerTests.cs`

- Migration 001 is idempotent (run twice → no error)
- Migration 002 adds columns to existing table
- Pending migrations run in correct version order
- Failed migration does not advance version

### `AuthServiceTests.cs`

- Valid PIN verifies correctly
- Wrong PIN fails
- BCrypt hash is never stored in plain form

---

## Phase 5 — SQLite Performance & Security Hardening *(modify 2 files)*

### 5A — SQLite Pragma Hardening

#### [MODIFY] `DatabaseService.InitializeAsync()`

Add after opening connection:

```csharp
// PRAGMA WAL mode — safe concurrent reads during write
await _connection.ExecuteAsync("PRAGMA journal_mode=WAL;");

// Enforce FK integrity at the SQLite level
await _connection.ExecuteAsync("PRAGMA foreign_keys=ON;");

// Faster sync (safe for mobile — OS flushes before app suspend)
await _connection.ExecuteAsync("PRAGMA synchronous=NORMAL;");
```

> [!NOTE]
> WAL mode is compatible with existing databases and is transparent to sqlite-net-pcl. The existing WAL checkpoint in BackupService already handles this correctly.

### 5B — Auth Security Hardening

#### Current: `BCrypt.Net-Next 4.1.0`

BCrypt is adequate for now. The request to replace with PBKDF2/Argon2id is valid long-term but carries **breaking change risk** for existing users (their stored PIN hash format changes).

**Safe approach:**
1. Keep BCrypt for existing users
2. Add `AuthVersion` field to `Company` table (Migration 005)
3. On next successful login, re-hash with PBKDF2 and upgrade `AuthVersion`
4. This is a transparent, zero-downtime migration

#### Add failed-attempt throttling

#### [MODIFY] `Services/AuthService.cs`

```csharp
private int _failedAttempts;
private DateTime? _lockedUntil;
private const int MaxAttempts = 5;
private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(5);

public bool IsLockedOut => _lockedUntil.HasValue && DateTime.UtcNow < _lockedUntil.Value;

public bool VerifyPin(string pin, string storedHash)
{
    if (IsLockedOut) return false;

    bool result = BCrypt.Net.BCrypt.Verify(pin, storedHash);
    if (!result)
    {
        _failedAttempts++;
        if (_failedAttempts >= MaxAttempts)
            _lockedUntil = DateTime.UtcNow.Add(LockDuration);
    }
    else
    {
        _failedAttempts = 0;
        _lockedUntil = null;
    }
    return result;
}
```

> [!IMPORTANT]
> `_failedAttempts` is in-memory only (resets on app kill). This is intentional for an offline app — persistent lockout would risk permanent lockout with no recovery path. The 5-attempt window per session is sufficient for the threat model (shared-device casual misuse).

### 5C — Indexes

Created in **Migration 003** (Phase 1). No additional code needed.

---

## Phase 6 — Domain Layer Cleanup *(modify 3 ViewModels)*

### 6A — Split `DashboardViewModel` (394 lines)

`DashboardViewModel` currently acts as a data coordinator but it's too large.

**Extract to:**
- `DashboardFleetSectionViewModel` — fleet stats properties
- `DashboardFinanceSectionViewModel` — revenue, CNG, owner profit
- `DashboardPerformanceSectionViewModel` — vehicle/driver rankings

These are `ObservableObject`-based sub-ViewModels bound to separate XAML sections. The parent `DashboardViewModel` becomes a coordinator that calls `LoadAsync` on each.

> [!NOTE]
> This is a UI refactor only — no business logic changes. The `IDashboardSummaryService` contract remains unchanged.

### 6B — Move `SettlementEntryViewModel` business logic to `SettlementCalculator`

`BuildCalculationRequest()` already exists. The duplicate-check logic in `SaveNewAsync` / `SaveEditAsync` should move to a `SettlementDomainService`:

```csharp
public class SettlementDomainService
{
    public async Task<bool> IsDuplicateAsync(
        ISettlementRepository repo,
        DateTime date, int driverId, int vehicleId, string shift, int excludeId = 0)
    {
        var existing = await repo.GetSettlementsByDateAsync(date);
        return existing.Any(e =>
            e.DriverId  == driverId  &&
            e.VehicleId == vehicleId &&
            e.ShiftType == shift     &&
            e.Id        != excludeId);
    }
}
```

### 6C — Add `AppConstants` expansion

#### [MODIFY] `Helpers/AppConstants.cs`

Replace the single-constant file with grouped constants:

```csharp
public static class AppConstants
{
    public const string DatabaseName    = "driverledger.db";
    public const int    SchemaVersion   = 3;      // current DB version
    public const int    CalculatorVersion = 1;    // settlement formula version
    public const int    MaxLoginAttempts = 5;
    public static readonly TimeSpan LoginLockDuration = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan AutoBackupInterval = TimeSpan.FromHours(24);
    public const string BackupFilePrefix = "DriverLedger_backup_";
    public const string BackupFileExt    = ".db";
}
```

---

## Phase 7 — Audit & History System *(new table + service)*

### New Table: `AuditLog`

```csharp
[Table("AuditLog")]
public class AuditLogEntry
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string EntityType { get; set; } = string.Empty;  // "Settlement", "DriverLedger", etc.
    public int EntityId { get; set; }
    public string Action { get; set; } = string.Empty;       // "Create", "Edit", "Delete", "Restore"
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
}
```

### New Service: `IAuditService` / `AuditService`

```csharp
public interface IAuditService
{
    Task LogAsync(string entityType, int entityId, string action,
                  object? before = null, object? after = null, string? notes = null);
    Task<List<AuditLogEntry>> GetLogsForEntityAsync(string entityType, int entityId);
}
```

### Integration Points

- `SaveEditAsync` in `SettlementEntryViewModel` → `_audit.LogAsync("Settlement", id, "Edit", before, after)`
- `OnDeleteAsync` in `SettlementDetailViewModel` → `_audit.LogAsync("Settlement", id, "Delete", settlement)`
- `RestoreAsync` in `BackupService` → `_audit.LogAsync("Database", 0, "Restore", notes: backupPath)`
- `LoginViewModel` → log successful and failed login attempts

### Migration 006: Add `AuditLog` table

> [!IMPORTANT]
> Settlement edits now have a before/after JSON snapshot stored in `AuditLog`. This satisfies the dispute-proof accounting requirement without changing the Settlement table structure.

---

## Phase 8 — Long-Term Maintainability *(documentation + tooling)*

### 8A — Structured Logging Wrapper

Replace all `System.Diagnostics.Debug.WriteLine(...)` calls with a thin `IAppLogger` interface:

```csharp
public interface IAppLogger
{
    void Info(string category, string message);
    void Warning(string category, string message);
    void Error(string category, string message, Exception? ex = null);
}
```

In Debug builds: writes to `Debug.WriteLine`. In Release: writes to a rotating log file in `AppDataDirectory/logs/`. Log file is never sent anywhere (offline-first).

### 8B — Health Diagnostics Page

Add a `DiagnosticsPage` (Settings → Advanced → Diagnostics) that shows:
- DB file size
- DB schema version
- Last backup timestamp
- Migration history
- Pending migrations count
- Auth version (BCrypt vs future PBKDF2)

This page is dev/admin-only but ships in the APK as it contains no sensitive data.

### 8C — Architecture Decision Records

```
Docs/
  ADR/
    ADR-001-offline-first.md
    ADR-002-sqlite-uow-pattern.md
    ADR-003-migration-engine.md
    ADR-004-money-value-object.md
    ADR-005-bcrypt-auth-upgrade-path.md
```

---

## Open Questions for User Review

> [!IMPORTANT]
> **Q1 — Migration 001 on existing databases:**  
> Existing databases have tables created by `CreateTableAsync`. Should Migration 001 check for table existence and auto-mark itself as "applied" if tables already exist? Or should we set the initial SchemaVersion to 1 for all existing DBs on first upgrade launch?  
> **Recommendation:** Auto-detect existing tables; mark Migration 001 as applied if `Settlements` table exists.

> [!IMPORTANT]
> **Q2 — BCrypt → PBKDF2 migration:**  
> Should existing users be silently re-hashed on next successful login (transparent), or should there be an explicit "Upgrade Security" prompt in Settings?  
> **Recommendation:** Silent on-login re-hash. Lower friction, same security outcome.

> [!IMPORTANT]
> **Q3 — `Money` struct scope:**  
> Should `Money` replace ALL `decimal` in models/repos/ViewModels (breaking SQLite serialization), or remain a pure in-memory computation type for the calculator only?  
> **Recommendation:** In-memory only. SQLite stores `REAL` (decimal). Converting the entire codebase to `Money` requires custom SQLite type converters and has high regression risk.

> [!IMPORTANT]
> **Q4 — Dashboard ViewModel split:**  
> Splitting `DashboardViewModel` into sub-ViewModels requires XAML binding path changes (e.g., `{Binding FleetSection.ActiveVehiclesToday}`). How large is the existing XAML? Is this refactor worth the UI binding churn?  
> **Recommendation:** Keep `DashboardViewModel` as a single coordinator VM. Extract business-logic helpers to private methods and a `DashboardSummaryService` (already done). The 394-line size is acceptable for a coordinator.

> [!WARNING]
> **Q5 — Audit Log storage growth:**  
> `AuditLog` with before/after JSON will grow indefinitely. Should there be a retention policy (e.g., purge entries older than 1 year)? Or keep all records forever?  
> **Recommendation:** Keep all audit records. For a taxi ledger, each driver does ≤730 settlements/year. The JSON per record is ~2KB. 5 drivers × 730 × 2KB = 7MB/year. Negligible.

---

## Execution Sequence Summary

| Phase | Description | Risk | Est. Time |
|-------|-------------|------|-----------|
| 0 | Critical bug fixes (5 items) | 🟢 Low | 1–2h |
| 1 | Database migration framework | 🟡 Medium | 4–6h |
| 2 | Money value object | 🟢 Low | 2h |
| 3 | Calculator audit trace + version | 🟢 Low | 2–3h |
| 4 | Unit test project (xUnit) | 🟢 Low | 4–6h |
| 5 | SQLite pragmas + auth throttling | 🟢 Low | 2h |
| 6 | Domain cleanup + AppConstants | 🟡 Medium | 3–4h |
| 7 | Audit log system | 🟡 Medium | 3–4h |
| 8 | Structured logging + diagnostics | 🟢 Low | 3h |
| **Total** | | | **~25–30h** |

> [!TIP]
> Phases 0 → 1 → 4 → 5 give you ~80% of the safety and correctness value for ~40% of the effort. Recommend executing in that order if time is constrained.
