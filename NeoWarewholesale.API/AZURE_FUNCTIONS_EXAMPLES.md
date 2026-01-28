# Azure Functions Examples - Speedy and Vault Integration (.NET 8 Isolated Worker)

## Overview
These Azure Functions use the **.NET 8 isolated worker model** and act as integration layers between suppliers and your main API. They:
1. Accept supplier-specific formats (SpeedyOrderDto or VaultOrderDto)
2. Transform to internal Order format
3. Call your existing API endpoints via HttpClient
4. Return responses to suppliers

**Key Differences from In-Process Model:**
- Uses `Microsoft.Azure.Functions.Worker` namespace
- Non-static classes with constructor injection
- `[Function]` attribute instead of `[FunctionName]`
- `HttpRequestData` and `HttpResponseData` instead of `HttpRequest` and `IActionResult`
- `System.Text.Json` instead of Newtonsoft.Json
- Requires Program.cs with HostBuilder configuration

---

## Project Setup

### Required NuGet Packages
```powershell
dotnet add package Microsoft.Azure.Functions.Worker --version 1.23.0
dotnet add package Microsoft.Azure.Functions.Worker.Extensions.Http --version 3.2.0
dotnet add package Microsoft.Azure.Functions.Worker.Sdk --version 1.18.1
dotnet add package Microsoft.ApplicationInsights.WorkerService --version 2.22.0
```

### Program.cs
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

### local.settings.json
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

**Important Notes:**
- Replace `[YourMachineName]` with your actual machine name (e.g., `http://DESKTOP-ABC123:5000`)
- Or use `http://localhost:5000` if running on the same machine
- **Check the console when you run the API** - it will display the actual listening URL:
  ```
  Now listening on: http://localhost:5143
  ```
  Use that exact URL and port in your `API_BASE_URL` setting
- Port numbers vary based on your API's `launchSettings.json` configuration
- Do not use `https` for local development unless you have SSL certificates configured

---

## Speedy Functions

### 1. SpeedyCreate - Create New Order from Speedy

```csharp
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace SupplierOrderFunctions.Functions
{
    public class SpeedyCreate
    {
        private readonly ILogger<SpeedyCreate> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _apiBaseUrl;

        public SpeedyCreate(
            ILogger<SpeedyCreate> logger,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _apiBaseUrl = Environment.GetEnvironmentVariable("API_BASE_URL") 
                ?? throw new InvalidOperationException("API_BASE_URL not configured");
        }

        [Function("SpeedyCreate")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "speedy/orders")] 
            HttpRequestData req)
        {
            _logger.LogInformation("Speedy Create Order function triggered");

            try
            {
                // Step 1: Read and deserialize Speedy format
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var speedyOrder = JsonSerializer.Deserialize<SpeedyOrderDto>(requestBody, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (speedyOrder == null)
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteAsJsonAsync(new { error = "Invalid Speedy order format" });
                    return badResponse;
                }

                _logger.LogInformation($"Received Speedy order for Customer ID: {speedyOrder.CustomerId}");

                // Step 2: Transform to internal CreateOrderDto format
                var createOrderDto = new CreateOrderDto
                {
                    CustomerId = speedyOrder.CustomerId,
                    CustomerEmail = null, // Speedy doesn't provide email
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
                    OrderItems = speedyOrder.LineItems?.Select(item => new CreateOrderItemDto
                    {
                        ProductId = item.ProductId,
                        Quantity = item.Qty,
                        Price = item.UnitPrice
                    }).ToList()
                };

                // Step 3: Call your main API to create the order
                var httpClient = _httpClientFactory.CreateClient();
                var json = JsonSerializer.Serialize(createOrderDto);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await httpClient.PostAsync($"{_apiBaseUrl}/api/orders", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"API call failed: {response.StatusCode} - {responseContent}");
                    var errorResponse = req.CreateResponse(response.StatusCode);
                    await errorResponse.WriteAsJsonAsync(new 
                    { 
                        error = "Failed to create order", 
                        details = responseContent 
                    });
                    return errorResponse;
                }

                // Step 4: Parse the created order response
                var createdOrder = JsonSerializer.Deserialize<OrderDto>(responseContent,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                _logger.LogInformation($"Order {createdOrder.Id} created successfully for Speedy");

                // Step 5: Return Speedy-friendly response
                var successResponse = req.CreateResponse(HttpStatusCode.OK);
                await successResponse.WriteAsJsonAsync(new
                {
                    success = true,
                    orderId = createdOrder.Id,
                    orderReference = $"SPEEDY-{createdOrder.Id}",
                    receivedAt = DateTime.UtcNow,
                    totalAmount = createdOrder.TotalAmount,
                    itemCount = createdOrder.OrderItems?.Count ?? 0,
                    status = "received"
                });
                return successResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Speedy create order");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new { error = "Internal server error" });
                return errorResponse;
            }
        }
    }
}
```

