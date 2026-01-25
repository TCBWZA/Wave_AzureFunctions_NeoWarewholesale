# NeoWarewholesale API

A .NET 8 Web API for wholesale order management with support for multiple supplier integrations.

---

## Overview

This API provides a centralized system for managing:
- **Customers** - Customer information and management
- **Products** - Product catalog with unique ProductCodes (Guids)
- **Orders** - Order processing and tracking
- **Suppliers** - Multiple supplier integrations (Speedy, Vault)
- **External Orders** - Webhook endpoints for supplier order integration

---

## Technology Stack

- **.NET 8** - Latest LTS version
- **ASP.NET Core Web API** - RESTful API framework
- **Entity Framework Core 8** - ORM for database access
- **SQL Server** - Database (or PostgreSQL with provider swap)
- **System.Text.Json** - JSON serialization
- **Repository Pattern** - Data access abstraction
- **Dependency Injection** - Built-in DI container

---

## Prerequisites

- .NET 8 SDK installed
- SQL Server (LocalDB, Express, or full version)
- Visual Studio 2022 or VS Code
- Git (for version control)

---

## Getting Started

?? **QUICK START TIP**: After running migrations, the database will be **automatically seeded** with sample data (customers, products, orders) on first application start. See section 6 below for details.

### 1. Clone the Repository

```bash
git clone <repository-url>
cd NeoWarewholesale
```

### 2. Configure Database Connection

Update `appsettings.json` with your database connection string:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=NeoWarewholesale;Trusted_Connection=True;MultipleActiveResultSets=true"
  }
}
```

For SQL Server:
```json
"DefaultConnection": "Server=localhost;Database=NeoWarewholesale;Trusted_Connection=True;TrustServerCertificate=True"
```

For PostgreSQL (requires Npgsql.EntityFrameworkCore.PostgreSQL package):
```json
"DefaultConnection": "Host=localhost;Database=neowarewholesale;Username=postgres;Password=yourpassword"
```

### 3. Install EF Core Tools (LOCAL Installation)

?? **IMPORTANT for Corporate Workstations**: If you have issues with global tool installs, use **local installation** instead.

#### Option A: Local Tool Manifest (Recommended for Corp Environments)

```bash
# Navigate to the API project folder
cd NeoWarewholesale.API

# Create a local tool manifest (if not exists)
dotnet new tool-manifest

# Install EF Core tools locally
dotnet tool install dotnet-ef --local

# Verify installation
dotnet tool list
```

#### Option B: Global Installation (If Permissions Allow)

```bash
# Install globally
dotnet tool install --global dotnet-ef

# Verify installation
dotnet ef --version
```

#### Using Local EF Core Tools

When tools are installed locally, prefix commands with `dotnet tool run`:

```bash
# Check EF Core tools version
dotnet tool run dotnet-ef --version

# Or use shorthand
dotnet dotnet-ef --version
```

### 4. Apply Database Migrations

#### Using Local Tools:
```bash
# Navigate to API project
cd NeoWarewholesale.API

# Apply migrations
dotnet tool run dotnet-ef database update

# Or if you're in the solution root
dotnet tool run dotnet-ef database update --project NeoWarewholesale.API
```

#### Using Global Tools:
```bash
# From API project folder
dotnet ef database update

# Or from solution root
dotnet ef database update --project NeoWarewholesale.API
```

### 5. Run the Application

```powershell
# From API project folder
cd NeoWarewholesale.API
dotnet run

# Or from solution root
dotnet run --project NeoWarewholesale.API
```

The API will be available at:
- HTTPS: `https://localhost:7001`
- HTTP: `http://localhost:5000`

### 6. Database Seeding (Important!)

?? **AUTOMATIC DATA SEEDING ON FIRST RUN**

The application includes **automatic database seeding** that populates the database with sample data on first run.

#### Seeding Behavior

**Condition**: Seeding will **ONLY** run if the `Customers` table is **empty**.

**What Gets Seeded:**
- ? 2 Suppliers (Speedy, Vault)
- ? 10 Customers with addresses
- ? 20 Products with ProductCodes (GUIDs)
- ? 5 Sample orders with order items

**Configuration** (in `appsettings.json`):
```json
{
  "DatabaseSeeding": {
    "Enabled": true,
    "SeedOnStartup": true
  }
}
```

