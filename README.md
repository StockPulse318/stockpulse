# StockPulse — Warehouse Inventory Management System (WPF Client)

A fast, responsive, and uncluttered desktop application for warehouse inventory management built with **C#**, **Windows Presentation Foundation (WPF)**, and **.NET 8 (LTS)** based strictly on the Product Requirements Document (PRD).

The application communicates with a hosted REST API backend over HTTPS using JWT Bearer authentication, and never connects to a database directly.

---

## 1. Architectural Design (Three Layers)

The application strictly adheres to the three-layer architecture specified in the PRD, with dependencies pointing strictly downward:

```
WarehouseInventory.sln
│
├── Presentation Layer (WarehouseInventory/Presentation)
│   ├── ViewModels/          MVVM implementation using CommunityToolkit.Mvvm (ObservableObject, RelayCommand)
│   ├── Views/               Clean XAML views with zero networking and no business rules in code-behind
│   ├── Converters/          WPF value converters (BooleanToVisibilityConverter, InverseBooleanConverter)
│   └── Services/            IDialogService & DialogService for modal interactions and window management
│
├── Business Logic Layer (WarehouseInventory/Business)
│   ├── Models/              Domain entities: Product, User, UserRole (WarehouseManager, Clerk)
│   ├── Validation/          Input validation: positive amounts, stock-out limits, non-negative price/reorder, required fields
│   └── Services/            IAuthService & IInventoryService enforcing business logic before API dispatch
│
├── Data Layer (WarehouseInventory/Data)
│   ├── Dtos/                Strongly-typed DTOs mirroring the OpenAPI contract exactly
│   ├── Http/                Typed IStockPulseApiClient communicating via IHttpClientFactory & System.Text.Json
│   └── State/               InMemoryTokenStorage keeping JWT access tokens in memory only (never written to disk)
│
└── Test Layer (WarehouseInventory.Tests)
    ├── Business/            xUnit tests for validation rules, amount checks, low-stock threshold, and role enforcement
    ├── Data/                xUnit tests for StockPulseApiClient using MockHttpMessageHandler (200, 400, 401, 403, network failure)
    └── Presentation/        xUnit tests for MainViewModel (role visibility, pagination calculations, alerts filtering)
```

---

## 2. Roles & Permissions

The system supports two distinct roles defined by the PRD:

| Role | Role Display Name | Inventory Search & Filter | Stock-In / Stock-Out | Add, Edit & Delete Products |
|---|---|:---:|:---:|:---:|
| `WAREHOUSE_MANAGER` | Warehouse Manager | Yes | Yes | **Yes** |
| `CLERK` | Stock Clerk | Yes | Yes | **No** (Controls hidden) |

- **Security Enforcement**: The server enforces all permissions. The client hides or disables management actions purely as a user convenience.
- **Session Expiry**: When an authenticated session expires (HTTP 401), the application clears in-memory credentials and returns the user to the login window with an explanatory notice.

---

## 3. Configuration

The backend base URL is configured in `appsettings.json` using the `ApiBaseUrl` key:

```json
{
  "ApiBaseUrl": "https://stockpulse-backend-production-4c30.up.railway.app"
}
```

- **No Hardcoded URLs or Credentials**: The application reads `ApiBaseUrl` at startup.
- **No Seeding / Fake Data**: The application displays strictly what the remote API returns.

---

## 4. Key Functional Features

1. **Inventory List (Main Screen)**:
   - DataGrid displaying Product ID, Product Name, Category, Quantity, Unit Price, and Reorder Level.
   - Enabled UI virtualization (`VirtualizingStackPanel.VirtualizationMode="Recycling"`) for smooth rendering of large catalogs.
   - Search box with 300 ms debounce and in-flight request cancellation (`CancellationTokenSource`), keeping existing rows visible while new results load.
   - Category filtering dropdown and column header sorting.
   - Paging navigation controls (Previous / Next / Page Info / Page Size).
   - Low-stock warning: Any product whose quantity is $\le$ reorder level (`is_low_stock` flag) is highlighted and marked **"⚠ Restock needed"**.

2. **Stock Operations**:
   - Stock-In and Stock-Out buttons available for the selected product.
   - Focused dialog rejecting zero, negative, and non-integer inputs before any HTTP request is issued.
   - Validates that stock-out quantities do not exceed available inventory, displaying the server's refusal message if rejected.
   - Dynamically refreshes the affected row and updates the low-stock alert counter.

3. **Low-Stock Alerts**:
   - Dedicated Alerts tab displaying only products requiring replenishment, populated from `/api/Products/low-stock`.
   - Real-time badge counter displayed directly on the navigation tab.

4. **Product Management (Warehouse Manager Only)**:
   - Add and Edit dialog with Product Name, Category dropdown, Unit Price, Reorder Level, and Quantity.
   - Category ComboBox populated from available categories (never typed freely).
   - Delete confirmation dialog with server refusal message displayed on rejection.

---

## 5. Prerequisites & Building

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (or .NET 10 SDK with .NET 8 targeting)
- Windows 10/11 x64

### Build the Solution
```powershell
dotnet build WarehouseInventory.sln -c Release /p:TreatWarningsAsErrors=true
```

### Run Unit Tests
```powershell
dotnet test WarehouseInventory.sln -c Release
```
*Current test suite: **38 passed**, 0 failed, 0 skipped.*

### Run the Desktop Application
```powershell
dotnet run --project WarehouseInventory.csproj
```

### Publish Single-File Executable
The application is configured for framework-dependent single-file publishing for `win-x64`:

```powershell
dotnet publish WarehouseInventory.csproj -c Release -r win-x64 --no-self-contained /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true -o ./publish-win
```

- **Output Executable**: `./publish-win/WarehouseInventory.exe`
- **Published Size**: **~1.23 MB (1,294,401 bytes)**.

---

## 6. Backend Mismatches & Assumptions

1. **Categories Endpoint**:
   - The backend OpenAPI specification does not expose `/api/products/categories` (returns HTTP 404).
   - **Resolution**: The client attempts `/api/products/categories` and gracefully falls back to querying the product catalog to extract and sort distinct categories.
2. **Low-Stock Endpoint**:
   - Confirmed present at `GET /api/Products/low-stock` requiring Bearer authentication. Used to populate the Low-Stock Alerts tab and badge count.
3. **Error Payload Formats**:
   - Handled flexibly for both nested format `{ "error": { "code", "message" } }` and flat string format `{ "error": "Invalid username or password." }` or `{ "message": "..." }`.
4. **Scope Exclusions Enforced**:
   - Per the PRD, multiple warehouse/branch selection, barcode scanning, cloud sync, mobile support, advanced reporting/analytics, and user management screens are strictly excluded from the client.
