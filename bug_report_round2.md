# DriverLedger — Bug Report Round 2

> Full audit of files not covered in Round 1. All 75 existing tests pass.
> Bugs are grouped by **severity** (🔴 Critical → 🟡 Medium → 🔵 Low).

---

## 🟠 High Bugs

### BUG2-H1 — `LoginViewModel.OnLoginAsync`: missing `LoginError` on wrong-password branch
**File:** [LoginViewModel.cs](file:///g:/Application%20MAUI/New%20folder/Prototype%20DriverLedger/P1/DriverLedger/ViewModels/LoginViewModel.cs#L162-L167)  
**Lines:** 162–167

```csharp
if (!_authService.VerifyPassword(Password, company.PasswordHash))
{
    Password = string.Empty;
    IncrementFailedAttempts();
    return;   // ← LoginError is already set by IncrementFailedAttempts()
}
```

`IncrementFailedAttempts()` sets `LoginError` correctly. However, the `Password` is cleared **before** `IncrementFailedAttempts()` runs. This is fine on its own, but the `MobileNumber` is NOT cleared — so the user must clear it manually to retry with a different number after a failed attempt. The real bug is that on the **last attempt before lockout**, `IncrementFailedAttempts()` sets `LoginError = string.Empty` and starts the lockout, but the `LockoutMessage` is the only visible feedback. If `LockoutMessage` is not bound in the XAML, the user sees a blank error section.

More critically: `LoginError` is cleared at line 150 (`LoginError = string.Empty`) at the **start** of every login attempt. But after `IncrementFailedAttempts()` sets it, the `finally` block (line 184) calls `ChangeCanExecute()` — which is fine. However, if `IncrementFailedAttempts()` sets `IsLockedOut = true` and the Tick lambda runs between the `return` and `finally`, `LoginError` could be set to an empty string by `OnLoginAsync`'s next invocation through a race. The lockout UI relies entirely on `LockoutMessage` while `LoginError` is blank — but `LockoutMessage` is only updated by the timer tick and is already set synchronously before `_lockoutTimer.Start()`, so this is minimal risk. Still, the blank state on first lockout detection is a UX issue.

**Fix:** Add an explicit `LoginError = ...` in the wrong-password branch for clarity, and set an initial lockout error in `IncrementFailedAttempts` before the timer starts — which actually already happens at line 197 (`LoginError = string.Empty`) and 231 (`LockoutMessage = ...`). So the UX flow is actually correct. **This is downgraded to Low — see BUG2-L1.**

---

### BUG2-H2 — `DriverLedgerListViewModel.OnClearBalanceAsync`: after clearing, `LoadAsync` is called from `finally` even when `IsBusy = true` race occurs
**File:** [DriverLedgerListViewModel.cs](file:///g:/Application%20MAUI/New%20folder/Prototype%20DriverLedger/P1/DriverLedger/ViewModels/DriverLedgerListViewModel.cs#L190-L197)  
**Lines:** 190–197

```csharp
finally
{
    IsBusy = false;
    await LoadAsync();   // ← always called, even if the operation failed
}
```

If `_ledgerRepo.AddLedgerEntryAsync` throws, `IsBusy` is reset to `false` and then `LoadAsync()` is called. The catch block shows an error dialog **before** the finally, so the sequence is: alert shown → finally runs → `LoadAsync()` triggers another full reload. This is actually acceptable. **BUT** the problem is `LoadAsync` itself has the `if (IsBusy) return` guard — so the finally sets `IsBusy = false` first, then calls `LoadAsync`, which will correctly set `IsBusy = true` again. This double-reset path works.

**The real bug**: `LoadAsync()` is called from the `finally` block of `OnClearBalanceAsync` **regardless** of whether the operation succeeded or failed. On failure, the user sees the error dialog AND then the list refreshes. This is fine from a data standpoint (no data changed), but triggers a redundant network/DB round-trip. **Downgraded to Low — see BUG2-L2.**

---

### BUG2-H3 — `SettlementListViewModel.LoadAsync`: loads ALL vehicles and drivers from DB every refresh — O(n) full table scans with no caching
**File:** [SettlementListViewModel.cs](file:///g:/Application%20MAUI/New%20folder/Prototype%20DriverLedger/P1/DriverLedger/ViewModels/SettlementListViewModel.cs#L134-L148)  
**Lines:** 134–148

```csharp
var settlements = await _settlementRepo.GetAllSettlementsAsync();
var vehicles    = await _vehicleRepo.GetAllVehiclesAsync();
var drivers     = await _driverRepo.GetAllDriversAsync();
```

On every `LoadAsync` / `RefreshCommand` call, three separate full-table reads are issued. With a fleet of 10 vehicles and 20 drivers this is fine. But the `Settlement` model already has `VehicleNumberSnapshot` and `DriverNameSnapshot` stored directly on it (written by `SettlementCalculator`). These snapshots are specifically designed so the list page doesn't need to join vehicle/driver tables.

**Fix:** Use `s.VehicleNumberSnapshot` and `s.DriverNameSnapshot` directly, drop the two extra `GetAllAsync` calls. This cuts DB round-trips from 3 to 1 on every page load.

```diff
- var vehicles    = await _vehicleRepo.GetAllVehiclesAsync();
- var drivers     = await _driverRepo.GetAllDriversAsync();
- var vDict = vehicles.ToDictionary(v => v.Id, v => v.VehicleNumber);
- var dDict = drivers.ToDictionary(d => d.Id, d => d.DriverName);
  _allRecords = settlements
      .OrderByDescending(s => s.Date)
      .Select(s => new SettlementRecord
      {
          Settlement    = s,
-         VehicleNumber = vDict.TryGetValue(s.VehicleId, out var vn) ? vn : "—",
-         DriverName    = dDict.TryGetValue(s.DriverId,  out var dn) ? dn : "—"
+         VehicleNumber = string.IsNullOrEmpty(s.VehicleNumberSnapshot) ? "—" : s.VehicleNumberSnapshot,
+         DriverName    = string.IsNullOrEmpty(s.DriverNameSnapshot)    ? "—" : s.DriverNameSnapshot
      }).ToList();
```

---

### BUG2-H4 — `SettlementCalculatorTests`: `OwnerChallan` test helper uses `ExpenseType.Other` — after the H4 fix it no longer matches the calculator's `OwnerChallan` enum branch
**File:** [SettlementCalculatorTests.cs](file:///g:/Application%20MAUI/New%20folder/Prototype%20DriverLedger/P1/DriverLedger.Tests/SettlementCalculatorTests.cs#L57-L60)  
**Lines:** 57–60

```csharp
private static SettlementExpense OwnerChallan(decimal amount) => new()
{
    Type = ExpenseType.Other, Amount = amount, Name = "OwnerChallan"  // ← stale after H4 fix
};
```

The H4 fix in Round 1 changed `SettlementEntryViewModel` to use `ExpenseType.OwnerChallan`. The test helper still uses `ExpenseType.Other, Name = "OwnerChallan"`. The test at line 164 (`Calculate_OwnerChallan_AddsToOwnerExpensesNotDriverChallan`) **still passes** because the calculator's fallback filter `(e.Type == ExpenseType.Other && e.Name != "DriverChallan")` catches it. But the test no longer exercises the primary `OwnerChallan` enum branch — the intent of the fix is untested.

**Fix:** Update the test helper to use `ExpenseType.OwnerChallan` and add a second test case verifying the `Other+Name="OwnerChallan"` backward-compat path:

```diff
- Type = ExpenseType.Other, Amount = amount, Name = "OwnerChallan"
+ Type = ExpenseType.OwnerChallan, Amount = amount, Name = "OwnerChallan"
```

---

## 🟡 Medium Bugs

### BUG2-M1 — `MigrationRunner.RunPendingMigrations`: if a migration throws, the `SchemaVersion` row for previously-successful migrations (run in the same call) is NOT rolled back — but subsequent restarts will skip them correctly
**File:** [MigrationRunner.cs](file:///g:/Application%20MAUI/New%20folder/Prototype%20DriverLedger/P1/DriverLedger/Database/Migrations/MigrationRunner.cs#L84-L95)  
**Lines:** 84–95

Each migration runs in its own `RunInTransaction`. So if V1 succeeds and V2 fails, V1's `SchemaVersion` row is committed and V2 is rolled back. This is the correct behavior — and the test at `MigrationRunnerTests.RunPendingMigrations_FailingMigration_DoesNotInsertSchemaVersionRow` validates exactly this.

However: `MigrationRunner` re-throws the exception from V2 **without** identifying which migration failed. Startup code in `DatabaseService` catches this with a generic `[DatabaseService] Init error: {ex.Message}` log. There is no way from the log to know it was V2 that failed vs V99 that failed.

**Fix:** Wrap the re-throw to include migration context:
```csharp
catch (Exception ex)
{
    throw new InvalidOperationException(
        $"Migration v{migration.Version} ({migration.Name}) failed: {ex.Message}", ex);
}
```

---

### BUG2-M2 — `DriverLedgerListViewModel.RecalculateTotals`: totals are computed from the **filtered** list (`DriverSummaries`) after `ApplyFilter()`, but `ApplyFilter()` is called **before** `RecalculateTotals()` in `LoadAsync` — and `RecalculateTotals()` is never called after `ApplyFilter` runs on a search change
**File:** [DriverLedgerListViewModel.cs](file:///g:/Application%20MAUI/New%20folder/Prototype%20DriverLedger/P1/DriverLedger/ViewModels/DriverLedgerListViewModel.cs#L141-L142)  
**Lines:** 141–142 and 199–210

```csharp
ApplyFilter();
RecalculateTotals();  // ← called after first load
```

But when `SearchText` changes, only `ApplyFilter()` is called (line 30 setter). `RecalculateTotals()` is **not** called. So the wallet totals (`TotalOwnerOwes`, `TotalOwnerToGet`, `PendingCount`) become stale when the user types in the search box.

**Fix:** Call `RecalculateTotals()` at the end of `ApplyFilter()`:
```diff
  DriverSummaries.Clear();
  foreach (var s in filtered) DriverSummaries.Add(s);
+ RecalculateTotals();
```
And remove the explicit `RecalculateTotals()` call from `LoadAsync`.

---

### BUG2-M3 — `AuditServiceTests.LogAsync_MultipleConcurrentCalls_AllPersist`: concurrent `Task.WhenAll` on a SQLite sync connection — will hit `SQLITE_BUSY`
**File:** [AuditServiceTests.cs](file:///g:/Application%20MAUI/New%20folder/Prototype%20DriverLedger/P1/DriverLedger.Tests/AuditServiceTests.cs#L197-L206)  
**Lines:** 197–206

```csharp
var tasks = Enumerable.Range(1, count).Select(i =>
    _sut.LogAsync(...));
await Task.WhenAll(tasks);
```

`AuditService.LogAsync` wraps writes in `Task.Run(() => conn.Insert(...))`. With 5 tasks launched concurrently against the same `SQLiteConnection`, the synchronous connection object is not thread-safe — two simultaneous `conn.Insert` calls can collide with `SQLITE_BUSY`. In test environments, this usually passes because 5 tasks are small enough to serialize naturally on the default thread pool. But it is a flaky test that can fail under load or on slower CI machines.

The fact that all 75 tests pass today doesn't mean this test is correct — it's inherently non-deterministic.

**Fix:** The test should either serialize calls with `await` loops, or the `AuditService` implementation should use a dedicated `SemaphoreSlim(1,1)` around writes. Since `AuditService` is a singleton, adding a write lock is the production-correct fix.

---

### BUG2-M4 — `Migration_006_AddAuditLog`: `CreateIndexIfNotExists` uses `DESC` in the `columns` argument — but SQLite does NOT support DESC in `CREATE INDEX` without enabling the `SQLITE_ENABLE_ORDERED_QUERIES` compile flag
**File:** [Migration_006_AddAuditLog.cs](file:///g:/Application%20MAUI/New%20folder/Prototype%20DriverLedger/P1/DriverLedger/Database/Migrations/Migration_006_AddAuditLog.cs#L43-L57)  
**Lines:** 43–57

```csharp
CreateIndexIfNotExists(conn,
    indexName: "idx_audit_driver_time",
    table:     "AuditLog",
    columns:   "DriverId, Timestamp DESC");  // ← DESC in index definition
```

SQLite supports `DESC` in index column definitions since v3.3 — and Android ships SQLite ≥ 3.39, so this won't crash. However, the index `idx_audit_driver_time` with `DESC` is subtly problematic: queries using `ORDER BY Timestamp ASC` won't use this index efficiently (they must do a full scan). The migration comment says "ordered by Timestamp DESC" which is the intended query pattern, so the `DESC` is intentional — but `CreateIndexIfNotExists` in `MigrationBase` constructs the SQL as:

```sql
CREATE INDEX IF NOT EXISTS idx_audit_driver_time ON AuditLog (DriverId, Timestamp DESC)
```

Verify that `MigrationBase.CreateIndexIfNotExists` passes `columns` verbatim (it does — line 67 of MigrationBase). This is fine in SQLite 3.3+. **Downgraded to Low — see BUG2-L3.**

---

## 🔵 Low / Code-Quality Bugs

### BUG2-L1 — `LoginViewModel`: `_lockoutTimer` is never stopped/disposed when the ViewModel is garbage collected — timer tick holds a closure reference, preventing GC
**File:** [LoginViewModel.cs](file:///g:/Application%20MAUI/New%20folder/Prototype%20DriverLedger/P1/DriverLedger/ViewModels/LoginViewModel.cs#L209-L232)  
**Lines:** 209–232

The `_lockoutTimer` Tick lambda captures `this` (via `_lockoutUntil`, `IsLockedOut`, `LockoutMessage`, `LoginCommand`). If the user navigates away from the LoginPage before the lockout expires, the timer continues firing and holds the ViewModel alive. `LoginViewModel` should implement `IDisposable` and stop the timer in `Dispose()`.

---

### BUG2-L2 — `DriverLedgerListViewModel.OnClearBalanceAsync`: `LoadAsync()` in `finally` is called even on exception, causing a redundant DB round-trip after an error dialog
**File:** [DriverLedgerListViewModel.cs](file:///g:/Application%20MAUI/New%20folder/Prototype%20DriverLedger/P1/DriverLedger/ViewModels/DriverLedgerListViewModel.cs#L191-L196)  
**Lines:** 191–196

Should move `LoadAsync()` to the `try` block after the successful `AddLedgerEntryAsync`, not unconditionally in `finally`.

---

### BUG2-L3 — `MigrationRunnerTests.RunPendingMigrations_MigrationsRunInAscendingVersionOrder`: orders by `AppliedAt` to verify sequence — fragile if two migrations are applied within the same millisecond
**File:** [MigrationRunnerTests.cs](file:///g:/Application%20MAUI/New%20folder/Prototype%20DriverLedger/P1/DriverLedger.Tests/MigrationRunnerTests.cs#L134-L136)  
**Lines:** 134–136

```csharp
var rows = _conn.Table<SchemaVersion>().OrderBy(r => r.AppliedAt).ToList();
rows[0].Version.Should().Be(1, "V1 must be applied before V2 ...");
```

`AppliedAt = DateTime.UtcNow` is set at the time of the INSERT. If V1 and V2 are applied within the same `DateTime` tick (common on fast CI machines where `UtcNow` has limited precision), both rows get the same `AppliedAt` and the `OrderBy` is non-deterministic.

**Fix:** Order by `Version` (which is the canonical ordering) rather than `AppliedAt`:
```diff
- var rows = _conn.Table<SchemaVersion>().OrderBy(r => r.AppliedAt).ToList();
+ var rows = _conn.Table<SchemaVersion>().OrderBy(r => r.Version).ToList();
```

---

### BUG2-L4 — `AuditLog` model has `[Indexed]` on individual columns but Migration_006 creates composite indexes with the same columns — resulting in **redundant single-column indexes**
**File:** [AuditLog.cs](file:///g:/Application%20MAUI/New%20folder/Prototype%20DriverLedger/P1/DriverLedger/Models/AuditLog.cs#L26-L44)  
**Lines:** 26–44

```csharp
[Indexed] public DateTime Timestamp { get; set; }
[Indexed] public string EntityType  { get; set; }
[Indexed] public int    EntityId    { get; set; }
[Indexed] public int    DriverId    { get; set; }
```

`Migration_001_InitialSchema` calls `conn.CreateTable<AuditLog>()` — but `AuditLog` is created in `Migration_006`, not 001. When `Migration_006` runs `conn.Execute("CREATE TABLE IF NOT EXISTS AuditLog ...")`, it creates the table without the sqlite-net `[Indexed]` attributes (they are only applied by `CreateTable<T>()`). Then `Migration_006` creates 3 composite indexes manually. The `[Indexed]` attributes on the model are therefore **never applied** — they would only take effect if someone called `conn.CreateTable<AuditLog>()`, which never happens after 006 runs the raw DDL.

This is not a data bug (the composite indexes cover all queries), but the `[Indexed]` attributes are misleading documentation: they imply 4 individual indexes exist, when actually only 3 composite indexes do.

**Fix:** Remove the `[Indexed]` attributes from `AuditLog` fields, or document why they are intentionally unused.

---

### BUG2-L5 — `SettlementListViewModel` has `_vehicleRepo` and `_driverRepo` injected and stored but (after BUG2-H3 fix) they become unused — dead DI dependencies
**File:** [SettlementListViewModel.cs](file:///g:/Application%20MAUI/New%20folder/Prototype%20DriverLedger/P1/DriverLedger/ViewModels/SettlementListViewModel.cs#L13-L16)  
**Lines:** 13–16

These repos should be removed from the constructor after applying the BUG2-H3 fix (using snapshot fields instead). Keeping them means the DI container must resolve them unnecessarily on every navigation to the settlement list page.

---

## Summary Table

| ID | Severity | File | Description |
|----|----------|------|-------------|
| H3 | 🟠 High | `SettlementListViewModel` | Loads ALL vehicles + drivers on every refresh when snapshots exist |
| H4 | 🟠 High | `SettlementCalculatorTests` | OwnerChallan test helper still uses `ExpenseType.Other` — doesn't test H4 fix |
| M1 | 🟡 Medium | `MigrationRunner` | Failed migration re-throw loses context of which migration failed |
| M2 | 🟡 Medium | `DriverLedgerListViewModel` | Wallet totals go stale when search text changes — `RecalculateTotals` not called from `ApplyFilter` |
| M3 | 🟡 Medium | `AuditServiceTests` | Concurrent `Task.WhenAll` against non-thread-safe `SQLiteConnection` — flaky test |
| L1 | 🔵 Low | `LoginViewModel` | `_lockoutTimer` not stopped on VM disposal — memory/timer leak |
| L2 | 🔵 Low | `DriverLedgerListViewModel` | Redundant `LoadAsync()` in `finally` after failed clear operation |
| L3 | 🔵 Low | `MigrationRunnerTests` | Order-by `AppliedAt` is non-deterministic — should order by `Version` |
| L4 | 🔵 Low | `AuditLog` model | `[Indexed]` attributes are never applied (table created via raw DDL in M006) |
| L5 | 🔵 Low | `SettlementListViewModel` | Dead DI dependencies after H3 fix |
