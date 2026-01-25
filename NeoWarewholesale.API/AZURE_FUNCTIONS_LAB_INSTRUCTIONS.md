# Azure Functions Lab - HTTP-Only Integration Exercise

## Lab Overview
In this lab, you will create Azure Functions that act as integration endpoints for two different suppliers: **Speedy** and **Vault**. Each supplier sends orders in their own unique format, and your task is to create serverless functions that transform these formats and integrate with the existing NeoWarehouse API.

---

## Learning Objectives

By completing this lab, you will:
1. ✅ Create .NET 8 isolated worker Azure Functions
2. ✅ Implement HTTP triggers for webhook integration
3. ✅ Transform external data formats to internal models
4. ✅ Make HTTP calls to validate and persist data via API
5. ✅ Handle different supplier formats (numeric IDs vs emails/GUIDs)
6. ✅ Use System.Text.Json for serialization
7. ✅ Implement proper error handling and logging

---

## Architecture Pattern

Your Azure Functions will follow this pattern:

```
┌─────────────┐        ┌──────────────────┐        ┌─────────────┐
│   Supplier  │───────>│ Azure Function   │───────>│  Main API   │
│  (Webhook)  │  HTTP  │  (HTTP Trigger)  │  HTTP  │   (EF Core) │
└─────────────┘        └──────────────────┘        └─────────────┘
                              │                            │
                              │ Transform Data             │ Database
                              │ No DB Access              │ Operations
                              └────────────────────────────┘
```

**Key Principle**: Your Azure Functions will **ONLY** use HTTP calls - no Entity Framework, no direct database connections.

---

## Supplier Requirements

### Supplier 1: Speedy

**Customer Identification**: Uses numeric `CustomerId` (long, required)

**Product Identification**: Uses numeric `ProductId` (long)

**Field Mappings**:
- Customer: `customerId` (long)
- Order Date: `orderTimestamp` (DateTime ISO format)
- Products: `lineItems` array
- Product fields: `productId` (long), `qty` (int), `unitPrice` (decimal)
- Addresses: `shipTo` and `billTo` objects
- Address fields: `streetAddress`, `city`, `region`, `postCode`, `country`

**Supplier ID**: 1

### Supplier 2: Vault

**Customer Identification**: Uses `customerEmail` (string, required)

**Product Identification**: Uses `productCode` (Guid) - requires lookup to get ProductId

**Field Mappings**:
- Customer: `customerEmail` (string)
- Order Date: `placedAt` (Unix timestamp - must convert to DateTime)
- Products: `items` array
- Product fields: `productCode` (Guid), `quantityOrdered` (int), `pricePerUnit` (decimal)
- Addresses: Nested in `deliveryDetails` object
  - Billing: `deliveryDetails.billingLocation`
  - Shipping: `deliveryDetails.shippingLocation`
- Address fields: `addressLine`, `cityName`, `stateProvince`, `zipPostal`, `countryCode`

**Supplier ID**: 2

**Special Note**: ProductCode (Guid) must be resolved to ProductId (long) via API call

---

## Models You Can Reuse

The main API project contains DTOs that you should **copy** to your Azure Function projects:

### From `DTOs/External/` folder:
- ✅ `SpeedyOrderDto.cs` - Speedy's incoming format
- ✅ `VaultOrderDto.cs` - Vault's incoming format

### Internal DTOs (create or copy from main API):
- ✅ `CreateOrderDto` - For POST /api/orders
- ✅ `OrderDto` - Response from API
- ✅ `AddressDto` - Address structure
- ✅ `CreateOrderItemDto` - Order line items
- ✅ `ProductDto` - Product information from API
- ✅ `CustomerDto` - Customer information from API

### Enums:
- ✅ `OrderStatus` - Order status enum (Received, Picking, Dispatched, Delivered)

---

## ⚠️ IMPORTANT: Namespace Considerations

When copying DTOs from the main API project to your Azure Functions project, you have **TWO OPTIONS**:

### Option 1: Keep Original Namespace (Recommended)
Keep the same namespace as the main API:
```csharp
namespace NeoWarewholesale.API.DTOs.External
{
    public class SpeedyOrderDto
    {
        // ... properties
    }
}
```

**Pros**: 
- Easier to maintain consistency
- Less confusion
- Matches the API exactly

**Note**: Create matching folder structure in your Functions project:
```
SupplierOrderFunctions/
└── DTOs/
    └── External/
        ├── SpeedyOrderDto.cs
        └── VaultOrderDto.cs
```

