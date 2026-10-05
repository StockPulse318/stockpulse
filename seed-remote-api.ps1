<#
.SYNOPSIS
    Seeds an active StockPulse REST API backend (Local or Remote) with multi-branch warehouse products and user accounts.
.EXAMPLE
    # Seed local backend (default port 5000 or 5242):
    .\seed-remote-api.ps1 -Local

    # Seed custom URL with admin credentials:
    .\seed-remote-api.ps1 -BaseUrl "http://localhost:5000" -Username "admin" -Password "Admin@1234"
#>

param(
    [string]$BaseUrl = "",
    [switch]$Local,
    [string]$Username = "admin",
    [string]$Password = "Admin@1234"
)

# Auto-detect target backend URL if not explicitly passed
if ($Local -or [string]::IsNullOrWhiteSpace($BaseUrl)) {
    $localCandidates = @("http://localhost:5000", "http://localhost:5242", "http://127.0.0.1:5000")
    $detectedUrl = $null

    foreach ($candidate in $localCandidates) {
        try {
            $test = Invoke-WebRequest -Uri "$candidate/swagger/v1/swagger.json" -Method Head -TimeoutSec 2 -ErrorAction SilentlyContinue
            if ($test.StatusCode -eq 200) {
                $detectedUrl = $candidate
                break
            }
        } catch { }
    }

    if ($detectedUrl) {
        $BaseUrl = $detectedUrl
        Write-Host "Detected active local StockPulse backend at: $BaseUrl" -ForegroundColor Cyan
    } elseif ($Local) {
        $BaseUrl = "http://localhost:5000"
        Write-Host "Using default local backend URL: $BaseUrl" -ForegroundColor Yellow
    } else {
        $BaseUrl = "https://stockpulse-backend-production-4c30.up.railway.app"
        Write-Host "No local backend found; targeting remote URL: $BaseUrl" -ForegroundColor Cyan
    }
}

$BaseUrl = $BaseUrl.Trim().TrimEnd('/')

Write-Host "Authenticating with $BaseUrl as '$Username'..." -ForegroundColor Cyan

$loginBody = @{
    username = $Username
    password = $Password
} | ConvertTo-Json

try {
    $loginResp = Invoke-RestMethod -Uri "$BaseUrl/api/auth/login" -Method Post -ContentType "application/json" -Body $loginBody
} catch {
    Write-Error "Failed to authenticate. Verify server URL ($BaseUrl) and credentials: $_"
    exit 1
}

$token = $loginResp.token
$role = $loginResp.role
Write-Host "Authentication successful! Role: $role" -ForegroundColor Green

if ($role -ne "Warehouse Manager" -and $role -ne "Administrator") {
    Write-Error "Seeding requires an 'Administrator' or 'Warehouse Manager' account to register products and users. Current account is '$role'."
    exit 1
}

$headers = @{
    Authorization = "Bearer $token"
}

# 1. Seed User Accounts across Branches
Write-Host "`nProvisioning branch accounts..." -ForegroundColor Cyan
$users = @(
    @{ Username = "manager"; Password = "Manager@1234"; Role = "Warehouse Manager"; FullName = "National Operations Lead"; AssignedBranch = "All Branches" },
    @{ Username = "clerk"; Password = "Clerk@1234"; Role = "Stock Clerk"; FullName = "General Floating Clerk"; AssignedBranch = "All Branches" },
    @{ Username = "manager_accra"; Password = "Manager@1234"; Role = "Warehouse Manager"; FullName = "Kwame Mensah"; AssignedBranch = "Accra Central" },
    @{ Username = "manager_kumasi"; Password = "Manager@1234"; Role = "Warehouse Manager"; FullName = "Yaw Frimpong"; AssignedBranch = "Kumasi Depot" },
    @{ Username = "manager_tema"; Password = "Manager@1234"; Role = "Warehouse Manager"; FullName = "Abena Osei"; AssignedBranch = "Tema Harbor" },
    @{ Username = "manager_takoradi"; Password = "Manager@1234"; Role = "Warehouse Manager"; FullName = "Ebenezer Quaye"; AssignedBranch = "Takoradi Logistics" },
    @{ Username = "clerk_accra"; Password = "Clerk@1234"; Role = "Stock Clerk"; FullName = "Emmanuel Addo"; AssignedBranch = "Accra Central" },
    @{ Username = "clerk_kumasi"; Password = "Clerk@1234"; Role = "Stock Clerk"; FullName = "Akosua Serwaa"; AssignedBranch = "Kumasi Depot" },
    @{ Username = "clerk_tema"; Password = "Clerk@1234"; Role = "Stock Clerk"; FullName = "Samuel Annan"; AssignedBranch = "Tema Harbor" },
    @{ Username = "clerk_takoradi"; Password = "Clerk@1234"; Role = "Stock Clerk"; FullName = "Grace Tandoh"; AssignedBranch = "Takoradi Logistics" },
    @{ Username = "kofi_mensah"; Password = "Clerk@1234"; Role = "Stock Clerk"; FullName = "Kofi Mensah Jr."; AssignedBranch = "Accra Central" },
    @{ Username = "ama_boateng"; Password = "Clerk@1234"; Role = "Stock Clerk"; FullName = "Ama Boateng"; AssignedBranch = "Tema Harbor" }
)