### 2. SpeedyUpdate - Update Existing Order from Speedy

```csharp
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace SupplierOrderFunctions.Functions
{
    public class SpeedyUpdate
    {
        private readonly ILogger<SpeedyUpdate> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _apiBaseUrl;

        public SpeedyUpdate(
            ILogger<SpeedyUpdate> logger,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _apiBaseUrl = Environment.GetEnvironmentVariable("API_BASE_URL");
        }

        [Function("SpeedyUpdate")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = "speedy/orders/{orderId}")] 
            HttpRequestData req,
            long orderId)
        {
            _logger.LogInformation($"Speedy Update Order function triggered for Order ID: {orderId}");

            try
            {
                // Step 1: Read and deserialize Speedy format
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var speedyOrder = JsonSerializer.Deserialize<SpeedyOrderDto>(requestBody,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (speedyOrder == null)
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteAsJsonAsync(new { error = "Invalid Speedy order format" });
                    return badResponse;
                }

                // Step 2: Transform to internal UpdateOrderDto format
                var updateOrderDto = new UpdateOrderDto
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
                    OrderItems = speedyOrder.LineItems?.Select(item => new CreateOrderItemDto
                    {
                        ProductId = item.ProductId,
                        Quantity = item.Qty,
                        Price = item.UnitPrice
                    }).ToList()
                };

                // Step 3: Call your main API to update the order
                var httpClient = _httpClientFactory.CreateClient();
                var json = JsonSerializer.Serialize(updateOrderDto);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await httpClient.PutAsync($"{_apiBaseUrl}/api/orders/{orderId}", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"API call failed: {response.StatusCode} - {responseContent}");
                    var errorResponse = req.CreateResponse(response.StatusCode);
                    await errorResponse.WriteAsJsonAsync(new 
                    { 
                        error = "Failed to update order", 
                        details = responseContent 
                    });
                    return errorResponse;
                }

                var updatedOrder = JsonSerializer.Deserialize<OrderDto>(responseContent,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                _logger.LogInformation($"Order {orderId} updated successfully for Speedy");

                var successResponse = req.CreateResponse(HttpStatusCode.OK);
                await successResponse.WriteAsJsonAsync(new
                {
                    success = true,
                    orderId = updatedOrder.Id,
                    orderReference = $"SPEEDY-{updatedOrder.Id}",
                    updatedAt = DateTime.UtcNow,
                    totalAmount = updatedOrder.TotalAmount,
                    itemCount = updatedOrder.OrderItems?.Count ?? 0
                });
                return successResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing Speedy update for order {orderId}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new { error = "Internal server error" });
                return errorResponse;
            }
        }
    }
}
```

### 3. GetAllOrdersSpeedy - Get Orders for Speedy