### Option 2: Change to Functions Namespace
Change the namespace to match your Azure Functions project:
```csharp
namespace SupplierOrderFunctions.DTOs.External
{
    public class SpeedyOrderDto
    {
        // ... properties
    }
}
```

**Pros**: 
- More "correct" for a separate project
- Follows standard project conventions

**Cons**: 
- Need to update using statements
- Different from API project

### Using Statements

**Option 1 (Original Namespace)**:
```csharp
using NeoWarewholesale.API.DTOs.External;
using NeoWarewholesale.API.Models;
```

**Option 2 (Functions Namespace)**:
```csharp
using SupplierOrderFunctions.DTOs.External;
using SupplierOrderFunctions.Models;
```

### Recommendation

**Use Option 1** (keep original namespace) for this lab because:
- Simpler to understand
- Less potential for errors
- Easier for instructors to help debug
- Can copy-paste code examples without modification

---

## API Endpoints Available to Call

Your Azure Functions should call these endpoints from the main API:

### Product Validation/Lookup:
1. `GET /api/products/{id}` - Validate ProductId exists (for Speedy)
2. `GET /api/products/code/{guid}` - Resolve ProductCode → ProductId (for Vault)

### Customer Validation:
3. `GET /api/customers/{id}` - Validate CustomerId exists (for Speedy)
4. `GET /api/customers/search?email={email}` - Find customer by email (for Vault)

### Order Operations:
5. `POST /api/orders` - Create new order (primary endpoint)
6. `PUT /api/orders/{id}` - Update existing order
7. `GET /api/orders/supplier/{supplierId}` - Get orders by supplier (idempotency checking)

---

## Lab Tasks

### Task 1: Project Setup (15 minutes)

Create a single .NET 8 isolated worker Azure Function project that will contain both functions:

```powershell
func init SupplierOrderFunctions --worker-runtime dotnet-isolated --target-framework net8.0
cd SupplierOrderFunctions
```

**Required NuGet Packages**:
- Microsoft.Azure.Functions.Worker (v1.21.0 or later)
- Microsoft.Azure.Functions.Worker.Extensions.Http (v3.1.0 or later)
- Microsoft.Azure.Functions.Worker.Sdk (v1.17.0 or later)
- Microsoft.ApplicationInsights.WorkerService (v2.22.0 or later)

**Setup Requirements**:
1. Create `Program.cs` with HostBuilder configuration
2. Register `IHttpClientFactory` for making API calls
3. Configure Application Insights
4. Create `local.settings.json` with API base URL setting
5. Target framework: `net8.0`
6. Output type: `Exe`
7. Worker runtime: `dotnet-isolated`

**Project Structure**:
```
SupplierOrderFunctions/
├── Program.cs
├── local.settings.json
├── host.json
├── Functions/
│   ├── ProcessSpeedyOrder.cs
│   └── ProcessVaultOrder.cs
├── DTOs/
│   ├── External/                        ⚠️ Copy from API, keep namespace!
│   │   ├── SpeedyOrderDto.cs
│   │   └── VaultOrderDto.cs
│   ├── CreateOrderDto.cs
│   ├── OrderDto.cs
│   └── ... (other DTOs)
└── SupplierOrderFunctions.csproj
```

**⚠️ Critical Setup Note**: 
When copying DTOs from the main API project, **see the "Namespace Considerations" section** above!
- Recommended: Keep original namespace (e.g., `NeoWarewholesale.API.DTOs.External`)
- Create matching folder structure in your Functions project
- Update using statements in your Function classes accordingly

---

### Task 2: Create Speedy Order Function (45 minutes)

**Function Name**: `ProcessSpeedyOrder`

**Route**: `speedy/orders`

**HTTP Method**: POST

**File Location**: Create in `Functions/ProcessSpeedyOrder.cs`

**Implementation Steps**:

**Step 0: Copy Required DTOs** ⚠️ **DO THIS FIRST!**
   - From the main API project, copy these files to your Functions project:
     - `DTOs/External/SpeedyOrderDto.cs`
     - `DTOs/CreateOrderDto.cs`
     - `DTOs/OrderDto.cs`
     - `DTOs/AddressDto.cs`
     - `DTOs/CreateOrderItemDto.cs`
     - `Models/OrderStatus.cs` (enum)
   - **IMPORTANT**: Keep the original namespaces (e.g., `NeoWarewholesale.API.DTOs.External`)
   - Add using statements to your Function:
     ```csharp
     using NeoWarewholesale.API.DTOs.External;
     using NeoWarewholesale.API.DTOs;
     using NeoWarewholesale.API.Models;
     ```

