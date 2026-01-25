# Azure Functions - HTTP-Only Integration Pattern

## Overview
Azure Functions in this architecture are **thin integration layers** that:
1. ✅ Receive data from suppliers -  HTTP
2. ✅ Call the main API for all data operations (no direct database access)
3. ✅ Transform data between supplier and internal formats
4. ✅ Return responses to suppliers

**Key Principle**: Azure Functions use **HTTP calls only** - no Entity Framework, no database connections.

**Configuration Note**: In all examples below, `apiBaseUrl` should be set to `http://[YourMachineName]:5000` for local development.
- Replace `[YourMachineName]` with your computer name (run `hostname` in PowerShell to find it)
- Or use `http://localhost:5000` if running API and Functions on the same machine
- **Check the API console output** when you run `dotnet run` - it displays the actual listening URL:
  ```
  Now listening on: http://localhost:5143
  ```
  Use that exact URL and port in your configuration

---

## Architecture Diagram

```
┌─────────────┐        ┌──────────────────┐        ┌─────────────┐
│   Speedy    │───────>│ Azure Function   │───────>│  Main API   │
│  (Webhook)  │  HTTP  │  (HTTP Trigger)  │  HTTP  │   (EF Core) │
└─────────────┘        └──────────────────┘        └─────────────┘
                              │                            │
                              │ Transform Data             │ Database
                              │ No DB Access              │ Operations
                              └────────────────────────────┘
```

---

## API Endpoints to Call from Azure Functions

### 1. **Product Lookups**

#### Get Product by ID
```http
GET /api/products/{id}
```
**Use Case**: Validate ProductId exists (for Speedy)
**Returns**: `ProductDto` or 404

**Example**:
```csharp
var response = await httpClient.GetAsync($"{apiBaseUrl}/api/products/{productId}");
if (!response.IsSuccessStatusCode)
{
    // Product doesn't exist - reject order
    return BadRequest("Product not found");
}
```

#### Get Product by ProductCode (GUID)
```http
GET /api/products/code/{productCode}
```
**Use Case**: Resolve ProductCode → ProductId (for Vault)
**Returns**: `ProductDto` or 404

**Example**:
```csharp
var productCode = Guid.Parse("550e8400-e29b-41d4-a716-446655440000");
var response = await httpClient.GetAsync($"{apiBaseUrl}/api/products/code/{productCode}");
if (response.IsSuccessStatusCode)
{
    var json = await response.Content.ReadAsStringAsync();
    var product = JsonConvert.DeserializeObject<ProductDto>(json);
    var productId = product.Id; // Use this in order
}
```

---

### 2. **Customer Lookups**

#### Check if Customer Exists by ID
```http
GET /api/customers/{id}
```
**Use Case**: Validate CustomerId exists (for Speedy)
**Returns**: `CustomerDto` or 404

**Example**:
```csharp
var response = await httpClient.GetAsync($"{apiBaseUrl}/api/customers/{customerId}");
if (!response.IsSuccessStatusCode)
{
    // Customer doesn't exist
    return BadRequest("Customer not found");
}
```

#### Search Customers by Email
```http
GET /api/customers/search?email={email}
```
**Use Case**: Find customer by email (for Vault), check if exists
**Returns**: List of `CustomerDto` (may be empty)

**Example**:
```csharp
var email = "user@example.com";
var response = await httpClient.GetAsync($"{apiBaseUrl}/api/customers/search?email={email}");
if (response.IsSuccessStatusCode)
{
    var json = await response.Content.ReadAsStringAsync();
    var customers = JsonConvert.DeserializeObject<List<CustomerDto>>(json);
    
    if (customers.Any())
    {
        var customer = customers.First();
        // Customer exists - you could optionally use their ID
    }
    else
    {
        // Customer with this email doesn't exist
        // Decide: reject order or proceed with email only
    }
}
```

---

### 3. **Order Creation**

#### Create New Order
```http
POST /api/orders
Content-Type: application/json

{
  "customerId": 1,              // or null if using email
  "customerEmail": "user@example.com",  // or null if using ID
  "supplierId": 1,
  "orderDate": "2024-01-15T10:30:00Z",
  "orderStatus": "Received",
  "billingAddress": { ... },
  "deliveryAddress": { ... },
  "orderItems": [
    {
      "productId": 1,
      "quantity": 5,
      "price": 29.99
    }
  ]
}
```

**Use Case**: Save the order after transformation and validation
**Returns**: `OrderDto` with assigned ID