foreach ($u in $users) {
    try {
        $body = @{
            username = $u.Username
            password = $u.Password
            role = $u.Role
            fullName = $u.FullName
            assignedBranch = $u.AssignedBranch
        } | ConvertTo-Json
        Invoke-RestMethod -Uri "$BaseUrl/api/auth/users" -Method Post -Headers $headers -ContentType "application/json" -Body $body | Out-Null
        Write-Host "  [+] Created account: $($u.Username) ($($u.Role) - $($u.AssignedBranch))" -ForegroundColor Green
    } catch {
        Write-Host "  [-] User '$($u.Username)' already exists or skipped." -ForegroundColor Yellow
    }
}

# 2. Seed Multi-Branch Products
Write-Host "`nProvisioning multi-branch warehouse inventory..." -ForegroundColor Cyan
$products = @(
    # Accra Central Warehouse
    @{ ProductName = "Portland Cement 50kg Grade 42.5N"; Branch = "Accra Central"; Category = "Building Supplies"; Quantity = 180; UnitPrice = 78.50; ReorderLevel = 50 },
    @{ ProductName = "High-Tensile Iron Rods 12mm x 12m"; Branch = "Accra Central"; Category = "Building Supplies"; Quantity = 45; UnitPrice = 125.00; ReorderLevel = 40 },
    @{ ProductName = "Solid Sandcrete Blocks 5-inch"; Branch = "Accra Central"; Category = "Building Supplies"; Quantity = 500; UnitPrice = 8.50; ReorderLevel = 150 },
    @{ ProductName = "Copper Cable 2.5mm Roll (100m)"; Branch = "Accra Central"; Category = "Electrical & Power"; Quantity = 8; UnitPrice = 280.00; ReorderLevel = 15 },
    @{ ProductName = "Schneider Circuit Breaker 63A Double Pole"; Branch = "Accra Central"; Category = "Electrical & Power"; Quantity = 35; UnitPrice = 45.00; ReorderLevel = 20 },
    @{ ProductName = "Heavy Duty PVC Conduit Pipe 20mm x 3m"; Branch = "Accra Central"; Category = "Electrical & Power"; Quantity = 120; UnitPrice = 16.00; ReorderLevel = 60 },

    # Tema Harbor Depot
    @{ ProductName = "Viro Solid Brass Padlock 70mm"; Branch = "Tema Harbor"; Category = "Hardware & Security"; Quantity = 60; UnitPrice = 65.00; ReorderLevel = 25 },
    @{ ProductName = "Galvanized Steel Wire Rope 10mm"; Branch = "Tema Harbor"; Category = "Heavy Rigging"; Quantity = 12; UnitPrice = 420.00; ReorderLevel = 20 },
    @{ ProductName = "Heavy Duty Steel Toe Work Boots (Size 43)"; Branch = "Tema Harbor"; Category = "Safety Gear"; Quantity = 85; UnitPrice = 195.00; ReorderLevel = 30 },
    @{ ProductName = "High-Visibility Safety Vest with Pockets"; Branch = "Tema Harbor"; Category = "Safety Gear"; Quantity = 150; UnitPrice = 28.00; ReorderLevel = 50 },
    @{ ProductName = "Anti-Corrosive Marine Paint 20L Grey"; Branch = "Tema Harbor"; Category = "Paints & Protective Coatings"; Quantity = 14; UnitPrice = 520.00; ReorderLevel = 20 },
    @{ ProductName = "Heavy Duty Cargo Lashing Belts 5 Ton 9m"; Branch = "Tema Harbor"; Category = "Shipping & Storage"; Quantity = 65; UnitPrice = 95.00; ReorderLevel = 20 },

    # Kumasi Regional Depot
    @{ ProductName = "PVC Pressure Pipe Class E 4in x 6m"; Branch = "Kumasi Depot"; Category = "Plumbing & Drainage"; Quantity = 75; UnitPrice = 58.00; ReorderLevel = 30 },
    @{ ProductName = "Solid Brass Gate Valve 2in Female Thread"; Branch = "Kumasi Depot"; Category = "Plumbing & Drainage"; Quantity = 4; UnitPrice = 85.00; ReorderLevel = 10 },
    @{ ProductName = "Polyethylene Water Tank 1000 Litres"; Branch = "Kumasi Depot"; Category = "Water Storage"; Quantity = 6; UnitPrice = 1250.00; ReorderLevel = 8 },
    @{ ProductName = "Submersible Deep Well Water Pump 1.5HP"; Branch = "Kumasi Depot"; Category = "Pumps & Machinery"; Quantity = 10; UnitPrice = 920.00; ReorderLevel = 5 },
    @{ ProductName = "Aluzinc Corrugated Roofing Sheet 3m x 0.4mm"; Branch = "Kumasi Depot"; Category = "Roofing & Timber"; Quantity = 40; UnitPrice = 160.00; ReorderLevel = 50 },
    @{ ProductName = "Seasoned Hardwood Timber 2x4 12ft"; Branch = "Kumasi Depot"; Category = "Roofing & Timber"; Quantity = 110; UnitPrice = 42.00; ReorderLevel = 40 },

    # Takoradi Logistics Hub
    @{ ProductName = "Hydraulic Bottle Jack 20 Ton Industrial"; Branch = "Takoradi Logistics"; Category = "Heavy Equipment"; Quantity = 5; UnitPrice = 650.00; ReorderLevel = 5 },
    @{ ProductName = "Industrial Ratchet Tie-Down Straps 50mm x 10m"; Branch = "Takoradi Logistics"; Category = "Cargo Handling"; Quantity = 90; UnitPrice = 38.00; ReorderLevel = 30 },
    @{ ProductName = "Industrial Safety Helmet with Face Shield"; Branch = "Takoradi Logistics"; Category = "Safety Gear"; Quantity = 0; UnitPrice = 55.00; ReorderLevel = 25 },
    @{ ProductName = "Bosch Professional Angle Grinder 9in 2200W"; Branch = "Takoradi Logistics"; Category = "Power Tools"; Quantity = 18; UnitPrice = 340.00; ReorderLevel = 12 },
    @{ ProductName = "Grade 304 Stainless Steel Wood Screws Box (500pcs)"; Branch = "Takoradi Logistics"; Category = "Fasteners"; Quantity = 200; UnitPrice = 22.00; ReorderLevel = 50 },
    @{ ProductName = "Heavy Duty Pallet Hand Truck 2.5 Ton"; Branch = "Takoradi Logistics"; Category = "Warehouse Equipment"; Quantity = 7; UnitPrice = 1850.00; ReorderLevel = 3 }
)

$createdCount = 0
foreach ($p in $products) {
    try {
        $body = @{
            productName  = $p.ProductName
            branch       = $p.Branch
            category     = "$($p.Branch) - $($p.Category)"
            quantity     = $p.Quantity
            unitPrice    = $p.UnitPrice
            reorderLevel = $p.ReorderLevel
        } | ConvertTo-Json

        $res = Invoke-RestMethod -Uri "$BaseUrl/api/products" -Method Post -Headers $headers -ContentType "application/json" -Body $body
        Write-Host "  [+] Added product #$($res.productId): $($p.ProductName) [Branch: $($p.Branch), Category: $($p.Category)]" -ForegroundColor Green
        $createdCount++
    } catch {
        Write-Host "  [-] Product '$($p.ProductName)' already exists or skipped." -ForegroundColor Yellow
    }
}

Write-Host "`nSeeding completed successfully! ($createdCount products created/updated)" -ForegroundColor Cyan