```csharp
using System.Net;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace SupplierOrderFunctions.Functions
{
    public class GetAllOrdersSpeedy
    {
        private readonly ILogger<GetAllOrdersSpeedy> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _apiBaseUrl;

        public GetAllOrdersSpeedy(
            ILogger<GetAllOrdersSpeedy> logger,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _apiBaseUrl = Environment.GetEnvironmentVariable("API_BASE_URL");
        }

        [Function("GetAllOrdersSpeedy")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "speedy/orders")] 
            HttpRequestData req)
        {
            _logger.LogInformation("GetAllOrdersSpeedy function triggered");

            try
            {
                // Get query parameters for filtering
                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                long? customerId = query["customerId"] != null ? long.Parse(query["customerId"]) : null;
                string status = query["status"];
                int page = query["page"] != null ? int.Parse(query["page"]) : 1;
                int pageSize = query["pageSize"] != null ? int.Parse(query["pageSize"]) : 20;

                // Step 1: Call your main API to get orders from Speedy (SupplierId = 1)
                var httpClient = _httpClientFactory.CreateClient();
                var url = $"{_apiBaseUrl}/api/orders/supplier/1?includeRelated=true&page={page}&pageSize={pageSize}";
                
                var response = await httpClient.GetAsync(url);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"API call failed: {response.StatusCode} - {responseContent}");
                    var errorResponse = req.CreateResponse(response.StatusCode);
                    await errorResponse.WriteAsJsonAsync(new { error = "Failed to retrieve orders" });
                    return errorResponse;
                }

                var orders = JsonSerializer.Deserialize<List<OrderDto>>(responseContent,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                // Step 2: Filter by customerId if provided
                if (customerId.HasValue)
                {
                    orders = orders.Where(o => o.CustomerId == customerId.Value).ToList();
                }

                // Step 3: Transform to Speedy format
                var speedyOrders = orders.Select(order => new
                {
                    orderId = order.Id,
                    orderReference = $"SPEEDY-{order.Id}",
                    customerId = order.CustomerId,
                    orderTimestamp = order.OrderDate,
                    status = MapOrderStatus(order.OrderStatus),
                    totalAmount = order.TotalAmount,
                    itemCount = order.OrderItems?.Count ?? 0,
                    shipTo = order.DeliveryAddress != null ? new
                    {
                        streetAddress = order.DeliveryAddress.Street,
                        city = order.DeliveryAddress.City,
                        region = order.DeliveryAddress.County,
                        postCode = order.DeliveryAddress.PostalCode,
                        country = order.DeliveryAddress.Country
                    } : null,
                    lineItems = order.OrderItems?.Select(item => new
                    {
                        productId = item.ProductId,
                        productName = item.ProductName,
                        qty = item.Quantity,
                        unitPrice = item.Price,
                        lineTotal = item.LineTotal
                    }).ToList()
                }).ToList();

                _logger.LogInformation($"Retrieved {speedyOrders.Count} orders for Speedy");

                var successResponse = req.CreateResponse(HttpStatusCode.OK);
                await successResponse.WriteAsJsonAsync(new
                {
                    success = true,
                    supplier = "Speedy",
                    totalCount = speedyOrders.Count,
                    page,
                    pageSize,
                    orders = speedyOrders
                });
                return successResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Speedy orders");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new { error = "Internal server error" });
                return errorResponse;
            }
        }

        private static string MapOrderStatus(OrderStatus status)
        {
            return status switch
            {
                OrderStatus.Received => "received",
                OrderStatus.Picking => "processing",
                OrderStatus.Dispatched => "shipped",
                OrderStatus.Delivered => "delivered",
                _ => "unknown"
            };
        }
    }
}
```

---

## Vault Functions

### 4. VaultCreate - Create New Order from Vault

