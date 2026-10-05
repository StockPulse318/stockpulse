# StockPulse (WPF Front End)

Front end for the StockPulse Warehouse Inventory Management System (DCIT 318). Built with C# and WPF on .NET 8.

Communicates with the StockPulse REST API backend using HTTP client calls and JWT Bearer token authentication.

---

## Backend Connection

By default, the application connects to the deployed backend server:

```
https://stockpulse-backend-production-4c30.up.railway.app
```

### Changing the Backend Server

The application supports switching to any backend host (Railway, Render, local dev, etc.):

1. **Via `appsettings.json`**:
   ```json
   {
     "Backend": {
       "BaseUrl": "https://stockpulse-backend-production-4c30.up.railway.app"
     }
   }
   ```
2. **Via Environment Variable**:
   Set `STOCKPULSE_API_URL` or `Backend__BaseUrl` (e.g. `http://localhost:5000` or custom server).
3. **Via the UI at Login**:
   Expand the **Server Settings** drawer on the Login window to view or edit the backend server URL at runtime.

---

## Authentication & Authorization

All protected backend endpoints require a JWT Bearer token:
- When a user logs in via `POST /api/auth/login`, the backend issues a signed JWT token.
- The desktop client stores this token in `StockPulseApiClient` and automatically attaches it via `Authorization: Bearer <token>` to all subsequent requests (`/api/products`, `/api/products/{id}/stock/in`, etc.).
- When logging out, the session and Bearer token are cleared.
- Roles supported:
  - **Warehouse Manager**: Full access (add, edit, delete products, stock movements).
  - **Stock Clerk**: Search products and move stock in/out.

---

## Deploying Frontend on Render

This repository includes a `Dockerfile` and `nginx.conf` to deploy a live web distribution portal on Render:

1. Create a **New Web Service** on Render and connect this repository.
2. Select **Docker** as the Runtime.
3. Render will automatically build the `Dockerfile`, spin up Nginx on port `10000`, and serve the web download page where users and graders can download the pre-configured Windows app.

---

## How to Run Locally

1. Ensure the .NET 8 SDK is installed.
2. Run from the terminal:
   ```bash
   dotnet run
   ```
   Or open `WarehouseInventory.csproj` in Visual Studio 2022 and press **F5**.

---

## Project Structure

- `Models/`: `Product`, `User`, `UserRole`.
- `Services/`:
  - `StockPulseConfig`: Manages configuration loading (`appsettings.json`, environment variables).
  - `StockPulseApiClient`: Handles HTTP requests, JWT Bearer tokens, response mapping, and error translation.
  - `AuthService`: Implementation of `IAuthService` backed by the REST API.
  - `InventoryService`: Implementation of `IInventoryService` backed by the REST API.
  - `InventoryException`: Business and server error exceptions displayed to the user.
- `ViewModels/`: `ProductRow` for formatting DataGrid rows, low-stock indicators, and threshold ticks.
- `Views/`:
  - `LoginWindow`: Login screen with credentials and expandable Server Settings.
  - `MainWindow`: Inventory dashboard, search, filter, stock in/out, add/edit/delete product dialogs.
  - `ProductDialog`: Add and Edit product dialog with server-assigned IDs.
  - `StockDialog`: Stock-In and Stock-Out dialog with live validation.