#### Disabling Seeding

To disable automatic seeding, set in `appsettings.json`:
```json
{
  "DatabaseSeeding": {
    "Enabled": false,
    "SeedOnStartup": false
  }
}
```

#### Manual Seeding

If you want to manually trigger seeding:
1. Delete all data from the database, OR
2. Drop and recreate the database:
   ```powershell
   dotnet tool run dotnet-ef database drop --force
   dotnet tool run dotnet-ef database update
   dotnet run
   ```

#### Checking If Data Was Seeded

**Look for this log message on startup:**
```
info: NeoWarewholesale.API.Data.DbSeeder[0]
      Database seeding completed. Added X customers, Y products, Z orders.
```

**Or check the database:**
```sql
SELECT COUNT(*) FROM Customers;  -- Should have 10 records
SELECT COUNT(*) FROM Products;   -- Should have 20 records
SELECT COUNT(*) FROM Suppliers;  -- Should have 2 records
```

?? **Important Notes:**
- Seeding runs **automatically** on application startup
- Seeding is **skipped** if `Customers` table already has data
- This prevents duplicate data on subsequent runs
- Perfect for getting started quickly with test data

---

## Database Management

### Creating New Migrations (Local Tools)

```bash
cd NeoWarewholesale.API

# Create a new migration
dotnet tool run dotnet-ef migrations add MigrationName

# Apply the migration
dotnet tool run dotnet-ef database update

# Remove last migration (if not applied)
dotnet tool run dotnet-ef migrations remove
```

### Creating New Migrations (Global Tools)

```bash
cd NeoWarewholesale.API

# Create a new migration
dotnet ef migrations add MigrationName

# Apply the migration
dotnet ef database update

# Remove last migration (if not applied)
dotnet ef migrations remove
```

### Database Commands Reference

```bash
# List all migrations
dotnet tool run dotnet-ef migrations list

# Update to specific migration
dotnet tool run dotnet-ef database update MigrationName

# Generate SQL script
dotnet tool run dotnet-ef migrations script

# Drop database
dotnet tool run dotnet-ef database drop

# Get database info
dotnet tool run dotnet-ef dbcontext info
```

### Troubleshooting EF Core Tools

#### Issue: "dotnet-ef command not found"
**Solution:** Install tools locally (see step 3 above)

#### Issue: "No tool manifest found"
**Solution:** Create manifest first:
```bash
dotnet new tool-manifest
```

#### Issue: "Permission denied" on global install
**Solution:** Use local installation instead (Option A)

#### Issue: "Build failed" when running EF commands
**Solution:** Build the project first:
```bash
dotnet build
dotnet tool run dotnet-ef database update
```

---

## Project Structure

```
NeoWarewholesale.API/
??? Controllers/
?   ??? CustomersController.cs
?   ??? ProductsController.cs
?   ??? OrdersController.cs
?   ??? SuppliersController.cs
?   ??? ExternalOrdersController.cs
??? Data/
?   ??? AppDbContext.cs
?   ??? Migrations/
??? DTOs/
?   ??? External/
?   ?   ??? SpeedyOrderDto.cs
?   ?   ??? VaultOrderDto.cs
?   ??? CreateOrderDto.cs
?   ??? OrderDto.cs
?   ??? ... (other DTOs)
??? Mappings/
?   ??? ExternalOrderMappings.cs
?   ??? DtoMappingExtensions.cs
??? Models/
?   ??? Customer.cs
?   ??? Product.cs
?   ??? Order.cs
?   ??? OrderItem.cs
?   ??? Supplier.cs
?   ??? Address.cs
??? Repositories/
?   ??? Interfaces/
?   ?   ??? ICustomerRepository.cs
?   ?   ??? IProductRepository.cs
?   ?   ??? IOrderRepository.cs
?   ??? Implementations/
?       ??? CustomerRepository.cs
?       ??? ProductRepository.cs
?       ??? OrderRepository.cs
??? Program.cs
??? appsettings.json
??? NeoWarewholesale.API.csproj
```

---

## API Endpoints

