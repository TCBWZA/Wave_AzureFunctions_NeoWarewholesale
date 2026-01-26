# External Orders Integration - Azure Functions Teaching Framework

## Overview
This project demonstrates how to accept orders from different suppliers with different data formats and normalize them into your internal Order model. This is a common real-world scenario for B2B integrations.

## Teaching Scenario
You work for NeoWarehouse, a wholesale company. Two suppliers (Speedy and Vault) send you orders via webhooks, but each has their own unique data format. You need to:
1. Accept their different formats
2. Validate the data
3. Convert to your internal format
4. Store in your database

## Current Implementation (API Endpoints)

### Files Structure
```
DTOs/External/
├── SpeedyOrderDto.cs      # Speedy's order format
└── VaultOrderDto.cs       # Vault's order format

Mappings/
└── ExternalOrderMappingExtensions.cs  # Conversion logic

Controllers/
└── ExternalOrdersController.cs        # API endpoints
```

### Endpoints

#### 1. POST /api/externalorders/fromspeedy
Accepts orders in Speedy's format:
- Uses `customerId` (required, long) for customer identification
- Uses `productId`, `qty`, `unitPrice` for line items
- Uses `shipTo` and `billTo` for addresses
- Automatically sets `SupplierId = 1` (Speedy)

#### 2. POST /api/externalorders/fromvault
Accepts orders in Vault's format:
- Uses `customerEmail` (required, string) for customer identification
- Uses `productCode` (Guid), `quantityOrdered`, `pricePerUnit` for items
- Uses Unix timestamp for `placedAt`
- Uses nested `deliveryDetails` structure
- Automatically sets `SupplierId = 2` (Vault)
- Performs ProductCode lookup to find ProductId

#### 3. GET /api/externalorders/suppliers
Returns information about supported suppliers.

## Key Differences Between Suppliers

| Field | Internal Model | Speedy Format | Vault Format |
|-------|---------------|---------------|--------------|
| Customer ID | `CustomerId` (long?) | `CustomerId` (long, required) | Not used |
| Customer Email | `CustomerEmail` (string?) | Not used | `CustomerEmail` (string, required) |
| Order Date | `OrderDate` (DateTime) | `OrderTimestamp` (DateTime) | `PlacedAt` (Unix timestamp) |
| Product ID | `ProductId` (long) | `ProductId` (long) | Looked up via `ProductCode` |
| Product Code | N/A | Not used | `ProductCode` (Guid) |
| Quantity | `Quantity` (int) | `Qty` (int) | `QuantityOrdered` (int) |
| Price | `Price` (decimal) | `UnitPrice` (decimal) | `PricePerUnit` (decimal) |
| Address | `BillingAddress` / `DeliveryAddress` | `BillTo` / `ShipTo` | Nested in `DeliveryDetails` |

## Testing the Endpoints

### Test Speedy Order (using PowerShell)
```powershell
$speedyOrder = @"
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
  ],
  "priority": "express"
}
"@

Invoke-RestMethod -Uri "http://localhost:5000/api/externalorders/fromspeedy" `
  -Method Post `
  -ContentType "application/json" `
  -Body $speedyOrder
```

### Test Vault Order (using PowerShell)
```powershell
$vaultOrder = @"
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
  ],
  "fulfillmentInstructions": "Handle with care"
}
"@

Invoke-RestMethod -Uri "http://localhost:5000/api/externalorders/fromvault" `
  -Method Post `
  -ContentType "application/json" `
  -Body $vaultOrder
```

**Note:** Replace `550e8400-e29b-41d4-a716-446655440000` with an actual ProductCode from your database.

## Converting to Azure Functions

### Step 1: Create Azure Function Projects
```powershell
func init SpeedyOrderFunction --dotnet
func init VaultOrderFunction --dotnet
```

### Step 2: Create HTTP Trigger Functions
```powershell
cd SpeedyOrderFunction
func new --name ProcessSpeedyOrder --template "HTTP trigger"

cd ..\VaultOrderFunction
func new --name ProcessVaultOrder --template "HTTP trigger"
```

### Step 3: Copy Code
1. Copy `SpeedyOrderDto.cs` to Function project

~~2. Copy `ExternalOrderMappingExtensions.cs`~~

3. Copy the logic from `OrderFromSpeedy` endpoint to your Function
4. Add necessary NuGet packages (Entity Framework, etc.)

### Step 4: Update Function Signature
```csharp
[FunctionName("ProcessSpeedyOrder")]
public async Task<IActionResult> Run(
    [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
    ILogger log)
{
    // Deserialize request body to SpeedyOrderDto
    // Use ToOrder() extension method
    // Save to database
    // Return response
}
```

## Learning Objectives

### For Students:
1. **Data Transformation**: Learn how to map between different data formats
2. **Supplier Integration**: Understand real-world B2B integration challenges
3. **Validation**: See how to validate external data before processing
4. **Error Handling**: Learn proper error handling for webhook endpoints
5. **Azure Functions**: Convert RESTful endpoints to serverless functions
6. **Logging**: Understand importance of logging in integration scenarios

### Key Concepts:
- **Extension Methods**: Used for clean mapping logic
- **Automatic Supplier Assignment**: Business logic embedded in mapping
- **Format Normalization**: Converting external formats to internal domain model
- **Webhook Pattern**: How suppliers send data to your system
- **Idempotency**: Consider adding order reference IDs to prevent duplicates

## Next Steps for Azure Functions

1. **Add Authentication**: Use Function-level keys or Azure AD
2. **Add Idempotency**: Check for duplicate orders using external order IDs
3. **Add Queue Processing**: Put orders in Azure Queue for async processing
4. **Add Monitoring**: Use Application Insights for tracking
5. **Add Retry Logic**: Handle transient failures with Polly
6. **Add Schema Validation**: Use JSON Schema validation
7. **Add Rate Limiting**: Prevent abuse of webhook endpoints

## Questions for Students

1. Why do we automatically set the `SupplierId` in the mapping extension?
2. How would you handle a situation where Speedy changes their API format?
3. What happens if a product ID from the supplier doesn't exist in our database?
4. How could you make this more robust with retry logic?
5. What would you do if an order arrives twice (duplicate prevention)?
6. How would you test these endpoints with unit tests?
7. What additional logging would you add for production?

## Additional Resources

- [Azure Functions Documentation](https://docs.microsoft.com/en-us/azure/azure-functions/)
- [HTTP Trigger Documentation](https://docs.microsoft.com/en-us/azure/azure-functions/functions-bindings-http-webhook)
- [Entity Framework Core in Azure Functions](https://docs.microsoft.com/en-us/azure/azure-functions/functions-dotnet-dependency-injection)