**Example**:
```csharp
var createOrderDto = new CreateOrderDto
{
    CustomerId = 1,
    SupplierId = 1,
    OrderDate = DateTime.UtcNow,
    OrderStatus = OrderStatus.Received,
    OrderItems = new List<CreateOrderItemDto>
    {
        new CreateOrderItemDto
        {
            ProductId = 1,
            Quantity = 5,
            Price = 29.99m
        }
    }
};

var json = JsonConvert.SerializeObject(createOrderDto);
var content = new StringContent(json, Encoding.UTF8, "application/json");
var response = await httpClient.PostAsync($"{apiBaseUrl}/api/orders", content);

if (response.IsSuccessStatusCode)
{
    var responseJson = await response.Content.ReadAsStringAsync();
    var createdOrder = JsonConvert.DeserializeObject<OrderDto>(responseJson);
    var orderId = createdOrder.Id; // Order was saved, return this ID
}
```

---

### 4. **Order Updates**

#### Update Existing Order
```http
PUT /api/orders/{id}
Content-Type: application/json

{
  "customerId": 1,
  "customerEmail": null,
  "supplierId": 1,
  "orderDate": "2024-01-15T10:30:00Z",
  "orderStatus": "Picking",
  "billingAddress": { ... },
  "deliveryAddress": { ... },
  "orderItems": [ ... ]
}
```

**Use Case**: Update order status or details
**Returns**: Updated `OrderDto`

---

### 5. **Check for Existing Orders (Idempotency)**

#### Get Orders by Supplier and Customer
```http
GET /api/orders/supplier/{supplierId}?customerId={id}
```
**or**
```http
GET /api/orders/supplier/{supplierId}?customerEmail={email}
```

**Use Case**: Check if order already exists (prevent duplicates)
**Returns**: List of `OrderDto`

**Example**:
```csharp
// For Speedy (uses CustomerId)
var response = await httpClient.GetAsync(
    $"{apiBaseUrl}/api/orders/supplier/1?customerId={customerId}");

// For Vault (uses CustomerEmail)
var response = await httpClient.GetAsync(
    $"{apiBaseUrl}/api/orders/supplier/2?customerEmail={email}");

if (response.IsSuccessStatusCode)
{
    var json = await response.Content.ReadAsStringAsync();
    var existingOrders = JsonSerializer.Deserialize<List<OrderDto>>(json, new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    });
    
    // Check if order with same items/date exists
    // Implement your duplicate detection logic
}
```

---

## Complete Azure Function Example - Speedy (.NET 8 Isolated Worker)