### Customers
- `GET /api/customers` - Get all customers
- `GET /api/customers/{id}` - Get customer by ID
- `GET /api/customers/search?email={email}` - Search by email
- `POST /api/customers` - Create customer
- `PUT /api/customers/{id}` - Update customer
- `DELETE /api/customers/{id}` - Delete customer

### Products
- `GET /api/products` - Get all products
- `GET /api/products/{id}` - Get product by ID
- `GET /api/products/code/{guid}` - Get product by ProductCode
- `POST /api/products` - Create product
- `PUT /api/products/{id}` - Update product
- `DELETE /api/products/{id}` - Delete product

### Orders
- `GET /api/orders` - Get all orders
- `GET /api/orders/{id}` - Get order by ID
- `GET /api/orders/supplier/{supplierId}` - Get orders by supplier
- `POST /api/orders` - Create order
- `PUT /api/orders/{id}` - Update order
- `DELETE /api/orders/{id}` - Delete order

### Suppliers
- `GET /api/suppliers` - Get all suppliers
- `GET /api/suppliers/{id}` - Get supplier by ID
- `POST /api/suppliers` - Create supplier
- `PUT /api/suppliers/{id}` - Update supplier
- `DELETE /api/suppliers/{id}` - Delete supplier

### External Orders (Teaching Endpoints)
- `POST /api/externalorders/fromspeedy` - Transform Speedy format (no save)
- `POST /api/externalorders/fromvault` - Transform Vault format (no save)
- `POST /api/externalorders/speedycreate` - Validate and create Speedy order
- `POST /api/externalorders/vaultcreate` - Validate and create Vault order
- `GET /api/externalorders/suppliers` - Get supported suppliers info

---

## External Supplier Integration

The API supports integration with multiple suppliers via the `ExternalOrdersController`:

### Supported Suppliers

#### 1. Speedy (ID: 1)
- **Customer Identification:** Numeric `CustomerId` (long)
- **Product Identification:** Numeric `ProductId` (long)
- **Timestamp Format:** ISO 8601 DateTime
- **Endpoints:** `/fromspeedy`, `/speedycreate`

#### 2. Vault (ID: 2)
- **Customer Identification:** `CustomerEmail` (string)
- **Product Identification:** `ProductCode` (Guid) - requires lookup to ProductId
- **Timestamp Format:** Unix timestamp
- **Endpoints:** `/fromvault`, `/vaultcreate`

### Teaching Pattern

The External Orders Controller demonstrates two patterns:

1. **Transform Only** (`/fromspeedy`, `/fromvault`)
   - Shows data transformation without database access
   - Returns transformed JSON
   - Good for testing and understanding mappings

2. **Validate and Create** (`/speedycreate`, `/vaultcreate`)
   - Validates customer and product existence
   - Creates order in database via EF Core
   - Demonstrates full integration pattern
   - Can be adapted to Azure Functions with HTTP calls

See [EXTERNAL_ORDERS_README.md](EXTERNAL_ORDERS_README.md) for detailed documentation.

See [AZURE_FUNCTIONS_HTTP_ONLY.md](AZURE_FUNCTIONS_HTTP_ONLY.md) for Azure Functions integration pattern.

See [AZURE_FUNCTIONS_LAB_INSTRUCTIONS.md](AZURE_FUNCTIONS_LAB_INSTRUCTIONS.md) for student lab exercises.

---

## Configuration

### appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=NeoWarewholesale;Trusted_Connection=True;MultipleActiveResultSets=true"
  },
  "DatabaseSeeding": {
    "Enabled": true,
    "SeedOnStartup": true
  },
  "AllowedHosts": "*"
}
```

?? **Database Seeding Configuration**:
- **`Enabled: true`** - Allows database seeding to run
- **`SeedOnStartup: true`** - Seeds database automatically on application startup
- **Condition**: Only seeds if `Customers` table is empty
- **To Disable**: Set both values to `false`

### appsettings.Development.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "System": "Information",
      "Microsoft": "Information"
    }
  }
}
```

---

## Development Workflow

### 1. Making Model Changes

```bash
# Edit your model classes in Models/

# Create migration
cd NeoWarewholesale.API
dotnet tool run dotnet-ef migrations add YourMigrationName

# Review the generated migration in Data/Migrations/

# Apply migration
dotnet tool run dotnet-ef database update
```

### 2. Adding New DTOs