1. **Create the Function Class**
   - Non-static class with constructor injection
   - Inject `ILogger<ProcessSpeedyOrder>` and `IHttpClientFactory`
   - Get API base URL from environment variable `API_BASE_URL`

2. **Implement the Function Method**
   - Use `[Function]` attribute
   - Accept `HttpRequestData` parameter with `[HttpTrigger]` attribute
   - Return `HttpResponseData`

3. **Step 1: Deserialize Incoming Request**
   - Read request body as string
   - Use `System.Text.Json.JsonSerializer.Deserialize<SpeedyOrderDto>`
   - Set `PropertyNameCaseInsensitive = true` for case-insensitive matching
   - Return 400 Bad Request if deserialization fails

4. **Step 2: Validate Customer**
   - Call `GET /api/customers/{customerId}` using HttpClient
   - Return 400 Bad Request with error message if customer doesn't exist
   - Log warning if validation fails

5. **Step 3: Validate Products**
   - Loop through each line item
   - Call `GET /api/products/{productId}` for each product
   - Return 400 Bad Request if any product doesn't exist
   - Log warnings for missing products

6. **Step 4: Transform to CreateOrderDto**
   - Map `CustomerId` from Speedy format
   - Set `CustomerEmail` to null (Speedy doesn't provide)
   - Set `SupplierId = 1` (Speedy)
   - Map `OrderDate` from `OrderTimestamp`
   - Set `OrderStatus = OrderStatus.Received`
   - Map billing address from `BillTo` object
   - Map delivery address from `ShipTo` object
   - Transform `LineItems` to `OrderItems` list

7. **Step 5: Create Order via API**
   - Serialize CreateOrderDto to JSON
   - Call `POST /api/orders` with JSON content
   - Handle error responses appropriately
   - Log errors if API call fails

8. **Step 6: Return Response**
   - Deserialize API response to OrderDto
   - Create success response with:
     - `success: true`
     - `orderReference: "SPEEDY-{orderId}"`
     - `orderId`
     - `status: "received"`
     - `totalAmount`
     - `receivedAt: DateTime.UtcNow`

9. **Error Handling**
   - Wrap everything in try-catch
   - Return 500 Internal Server Error for exceptions
   - Log all errors with appropriate log levels

**Validation Checklist**:
- [ ] Customer exists before processing
- [ ] All products exist before processing
- [ ] Order successfully created in API
- [ ] Appropriate HTTP status codes returned
- [ ] Errors logged with context

---

### Task 3: Create Vault Order Function (60 minutes)

**Function Name**: `ProcessVaultOrder`

**Route**: `vault/orders`

**HTTP Method**: POST

**Implementation Steps**:

**Step 0: Copy Required DTOs** ⚠️ **If not already done in Task 2!**
   - Make sure you have copied from the main API project:
     - `DTOs/External/VaultOrderDto.cs`
     - `DTOs/ProductDto.cs`
     - All other DTOs from Task 2
   - **IMPORTANT**: Keep the original namespaces
   - Add using statements:
     ```csharp
     using NeoWarewholesale.API.DTOs.External;
     using NeoWarewholesale.API.DTOs;
     using NeoWarewholesale.API.Models;
     ```

1. **Create the Function Class**
   - Same structure as Speedy function (in the same project)
   - Constructor injection with logger and HttpClient factory

2. **Implement the Function Method**
   - Same signature pattern as Speedy

3. **Step 1: Deserialize Incoming Request**
   - Deserialize to `VaultOrderDto`
   - Handle deserialization errors

4. **Step 2: (Optional) Search Customer by Email**
   - Call `GET /api/customers/search?email={email}`
   - Log if customer found or not found
   - Decision: Reject order or proceed with email only?
   - *This is optional validation - discuss with your instructor*

5. **Step 3: Resolve ProductCodes to ProductIds** ⚠️ **Critical Step**
   - Create a list to track resolved products
   - For each item in `vaultOrder.Items`:
     - Call `GET /api/products/code/{productCode}`
     - Store tuple: `(ProductCode, ProductId, Price, Quantity)`
     - Return 400 if any ProductCode cannot be resolved
     - Log each resolution

6. **Step 4: Transform to CreateOrderDto**
   - Set `CustomerId = null` (Vault uses email)
   - Set `CustomerEmail` from Vault order
   - Set `SupplierId = 2` (Vault)
   - **Convert Unix timestamp to DateTime**:
     - Use `DateTimeOffset.FromUnixTimeSeconds(vaultOrder.PlacedAt).UtcDateTime`
   - Set `OrderStatus = OrderStatus.Received`
   - Map billing address from nested `DeliveryDetails.BillingLocation`
   - Map delivery address from nested `DeliveryDetails.ShippingLocation`
   - Create `OrderItems` list using **resolved ProductIds** (not ProductCodes!)

7. **Step 5: Create Order via API**
   - Same as Speedy function
   - Call `POST /api/orders`

8. **Step 6: Return Response**
   - Create Vault-specific response format:
     - `order_id: "{orderId}"`
     - `external_reference: "VAULT-{orderId}"`
     - `status: "accepted"`
     - `received_timestamp: Unix timestamp`
     - `total_value`
     - `product_resolutions: array of ProductCode → ProductId mappings`


9. **Error Handling**
   - Same pattern as Speedy

**Key Differences from Speedy**:
- [ ] Uses email instead of CustomerId
- [ ] Uses ProductCode (Guid) requiring API lookup
- [ ] Unix timestamp conversion required
- [ ] Nested address structure
- [ ] Different response format

---

## Configuration Requirements

### local.settings.json Template

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "API_BASE_URL": "http://[YourMachineName]:5000"
  }
}
```

**For Local Development**: 
- Set `API_BASE_URL` to `http://[YourMachineName]:5000` where `[YourMachineName]` is your computer name
- Or use `http://localhost:5000` if running both API and Functions on the same machine
- **Important:** When you run the API (`dotnet run`), check the console output for the actual listening URL:
  ```
  Now listening on: http://localhost:5143
  ```
  Use that exact URL and port number in your `API_BASE_URL` setting