```csharp
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace SpeedyOrderFunction
{
    public class ProcessSpeedyOrder
    {
        private readonly ILogger<ProcessSpeedyOrder> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _apiBaseUrl;

        public ProcessSpeedyOrder(ILogger<ProcessSpeedyOrder> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
            _apiBaseUrl = Environment.GetEnvironmentVariable("API_BASE_URL") 
                ?? "https://your-api.azurewebsites.net";
        }

        [Function("ProcessSpeedyOrder")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "speedy/orders")] 
            HttpRequestData req)
        {
            _logger.LogInformation("Speedy webhook received");

            try
            {
                // ================================================================
                // STEP 1: Read and deserialize incoming webhook
                // ================================================================
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var speedyOrder = JsonSerializer.Deserialize<SpeedyOrderDto>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (speedyOrder == null)
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteAsJsonAsync(new { error = "Invalid order format" });
                    return badResponse;
                }

                _logger.LogInformation($"Processing Speedy order for Customer {speedyOrder.CustomerId}");

                // ================================================================
                // STEP 2: Validate Customer exists via API call
                // ================================================================
                var customerResponse = await _httpClient.GetAsync(
                    $"{_apiBaseUrl}/api/customers/{speedyOrder.CustomerId}");

                if (!customerResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning($"Customer {speedyOrder.CustomerId} not found");
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteAsJsonAsync(new 
                    { 
                        error = $"Customer {speedyOrder.CustomerId} does not exist" 
                    });
                    return badResponse;
                }

                // ================================================================
                // STEP 3: Validate all Products exist via API calls
                // ================================================================
                foreach (var item in speedyOrder.LineItems)
                {
                    var productResponse = await _httpClient.GetAsync(
                        $"{_apiBaseUrl}/api/products/{item.ProductId}");

                    if (!productResponse.IsSuccessStatusCode)
                    {
                        _logger.LogWarning($"Product {item.ProductId} not found");
                        var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                        await badResponse.WriteAsJsonAsync(new 
                        { 
                            error = $"Product {item.ProductId} does not exist" 
                        });
                        return badResponse;
                    }
                }

                // ================================================================
                // STEP 4: Transform to internal CreateOrderDto format
                // ================================================================
                var createOrderDto = new CreateOrderDto
                {
                    CustomerId = speedyOrder.CustomerId,
                    CustomerEmail = null,
                    SupplierId = 1, // Speedy
                    OrderDate = speedyOrder.OrderTimestamp,
                    OrderStatus = OrderStatus.Received,
                    BillingAddress = speedyOrder.BillTo != null ? new AddressDto
                    {
                        Street = speedyOrder.BillTo.StreetAddress,
                        City = speedyOrder.BillTo.City,
                        County = speedyOrder.BillTo.Region,
                        PostalCode = speedyOrder.BillTo.PostCode,
                        Country = speedyOrder.BillTo.Country
                    } : null,
                    DeliveryAddress = speedyOrder.ShipTo != null ? new AddressDto
                    {
                        Street = speedyOrder.ShipTo.StreetAddress,
                        City = speedyOrder.ShipTo.City,
                        County = speedyOrder.ShipTo.Region,
                        PostalCode = speedyOrder.ShipTo.PostCode,
                        Country = speedyOrder.ShipTo.Country
                    } : null,
                    OrderItems = speedyOrder.LineItems.Select(item => new CreateOrderItemDto
                    {
                        ProductId = item.ProductId,
                        Quantity = item.Qty,
                        Price = item.UnitPrice
                    }).ToList()
                };

                // ================================================================
                // STEP 5: Create order via API call (saves to database)
                // ================================================================
                var json = JsonSerializer.Serialize(createOrderDto);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var createResponse = await _httpClient.PostAsync(
                    $"{_apiBaseUrl}/api/orders", 
                    content);

                if (!createResponse.IsSuccessStatusCode)
                {
                    var errorContent = await createResponse.Content.ReadAsStringAsync();
                    _logger.LogError($"Order creation failed: {errorContent}");
                    var errorResponse = req.CreateResponse(createResponse.StatusCode);
                    await errorResponse.WriteAsJsonAsync(new { error = "Order creation failed", details = errorContent });
                    return errorResponse;
                }

                // ================================================================
                // STEP 6: Parse response and return to Speedy
                // ================================================================
                var responseJson = await createResponse.Content.ReadAsStringAsync();
                var createdOrder = JsonSerializer.Deserialize<OrderDto>(responseJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                _logger.LogInformation($"Order {createdOrder.Id} created successfully");

                // Return Speedy-friendly response
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    orderReference = $"SPEEDY-{createdOrder.Id}",
                    orderId = createdOrder.Id,
                    status = "received",
                    totalAmount = createdOrder.TotalAmount,
                    receivedAt = DateTime.UtcNow
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Speedy order");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new { error = "Internal server error" });
                return errorResponse;
            }
        }
    }
}
```

---

## Complete Azure Function Example - Vault (.NET 8 Isolated Worker)

