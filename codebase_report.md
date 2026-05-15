# DriverLedger — Codebase Report
**Generated:** 2026-05-07  
**Version:** 1.2.0 (build 10) · `com.sabiyaco.driverledger`  
**Framework:** .NET 10 MAUI · Target: `net10.0-android` (Android 5.0+ / API 21+)

---

## 1. At a Glance

| Metric | Value |
|--------|-------|
| Total source files | **137** (111 C# + 26 XAML) |
| Total lines of code | **12,749** |
| NuGet dependencies | **5** |
| App version | 1.2.0 (build 10) |
| Offline storage | SQLite (sqlite-net-pcl 1.9.172) |
| Auth | BCrypt.Net-Next 4.1.0 |
| PDF export | QuestPDF 2026.2.4 |
| Pattern | MVVM + Repository + Unit of Work + DI |

---

## 2. Architecture Overview

```
┌──────────────────────────────────────────────────────────┐
│                        Views (XAML)                       │
│  20 Pages — Shell navigation, data-bound to ViewModels    │
└───────────────────────┬──────────────────────────────────┘
                        │ BindingContext
┌───────────────────────▼──────────────────────────────────┐
│                    ViewModels (20)                         │
│  BaseViewModel → INotifyPropertyChanged + IsBusy/IsNotBusy│
│  Commands via ICommand / Command<T>                        │
└──────┬────────────┬──────────────────────┬───────────────┘
       │            │                      │
  INavigationService  IDialogService   Services layer
       │            │                      │
┌──────▼────────────▼──────────────────────▼───────────────┐
│                     Services (24)                          │
│  SettlementCalculator · DashboardSummaryService           │
│  ExportOrchestrator · PdfService · CsvExportService       │
│  DatabaseBackupService · SessionService · AuthService      │
│  IncomeEngine · ExpenseEngine · SettlementEngine          │
└──────────────────────────┬───────────────────────────────┘
                           │
┌──────────────────────────▼───────────────────────────────┐
│               Repositories (14 files, 8 interfaces)        │
│  IUnitOfWork / SqliteUnitOfWork (transactional writes)    │
│  Settlement · DriverLedger · Vehicle · Driver · Company   │
│  VehicleDriver (assignment)                                │
└──────────────────────────┬───────────────────────────────┘
                           │
┌──────────────────────────▼───────────────────────────────┐
│             DatabaseService (SQLiteAsyncConnection)        │
│  Semaphore-locked lazy init · CloseAsync for backup       │
│  GetRawConnectionAsync → sync conn for RunInTransaction   │
└──────────────────────────┬───────────────────────────────┘
                           │
                    SQLite file on device
              FileSystem.AppDataDirectory/driverledger.db
```

---

## 3. Layer Breakdown

| Layer | Files | Lines | Notes |
|-------|------:|------:|-------|
| **ViewModels** | 20 | 4,085 | Largest layer — DashboardViewModel alone is 394 lines |
| **Services** | 24 | 1,796 | Includes engines, export, backup, auth, dashboard |
| **Repositories** | 14 | 746 | 8 interfaces + 6 concrete implementations |
| **Helpers** | 5 | 323 | Converters, BarChartDrawable, ThemeService, AppConstants |
| **Views (.cs code-behind)** | 20 | 452 | Mostly thin — lifecycle hooks only |
| **DTOs** | 6 | 304 | Flat display records used by ViewModels |
| **Models** | 11 | 397 | SQLite entity classes |
| **Database** | 1 | 124 | Single DatabaseService singleton |
| **XAML** | 26 | 4,123 | Rich UI with gradients, animations, custom drawables |

---

## 4. Data Model (SQLite Tables)

```
Company          ← single-row: CompanyName, OwnerName, PIN (BCrypt hashed)
│
Driver           ← DriverName, DriverIncomePercent, DriverCngPercent, DriverType
│
Vehicle          ← VehicleNumber, IsActive
│
VehicleDriver    ← FK Driver+Vehicle+ShiftType (Day/Night) — one-driver-per-shift rule
│
Settlement       ← Date(date-only), ShiftType, DriverId, VehicleId
│                   TotalIncome, TotalCashCollected, DriverShare
│                   OwnerCngShare, DriverCngShare, DriverChallanTotal
│                   TotalOwnerExpenses, NetDriverPayable
│                   DriverNameSnapshot, VehicleNumberSnapshot (audit trail)
│                   CreatedAt, IsDeleted
│
PlatformIncome   ← FK SettlementId, PlatformName (Ola/Rapido/…), OperatorBill, CashCollected
│
SettlementExpense← FK SettlementId, Type (CNG/Toll/Parking/Other), Amount, Name
│
DriverLedger     ← FK DriverId, SettlementId, Date, TransactionType
                    Debit (owner→driver), Credit (driver→owner), Balance (running)
                    ShiftType, Description, CreatedAt
```

**Transaction types:** `Settlement` · `Advance` · `Payment` · `Clearance`

---

## 5. Settlement Finance Formula

The core accounting formula (implemented in `SettlementCalculator.cs`):

```
TotalIncome        = Σ(OperatorBill across all platforms)
TotalCashCollected = Σ(CashCollected across all platforms)
DriverShare        = TotalIncome × DriverIncomePercent%
OwnerCngShare      = TotalCNG × OwnerCngPercent%
DriverCngShare     = TotalCNG × DriverCngPercent%
DriverChallanTotal = Σ(expenses where Name="DriverChallan")
TotalOwnerExpenses = Σ(Toll + Parking + OwnerChallan + Other expenses)

NetDriverPayable = DriverShare
                 - DriverChallanTotal    ← driver-fault challan reduces haq
                 - TotalCashCollected    ← cash already with driver
                 + OwnerCngShare         ← owner's fuel debt to settle
                 + TotalOwnerExpenses    ← owner expenses driver fronted

(+) → Owner must pay driver
(−) → Driver must pay owner
```

---

## 6. Feature Inventory

### Auth & Onboarding
- `SplashPage` → `StartupService.RunAsync()` → routes to Login or Dashboard
- BCrypt PIN-based login per company profile (`AuthService`)
- Forgot-password flow (security question)
- Company creation wizard (`CompanyCreationViewModel`)

### Fleet Management
- Driver CRUD (`AddDriverViewModel`) — income %, CNG %, type
- Vehicle CRUD (`AddVehicleViewModel`) — active/inactive flag
- Driver-Vehicle assignment per shift (`AssignDriverViewModel`) — enforces one-driver-per-shift

### Settlement Entry
- Dynamic multi-operator rows (Ola, Rapido, Uber, Other)
- Bill + Cash per operator, auto-summed
- Section ③ Fuel & Challans (CNG, Driver Challan)
- Section ④ Owner Expenses expandable (Parking, Toll, Repair, Misc, Owner Challan)
- Live recalculation result card (Driver income share, CNG share, challan, Net Haq, Cash direction)
- Edit mode via `?editId=X` QueryProperty — full field restoration
- Duplicate prevention (Date + Driver + Vehicle + **ShiftType**)
- New / Edit / Delete with atomic `SqliteUnitOfWork` transaction + ledger rebalance

### Settlement List & Detail
- Searchable list (vehicle, driver, date, shift)
- Summary totals row filtered to visible set
- `SettlementDetailViewModel` — full breakdown with PDF export button
- `SettlementDetailPage` — read-only receipt view

### Driver Ledger
- Per-driver running balance (Debit/Credit/Balance columns)
- Add Advance (`AddAdvanceViewModel`)
- Receive Payment (`ReceivePaymentViewModel`)
- Export per-driver ledger as CSV

### Dashboard
- Daily fleet summary (active vehicles, trips, income, CNG, profit)
- Monthly summary cards
- Last 5 settlements feed
- Ledger balance overview (owner-to-get vs owner-owes)
- Top/bottom vehicle by earnings, top driver by earnings and trips
- Pull-to-refresh (`RefreshView`)
- Export daily summary as CSV

### Analytics
- 7-day bar charts: Operator Bills, Owner CNG Share, Owner Net Profit
- Weekly summary totals + average daily income (operating days only)
- Custom `BarChartDrawable` (IDrawable via GraphicsView)

### Settings
- Dark/light theme toggle (`ThemeService`)
- Manual backup → WAL checkpoint → file copy → filenames with timestamp
- Backup listing + restore (closes DB, overwrites, restart prompt)
- Auto-backup on launch if > 24h since last backup (`AutoBackupIfNeededAsync`)
- Logout + session clear

### Export
- PDF receipt per settlement (`PdfService` via QuestPDF)
- CSV: all settlements, per-driver ledger
- `ExportOrchestrator` → generates file → `Share.Default.RequestAsync` (OS share sheet)

---

## 7. Dependency Injection Registration Summary

All registrations in `MauiProgram.cs`:

| Lifetime | Type | Count |
|----------|------|------:|
| **Singleton** | Infrastructure (DB, Repos, UoW, Services) | 22 |
| **Transient** | ViewModels + Pages | 44 |

> `SettlementEntryViewModel` uses a manual factory lambda to satisfy its `Dependencies` inner class (>7 params workaround).

---

## 8. Known Technical Debt & Open Issues

### 🔴 Schema migration risk
`DatabaseService.InitializeAsync()` calls `CreateTableAsync<T>()` for every table on every launch. This is safe for **new** columns added with nullable/default values, but **dropping or renaming** a column requires a manual migration — there is currently **no migration framework**.

### 🟡 AnalyticsViewModel — date comparison uses `.ToLocalTime()` on stored dates
Settlements are now stored with `Date = request.Date.Date` (time-stripped, no UTC offset). The analytics filter `s.Date.ToLocalTime().Date == day.Date` will call `ToLocalTime()` on a midnight-local `DateTime` which is correct, but only because the date-strip fix was applied. If any **older records** in the DB were stored with UTC time-of-day, those records could still appear on the wrong day in the 7-day chart.

### 🟡 `DashboardSummaryService` — `RecentSettlementRow` vs `RecentSettlementItem`
The service returns `RecentSettlementRow` (an inner DTO) but `DashboardViewModel` re-maps it manually to `RecentSettlementItem`. These two types carry identical fields — a refactor to use a single shared type would remove the mapping boilerplate.

### 🟡 `ExportOrchestrator.ExportDriverLedgerCsvAsync` — manual `RunningBalance` calculation
Line 133: `RunningBalance = e.Debit - e.Credit` is recalculated per-row instead of using the `Balance` field stored on `DriverLedgerEntry` (which `RebalanceInTransaction` maintains). Should use `e.Balance`.

### 🟡 `DatabaseBackupService.BackupAsync` — sync GetRawConnectionAsync inside Task.Run
Line 63 uses `.GetAwaiter().GetResult()` inside `Task.Run()` to get the raw connection for the WAL checkpoint. This works but is an anti-pattern (sync-over-async). Should be refactored to `await _db.GetRawConnectionAsync()` outside the `Task.Run`.

### 🟡 `SettlementDetailViewModel` — needs review
At 17,761 bytes / not read in this audit pass. Likely carries the same result-card display properties that were missing from `SettlementEntryViewModel` (BUG-1). Recommend verifying it uses `Settlement.DriverCngShare` and `Settlement.DriverChallanTotal` (the new fields added today).

### 🟢 No unit tests
There are zero test projects. `SettlementCalculator` is the most testable surface — all pure math. Recommend adding at minimum a `DriverLedger.Tests` xUnit project covering the calculator formula.

### 🟢 `ShiftTypes.All` not visible in codebase scan
`ShiftOptions => ShiftTypes.All` is referenced in `SettlementEntryViewModel` but `ShiftTypes` was not found in `SettlementEnums.cs`. Likely defined elsewhere or as a static class — confirm location to avoid silent `NullReferenceException` if the type is moved.

### 🟢 `Helpers/BackupService.cs` vs `Services/BackupService.cs`
There are **two files** named `BackupService.cs` — one in `Helpers/` (4,680 bytes) and one in `Services/` (9,291 bytes). The one in `Services/` is `DatabaseBackupService : IBackupService` (registered in DI). The `Helpers/` file appears to be a legacy duplicate. Should be deleted to avoid confusion.

---

## 9. NuGet Dependency Summary

| Package | Version | Purpose |
|---------|---------|---------|
| `Microsoft.Maui.Controls` | 10.0.60 | MAUI framework |
| `Microsoft.Extensions.Logging.Debug` | 10.0.7 | Debug logging |
| `sqlite-net-pcl` | 1.9.172 | SQLite ORM (async + sync) |
| `SQLitePCLRaw.bundle_green` | 2.1.11 | SQLite native bindings |
| `BCrypt.Net-Next` | 4.1.0 | Password hashing (PIN auth) |
| `QuestPDF` | 2026.2.4 | PDF generation |

---

## 10. Recommended Next Steps

| Priority | Action |
|----------|--------|
| 🔴 High | Audit `SettlementDetailViewModel` — confirm it uses new `DriverCngShare` / `DriverChallanTotal` model fields |
| 🔴 High | Delete `Helpers/BackupService.cs` (legacy duplicate) |
| 🟡 Medium | Fix `ExportOrchestrator` to use `e.Balance` (stored) not `e.Debit - e.Credit` |
| 🟡 Medium | Refactor `RecentSettlementRow` → reuse `RecentSettlementItem` directly |
| 🟡 Medium | Refactor `BackupAsync` WAL checkpoint to avoid sync-over-async pattern |
| 🟢 Low | Add `DriverLedger.Tests` xUnit project — start with `SettlementCalculator` |
| 🟢 Low | Add a lightweight DB migration helper (table version check + ALTER TABLE) |
| 🟢 Low | Locate & confirm `ShiftTypes` class definition |