- Find your machine name: Run `hostname` in PowerShell

### Program.cs Template Structure

You need to create a **single** Program.cs that:
1. Creates a `HostBuilder`
2. Calls `ConfigureFunctionsWorkerDefaults()`
3. Configures services:
   - Application Insights telemetry
   - HttpClient factory (will be shared by both functions)
4. Builds and runs the host

**Program.cs Example Structure**:
```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        
        // Register HttpClient for making API calls
        services.AddHttpClient();
    })
    .Build();

host.Run();
```

**Note**: Both functions will share the same Program.cs and dependency injection configuration. No extension bundles or worker extension projects are needed.

---

## Testing Your Functions

### Test Data for Speedy

**Valid Request**:
```json
{
  "customerId": 1,
  "orderTimestamp": "2024-01-15T10:30:00Z",
  "shipTo": {
    "streetAddress": "123 Main St",
    "city": "London",
    "region": "Greater London",
    "postCode": "SW1A 1AA",
    "country": "United Kingdom"
  },
  "billTo": {
    "streetAddress": "123 Main St",
    "city": "London",
    "region": "Greater London",
    "postCode": "SW1A 1AA",
    "country": "United Kingdom"
  },
  "lineItems": [
    {
      "productId": 1,
      "qty": 5,
      "unitPrice": 29.99
    }
  ]
}
```

**Test Cases**:
1. Valid order with existing customer and products
2. Invalid customer ID (should return 400)
3. Invalid product ID (should return 400)
4. Missing required fields (should return 400)

### Test Data for Vault

**Valid Request**:
```json
{
  "customerEmail": "user@example.com",
  "placedAt": 1705315800,
  "deliveryDetails": {
    "billingLocation": {
      "addressLine": "456 High Street",
      "cityName": "Manchester",
      "stateProvince": "Greater Manchester",
      "zipPostal": "M1 1AA",
      "countryCode": "GB"
    },
    "shippingLocation": {
      "addressLine": "456 High Street",
      "cityName": "Manchester",
      "stateProvince": "Greater Manchester",
      "zipPostal": "M1 1AA",
      "countryCode": "GB"
    }
  },
  "items": [
    {
      "productCode": "550e8400-e29b-41d4-a716-446655440000",
      "quantityOrdered": 3,
      "pricePerUnit": 49.99
    }
  ]
}
```

**Note**: Use actual ProductCode GUIDs from your database!

**Test Cases**:
1. Valid order with existing email and product codes
2. Invalid product code (should return 400)
3. Invalid email format (should return 400)
4. Missing required fields (should return 400)

---

## Debugging Tips

1. **Use Azurite** for local Azure Storage emulation
2. **Enable verbose logging** in host.json
3. **Check API is running** before testing functions
4. **Use Postman or curl** for testing HTTP endpoints
5. **Inspect logs** in console output
6. **Verify API base URL** in local.settings.json
7. **Check System.Text.Json** serialization options (case sensitivity)
8. **Test one function at a time** when debugging
9. **Both functions can run simultaneously** in the same project
10. **No extension bundles needed** - all bindings come from NuGet packages