```csharp
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace VaultOrderFunction
{
    public class ProcessVaultOrder
    {
        private readonly ILogger<ProcessVaultOrder> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _apiBaseUrl;

        public ProcessVaultOrder(ILogger<ProcessVaultOrder> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
            _apiBaseUrl = Environment.GetEnvironmentVariable("API_BASE_URL") 
                ?? "https://your-api.azurewebsites.net";
        }

        [Function("ProcessVaultOrder")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "vault/orders")] 
            HttpRequestData req)
        {
            _logger.LogInformation("Vault webhook received");

            try
            {
                // ================================================================
                // STEP 1: Read and deserialize incoming webhook
                // ================================================================
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var vaultOrder = JsonSerializer.Deserialize<VaultOrderDto>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (vaultOrder == null)
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteAsJsonAsync(new { error = "Invalid order format" });
                    return badResponse;
                }

                _logger.LogInformation($"Processing Vault order for {vaultOrder.CustomerEmail}");

                // ================================================================
                // STEP 2: (Optional) Search for Customer by Email via API call
                // ================================================================
                var customerSearchResponse = await _httpClient.GetAsync(
                    $"{_apiBaseUrl}/api/customers/search?email={vaultOrder.CustomerEmail}");

                if (customerSearchResponse.IsSuccessStatusCode)
                {
                    var customersJson = await customerSearchResponse.Content.ReadAsStringAsync();
                    var customers = JsonSerializer.Deserialize<List<CustomerDto>>(customersJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    if (customers.Any())
                    {
                        _logger.LogInformation($"Customer found by email: {customers.First().Id}");
                        // You could optionally use the customer ID
                    }
                    else
                    {
                        _logger.LogWarning($"No customer found with email {vaultOrder.CustomerEmail}");
                        // Decide: reject or proceed with email only
                    }
                }

                // ================================================================
                // STEP 3: Resolve ProductCodes to ProductIds via API calls
                // ================================================================
                var resolvedProducts = new List<(Guid ProductCode, long ProductId, decimal Price, int Quantity)>();

                foreach (var item in vaultOrder.Items)
                {
                    var productResponse = await _httpClient.GetAsync(
                        $"{_apiBaseUrl}/api/products/code/{item.ProductCode}");

                    if (!productResponse.IsSuccessStatusCode)
                    {
                        _logger.LogWarning($"Product code {item.ProductCode} not found");
                        var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                        await badResponse.WriteAsJsonAsync(new 
                        { 
                            error = $"Product with code {item.ProductCode} does not exist" 
                        });
                        return badResponse;
                    }

                    var productJson = await productResponse.Content.ReadAsStringAsync();
                    var product = JsonSerializer.Deserialize<ProductDto>(productJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    resolvedProducts.Add((
                        item.ProductCode,
                        product.Id,
                        item.PricePerUnit,
                        item.QuantityOrdered
                    ));

                    _logger.LogInformation($"Resolved {item.ProductCode} → ProductId {product.Id}");
                }

                // ================================================================
                // STEP 4: Transform to internal CreateOrderDto format
                // ================================================================
                var orderDate = DateTimeOffset.FromUnixTimeSeconds(vaultOrder.PlacedAt).UtcDateTime;

                var createOrderDto = new CreateOrderDto
                {
                    CustomerId = null, // Vault uses email
                    CustomerEmail = vaultOrder.CustomerEmail,
                    SupplierId = 2, // Vault
                    OrderDate = orderDate,
                    OrderStatus = OrderStatus.Received,
                    BillingAddress = vaultOrder.DeliveryDetails?.BillingLocation != null 
                        ? new AddressDto
                        {
                            Street = vaultOrder.DeliveryDetails.BillingLocation.AddressLine,
                            City = vaultOrder.DeliveryDetails.BillingLocation.CityName,
                            County = vaultOrder.DeliveryDetails.BillingLocation.StateProvince,
                            PostalCode = vaultOrder.DeliveryDetails.BillingLocation.ZipPostal,
                            Country = vaultOrder.DeliveryDetails.BillingLocation.CountryCode
                        } 
                        : null,
                    DeliveryAddress = vaultOrder.DeliveryDetails?.ShippingLocation != null 
                        ? new AddressDto
                        {
                            Street = vaultOrder.DeliveryDetails.ShippingLocation.AddressLine,
                            City = vaultOrder.DeliveryDetails.ShippingLocation.CityName,
                            County = vaultOrder.DeliveryDetails.ShippingLocation.StateProvince,
                            PostalCode = vaultOrder.DeliveryDetails.ShippingLocation.ZipPostal,
                            Country = vaultOrder.DeliveryDetails.ShippingLocation.CountryCode
                        } 
                        : null,
                    OrderItems = resolvedProducts.Select(p => new CreateOrderItemDto
                    {
                        ProductId = p.ProductId, // Resolved from ProductCode
                        Quantity = p.Quantity,
                        Price = p.Price
                    }).ToList()
                };

                // ================================================================
                // STEP 5: Create order via API call (saves to database)
                // ================================================================
                var json = JsonSerializer.Serialize(createOrderDto);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var createResponse = await _httpClient.PostAsync(
                    $"{_apiBaseUrl}/api/orders", 
                    content);

                if (!createResponse.IsSuccessStatusCode)
                {
                    var errorContent = await createResponse.Content.ReadAsStringAsync();
                    _logger.LogError($"Order creation failed: {errorContent}");
                    var errorResponse = req.CreateResponse(createResponse.StatusCode);
                    await errorResponse.WriteAsJsonAsync(new { error = "Order creation failed", details = errorContent });
                    return errorResponse;
                }

                // ================================================================
                // STEP 6: Parse response and return to Vault
                // ================================================================
                var responseJson = await createResponse.Content.ReadAsStringAsync();
                var createdOrder = JsonSerializer.Deserialize<OrderDto>(responseJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                _logger.LogInformation($"Order {createdOrder.Id} created successfully");

                // Return Vault-friendly response
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(new
                {
                    order_id = createdOrder.Id.ToString(),
                    external_reference = $"VAULT-{createdOrder.Id}",
                    status = "accepted",
                    received_timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    total_value = createdOrder.TotalAmount,
                    product_resolutions = resolvedProducts.Select(p => new
                    {
                        product_code = p.ProductCode,
                        resolved_product_id = p.ProductId
                    })
                });
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Vault order");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new { error = "Internal server error" });
                return errorResponse;
            }
        }
    }
}
```