1. Create DTO class in `DTOs/`
2. Add mapping logic in `Mappings/`
3. Update controller to use new DTO

### 3. Adding New Controllers

```csharp
[ApiController]
[Route("api/[controller]")]
public class YourController : ControllerBase
{
    private readonly IYourRepository _repository;
    
    public YourController(IYourRepository repository)
    {
        _repository = repository;
    }
    
    // Your endpoints here
}
```

### 4. Testing Your Changes

```bash
# Run the API
dotnet run

# Test with curl
curl -X GET https://localhost:7001/api/customers

# Test with Postman or VS Code REST Client
# See examples in EXTERNAL_ORDERS_README.md
```

---

## Common Issues & Solutions

### Issue: "dotnet-ef not recognized"
**Solution:** Install EF Core tools locally (see step 3)

### Issue: "Unable to create migrations"
**Solution:** Ensure you're in the project folder with DbContext

### Issue: "Database connection failed"
**Solution:** Check connection string and ensure SQL Server is running

### Issue: "Migration pending"
**Solution:** Run `dotnet tool run dotnet-ef database update`

### Issue: "Port already in use"
**Solution:** Change ports in `Properties/launchSettings.json`

### Issue: "CORS errors"
**Solution:** Configure CORS in `Program.cs` for development

### Issue: "No data in database after running"
**Solution:** 
- Check if seeding is enabled in `appsettings.json` (`DatabaseSeeding.Enabled: true`)
- Check application logs for seeding messages
- Remember: Seeding only runs if `Customers` table is empty
- To force re-seed: Drop database, run migrations, then run application

### Issue: "Duplicate data being created"
**Solution:** 
- This shouldn't happen - seeding checks if `Customers` table has data
- If it does, verify the seeding logic in `Program.cs` or `DbSeeder.cs`
- Seeding condition: `if (!context.Customers.Any())`

---

## Testing

Tests are located in the `NeoWarewholesale.Tests` project.

### Run All Tests
```powershell
# From solution root
dotnet test

# With detailed output
dotnet test --logger "console;verbosity=detailed"
```

### Test Categories
- **Mapping Tests:** External order format transformations
- **Controller Tests:** API endpoint behavior
- **Model Tests:** Business logic and calculations

See [NeoWarewholesale.Tests/README.md](../NeoWarewholesale.Tests/README.md) for detailed test documentation.

---

## Deployment

### Publishing the API

```powershell
# Publish for production
dotnet publish -c Release -o ./publish

# The published files will be in ./publish folder
```

### Database in Production

```powershell
# Generate SQL script for migrations
dotnet tool run dotnet-ef migrations script -o migration.sql

# Apply script to production database using SQL Server Management Studio or Azure Data Studio
```

---

## Additional Documentation

- [External Orders Integration](EXTERNAL_ORDERS_README.md) - Supplier webhook integration
- [Azure Functions HTTP Pattern](AZURE_FUNCTIONS_HTTP_ONLY.md) - HTTP-only integration approach
- [Azure Functions Lab](AZURE_FUNCTIONS_LAB_INSTRUCTIONS.md) - Student exercises
- [Namespace Considerations](NAMESPACE_CONSIDERATIONS.md) - DTO namespace guidance
- [Test Suite](../NeoWarewholesale.Tests/README.md) - Comprehensive test documentation

---

## Support

For questions or issues:
1. Check this README
2. Review the documentation files listed above
3. Check the code comments in the controllers
4. Consult your instructor or team lead

---

## License

[Your License Here]

---

## Quick Reference: EF Core Commands (Local Tools)

```bash
# All commands assume you're in NeoWarewholesale.API folder

# Create migration
dotnet tool run dotnet-ef migrations add MigrationName

# Update database
dotnet tool run dotnet-ef database update

# List migrations
dotnet tool run dotnet-ef migrations list

# Remove last migration
dotnet tool run dotnet-ef migrations remove

# Generate SQL script
dotnet tool run dotnet-ef migrations script

# Drop database
dotnet tool run dotnet-ef database drop --force

# See DbContext info
dotnet tool run dotnet-ef dbcontext info
```

**Remember:** If you have global tools installed, you can drop `tool run` from all commands.

---

**Last Updated:** January 2025  
**API Version:** 1.0  
**.NET Version:** 8.0