### Common Issues:

| Problem | Solution |
|---------|----------|
| "404 Not Found" from API | Check API base URL and endpoint paths |
| "Deserialization fails" | Verify PropertyNameCaseInsensitive = true |
| "Product not found" | Use actual GUIDs from your database |
| "Connection refused" | Ensure API is running locally |
| "Method not found" | Check you're using isolated worker, not in-process |
| "Extension bundle errors" | Ensure you used `--no-bundle` flag and have NuGet packages installed |
| "Package version conflicts" | Use exact versions listed for .NET 8 compatibility |
| **"The type or namespace name 'SpeedyOrderDto' could not be found"** | **You forgot to copy DTOs from API project - See "Namespace Considerations" section above** |
| **"Using directive is unnecessary"** | **Check that DTOs are in correct namespace - either keep original or update using statements** |
| **"Cannot find type 'OrderStatus'"** | **Copy the OrderStatus enum from API Models folder and keep its namespace** |

---


## Submission Requirements

Your submission should include:

1. **Two Azure Function Projects**:
   - SpeedyOrderFunction
   - VaultOrderFunction

2. **Each project must have**:
   - Program.cs with proper configuration
   - Function class with HTTP trigger
   - Proper error handling
   - Logging at appropriate levels
   - .csproj targeting net8.0
   - local.settings.json (with placeholder API URL)

3. **Documentation** (README.md):
   - How to run the functions locally
   - What API endpoints they call
   - Test data examples
   - Known limitations


---

## Grading Rubric

| Criteria | Points | Description |
|----------|--------|-------------|
| **Project Setup** | 10 | Correct packages, configuration, Program.cs |
| **Speedy Function** | 25 | Correct implementation, validation, transformation |
| **Vault Function** | 30 | Correct implementation including ProductCode lookup |
| **Error Handling** | 15 | Appropriate error responses and logging |
| **Testing** | 10 | Evidence of successful testing |
| **Code Quality** | 10 | Clean code, proper naming, comments |
| **Total** | 100 | |

---

## Key Takeaways

After completing this lab, you should understand:

1. ✅ **Azure Functions as Integration Layer**: Functions don't directly access databases
2. ✅ **HTTP-Only Pattern**: All data operations via API calls
3. ✅ **Format Transformation**: Converting external formats to internal models
4. ✅ **Reference Data Resolution**: Looking up GUIDs to get numeric IDs
5. ✅ **Isolated Worker Model**: Modern .NET 8 approach vs old in-process
6. ✅ **Dependency Injection**: Constructor injection in Azure Functions
7. ✅ **System.Text.Json**: Using built-in JSON serialization
8. ✅ **Error Handling**: Proper HTTP status codes and error messages

---

## Additional Resources

- [Azure Functions .NET 8 Isolated Documentation](https://learn.microsoft.com/en-us/azure/azure-functions/dotnet-isolated-process-guide)
- [HTTP Trigger Documentation](https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-http-webhook-trigger)
- [System.Text.Json Documentation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json-overview)
- [HttpClient Best Practices](https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/http/httpclient-guidelines)

---

## Questions to Consider

1. Why do we use HttpClient calls instead of direct database access in Azure Functions?
2. What are the advantages of the isolated worker model over the in-process model?
3. How does the ProductCode → ProductId resolution differ between suppliers?
4. What happens if the API is down when a webhook arrives?
5. How would you implement duplicate order detection?
6. Why use IHttpClientFactory instead of creating HttpClient directly?
7. What security considerations should be added for production?

---

## Notes

**Time Estimate**: 4-6 hours

**Prerequisites**:
- Understanding of C# and .NET 8
- Basic understanding of REST APIs
- Familiarity with JSON
- Azure Functions concepts (HTTP triggers)

**Common Student Struggles**:
1. Vault ProductCode lookup (most complex part)
2. Unix timestamp conversion
3. System.Text.Json case sensitivity
4. Isolated worker vs in-process confusion
5. Async/await patterns

**Tips**:
- Start with Speedy (simpler)
- Remember to do a lookup of ProductCode for Vault
- Use live debugging of API calls
- Use Postman for testing
- Why do we use API calls to get data instead of EF (TIP: separation of concerns)
- Clarify that .NET 8 isolated worker uses NuGet packages, I have removed extension bundles from the project to try reduce issues with Artifactory.
- Check my versions in this document against what Artifactory gives as available package versions for .NET 8