---

## Program.cs for .NET 8 Isolated Worker

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
        
        // Register HttpClient for API calls
        services.AddHttpClient();
    })
    .Build();

host.Run();
```

---

## Project File (.csproj) for .NET 8 Isolated Worker

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <AzureFunctionsVersion>v4</AzureFunctionsVersion>
    <OutputType>Exe</OutputType>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Azure.Functions.Worker" Version="1.21.0" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.Http" Version="3.1.0" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Sdk" Version="1.17.0" />
    <PackageReference Include="Microsoft.ApplicationInsights.WorkerService" Version="2.22.0" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.ApplicationInsights" Version="1.2.0" />
  </ItemGroup>
  <ItemGroup>
    <None Update="host.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
    <None Update="local.settings.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
      <CopyToPublishDirectory>Never</CopyToPublishDirectory>
    </None>
  </ItemGroup>
</Project>
```

---

## local.settings.json

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "API_BASE_URL": "https://your-api.azurewebsites.net"
  }
}
```

---

## Summary: HTTP-Only Pattern

### What Azure Functions DO:
✅ Receive webhooks (HTTP Trigger)  
✅ Validate data via API calls (GET endpoints)  
✅ Transform data formats  
✅ Create orders via API calls (POST endpoints)  
✅ Return responses to suppliers  

### What Azure Functions DON'T DO:
❌ Direct database access  
❌ Entity Framework  
❌ Repository pattern  
❌ Database connections  

### Benefits:
1. **Stateless**: No database connection strings in Functions
2. **Scalable**: Functions scale independently of API/database
3. **Secure**: API layer handles all authentication/authorization
4. **Maintainable**: All business logic stays in one place (API)
5. **Testable**: Easy to test without database dependencies

---

## API Endpoints Quick Reference

| Purpose | Endpoint | Method | Use Case |
|---------|----------|--------|----------|
| Validate Product ID | `/api/products/{id}` | GET | Speedy validation |
| Resolve ProductCode | `/api/products/code/{guid}` | GET | Vault resolution |
| Validate Customer ID | `/api/customers/{id}` | GET | Speedy validation |
| Search by Email | `/api/customers/search?email={email}` | GET | Vault lookup |
| Create Order | `/api/orders` | POST | Save order to DB |
| Update Order | `/api/orders/{id}` | PUT | Update order |
| Get Orders by Supplier | `/api/orders/supplier/{id}` | GET | Idempotency check |

---

## Next Steps

1. ✅ Create .NET 8 isolated worker function project
2. ✅ Copy the DTOs to your Azure Function projects
3. ✅ Configure API base URL in Function app settings
4. ✅ Use System.Text.Json (built-in to .NET 8)
5. ✅ Add retry logic with Polly for API calls
6. ✅ Implement idempotency checking
7. ✅ Add Application Insights for monitoring
8. ✅ Set up Function-level authentication

## Key Differences: .NET 8 Isolated Worker vs In-Process

| Aspect | In-Process (Old) | Isolated Worker (.NET 8) |
|--------|------------------|--------------------------|
| **Package** | `Microsoft.Azure.WebJobs` | `Microsoft.Azure.Functions.Worker` |
| **Request Type** | `HttpRequest` | `HttpRequestData` |
| **Response Type** | `IActionResult` | `HttpResponseData` |
| **JSON Library** | Newtonsoft.Json | System.Text.Json |
| **Attribute** | `[FunctionName]` | `[Function]` |
| **Dependency Injection** | Via Startup class | Via `Program.cs` HostBuilder |
| **Performance** | Shared process | Isolated, better resource isolation |

## Why .NET 8 Isolated Worker?

✅ **Better Performance**: Process isolation  
✅ **Modern .NET**: Full .NET 8 features  
✅ **System.Text.Json**: Faster, built-in serialization  
✅ **Better DI**: Standard .NET dependency injection  
✅ **Future-Proof**: Microsoft's recommended approach  
✅ **Easier Testing**: Standard .NET patterns