```csharp
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace SupplierOrderFunctions.Functions
{
    public class VaultCreate
    {
        private readonly ILogger<VaultCreate> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _apiBaseUrl;

        public VaultCreate(
            ILogger<VaultCreate> logger,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _apiBaseUrl = Environment.GetEnvironmentVariable("API_BASE_URL");
        }

        [Function("VaultCreate")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "vault/orders")] 
            HttpRequestData req)
        {
            _logger.LogInformation("Vault Create Order function triggered");

            try
            {
                // Step 1: Read and deserialize Vault format
                var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var vaultOrder = JsonSerializer.Deserialize<VaultOrderDto>(requestBody,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (vaultOrder == null)
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteAsJsonAsync(new { error = "Invalid Vault order format" });
                    return badResponse;
                }

                _logger.LogInformation($"Received Vault order for Customer Email: {vaultOrder.CustomerEmail}");

                var httpClient = _httpClientFactory.CreateClient();

                // Step 2: First, resolve ProductCodes to ProductIds
                var orderItems = new List<CreateOrderItemDto>();
                foreach (var item in vaultOrder.Items)
                {
                    // Call API to get product by ProductCode
                    var productResponse = await httpClient.GetAsync(
                        $"{_apiBaseUrl}/api/products/code/{item.ProductCode}");
                    
                    if (!productResponse.IsSuccessStatusCode)
                    {
                        var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                        await badResponse.WriteAsJsonAsync(new 
                        { 
                            error = $"Product not found for code: {item.ProductCode}" 
                        });
                        return badResponse;
                    }

                    var productContent = await productResponse.Content.ReadAsStringAsync();
                    var product = JsonSerializer.Deserialize<ProductDto>(productContent,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    orderItems.Add(new CreateOrderItemDto
                    {
                        ProductId = product.Id,
                        Quantity = item.QuantityOrdered,
                        Price = item.PricePerUnit
                    });
                }

                // Step 3: Convert Unix timestamp to DateTime
                var orderDate = DateTimeOffset.FromUnixTimeSeconds(vaultOrder.PlacedAt).UtcDateTime;

                // Step 4: Transform to internal CreateOrderDto format
                var createOrderDto = new CreateOrderDto
                {
                    CustomerId = null, // Vault uses email
                    CustomerEmail = vaultOrder.CustomerEmail,
                    SupplierId = 2, // Vault
                    OrderDate = orderDate,
                    OrderStatus = OrderStatus.Received,
                    BillingAddress = vaultOrder.DeliveryDetails?.BillingLocation != null ? new AddressDto
                    {
                        Street = vaultOrder.DeliveryDetails.BillingLocation.AddressLine,
                        City = vaultOrder.DeliveryDetails.BillingLocation.CityName,
                        County = vaultOrder.DeliveryDetails.BillingLocation.StateProvince,
                        PostalCode = vaultOrder.DeliveryDetails.BillingLocation.ZipPostal,
                        Country = vaultOrder.DeliveryDetails.BillingLocation.CountryCode
                    } : null,
                    DeliveryAddress = vaultOrder.DeliveryDetails?.ShippingLocation != null ? new AddressDto
                    {
                        Street = vaultOrder.DeliveryDetails.ShippingLocation.AddressLine,
                        City = vaultOrder.DeliveryDetails.ShippingLocation.CityName,
                        County = vaultOrder.DeliveryDetails.ShippingLocation.StateProvince,
                        PostalCode = vaultOrder.DeliveryDetails.ShippingLocation.ZipPostal,
                        Country = vaultOrder.DeliveryDetails.ShippingLocation.CountryCode
                    } : null,
                    OrderItems = orderItems
                };

                // Step 5: Call your main API to create the order
                var json = JsonSerializer.Serialize(createOrderDto);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await httpClient.PostAsync($"{_apiBaseUrl}/api/orders", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"API call failed: {response.StatusCode} - {responseContent}");
                    var errorResponse = req.CreateResponse(response.StatusCode);
                    await errorResponse.WriteAsJsonAsync(new 
                    { 
                        error = "Failed to create order", 
                        details = responseContent 
                    });
                    return errorResponse;
                }

                var createdOrder = JsonSerializer.Deserialize<OrderDto>(responseContent,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                _logger.LogInformation($"Order {createdOrder.Id} created successfully for Vault");

                // Step 6: Return Vault-friendly response
                var successResponse = req.CreateResponse(HttpStatusCode.OK);
                await successResponse.WriteAsJsonAsync(new
                {
                    order_id = createdOrder.Id.ToString(),
                    external_reference = $"VAULT-{createdOrder.Id}",
                    status = "accepted",
                    received_timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    total_value = createdOrder.TotalAmount,
                    line_count = createdOrder.OrderItems?.Count ?? 0,
                    fulfillment_notes = "Order received and ready for processing"
                });
                return successResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Vault create order");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(new { error = "Internal server error" });
                return errorResponse;
            }
        }
    }
}
```
