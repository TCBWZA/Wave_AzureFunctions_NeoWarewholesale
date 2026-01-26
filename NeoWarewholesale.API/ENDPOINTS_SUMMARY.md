# External Orders API Endpoints - Summary

## Overview
The ExternalOrdersController now has **4 main endpoints** demonstrating different levels of integration:

---

## Endpoints Breakdown

### 1. **Transformation Only** (No Database Save)

#### POST `/api/externalorders/fromspeedy`
- **Purpose**: Demonstrates data transformation without persistence
- **Input**: Speedy format (CustomerId, ProductId, etc.)
- **Process**:
  - Accepts Speedy JSON
  - Transforms to internal Order model
  - Returns transformed Order as JSON
  - **DOES NOT SAVE** to database
- **Teaching Point**: Shows pure data mapping logic
- **Use Case**: Testing transformation, seeing internal structure

**Response Example**:
```json
{
  "message": "Order transformed successfully (not saved)",
  "transformedOrder": {
    "customerId": 1,
    "supplierId": 1,
    "supplierName": "Speedy",
    "orderDate": "2024-01-15T10:30:00Z",
    "orderStatus": "Received",
    "orderItems": [...],
    "totalAmount": 149.95,
    "itemCount": 2
  },
  "note": "Use POST /api/externalorders/speedycreate to save this order"
}
```

#### POST `/api/externalorders/fromvault`
- **Purpose**: Demonstrates complex transformation with ProductCode lookup
- **Input**: Vault format (CustomerEmail, ProductCode GUID, etc.)
- **Process**:
  - Accepts Vault JSON
  - Validates ProductCodes exist
  - Resolves ProductCode (Guid) → ProductId (long)
  - Transforms to internal Order model
  - Returns transformed Order with resolution details
  - **DOES NOT SAVE** to database
- **Teaching Point**: Shows data enrichment and reference data lookup
- **Use Case**: Understanding ProductCode resolution without side effects

**Response Example**:
```json
{
  "message": "Order transformed successfully (not saved)",
  "productCodeResolution": [
    {
      "vaultProductCode": "550e8400-e29b-41d4-a716-446655440000",
      "resolvedProductId": 5,
      "productName": "Widget Pro"
    }
  ],
  "transformedOrder": {
    "customerId": null,
    "customerEmail": "user@example.com",
    "supplierId": 2,
    "supplierName": "Vault",
    "orderItems": [...],
    "totalAmount": 149.97
  },
  "note": "Use POST /api/externalorders/vaultcreate to save this order"
}
```

---

### 2. **Full Integration** (With Database Save using Entity Framework)

#### POST `/api/externalorders/speedycreate`
- **Purpose**: Complete workflow - transform AND save
- **Input**: Speedy format
- **Process**:
  - Accepts Speedy JSON
  - Transforms to internal Order model
  - **Validates** customer exists
  - **Validates** all products exist
  - **Saves to database** using Entity Framework (via repository)
  - Returns created order with ID
- **Teaching Point**: Full integration pattern for Azure Functions
- **Use Case**: Production-ready order creation

**Response Example**:
```json
{
  "success": true,
  "orderId": 123,
  "message": "Order from Speedy created and saved successfully",
  "supplier": "Speedy",
  "supplierId": 1,
  "orderReference": "SPEEDY-123",
  "totalAmount": 149.95,
  "itemCount": 2,
  "orderDate": "2024-01-15T10:30:00Z",
  "orderStatus": "Received"
}
```

#### POST `/api/externalorders/vaultcreate`
- **Purpose**: Complete complex workflow - transform, lookup, AND save
- **Input**: Vault format
- **Process**:
  - Accepts Vault JSON
  - Validates ProductCodes exist
  - Resolves ProductCode → ProductId
  - Transforms to internal Order model
  - **Saves to database** using Entity Framework (via repository)
  - Returns created order with ID and resolution details
- **Teaching Point**: Complex integration with reference data resolution
- **Use Case**: Production-ready order creation with GUID product references

**Response Example**:
```json
{
  "success": true,
  "orderId": 124,
  "message": "Order from Vault created and saved successfully",
  "supplier": "Vault",
  "supplierId": 2,
  "orderReference": "VAULT-124",
  "productResolutions": [
    {
      "productCode": "550e8400-e29b-41d4-a716-446655440000",
      "resolvedProductId": 5,
      "productName": "Widget Pro"
    }
  ],
  "totalAmount": 149.97,
  "itemCount": 1,
  "orderDate": "2024-01-15T10:30:00Z",
  "orderStatus": "Received",
  "customerEmail": "user@example.com"
}
```

---

### 3. **Information Endpoint**

#### GET `/api/externalorders/suppliers`
- **Purpose**: Lists available supplier integrations
- **Returns**: Endpoints and format information for both suppliers

**Response**:
```json
{
  "suppliers": [
    {
      "id": 1,
      "name": "Speedy",
      "transformEndpoint": "/api/externalorders/fromspeedy",
      "createEndpoint": "/api/externalorders/speedycreate",
      "format": "Speedy uses: customerId (long), productId (long), qty, unitPrice"
    },
    {
      "id": 2,
      "name": "Vault",
      "transformEndpoint": "/api/externalorders/fromvault",
      "createEndpoint": "/api/externalorders/vaultcreate",
      "format": "Vault uses: customerEmail (string), productCode (Guid), placedAt (Unix timestamp)"
    }
  ],
  "message": "Transform endpoints show data conversion only. Create endpoints save to database using Entity Framework.",
  "teachingNote": "Use 'fromspeedy' and 'fromvault' to see transformation. Use 'speedycreate' and 'vaultcreate' for full integration."
}
```

---

## Comparison Table

| Aspect | `fromspeedy` / `fromvault` | `speedycreate` / `vaultcreate` |
|--------|---------------------------|-------------------------------|
| **Transformation** | ✅ Yes | ✅ Yes |
| **Validation** | ❌ No | ✅ Yes |
| **Database Save** | ❌ No | ✅ Yes (EF Core) |
| **Returns** | Transformed JSON | Created order with DB ID |
| **Status Code** | 200 OK | 201 Created |
| **Side Effects** | None | Order persisted to DB |
| **Use Case** | Testing, viewing structure | Production webhooks |

---

## Teaching Progression

### Level 1: Understanding Transformation
1. Call `POST /api/externalorders/fromspeedy` 
2. See how Speedy format → Internal Order
3. Understand field mappings

### Level 2: Complex Transformation
1. Call `POST /api/externalorders/fromvault`
2. See ProductCode lookup resolution
3. Understand reference data enrichment

### Level 3: Full Integration
1. Call `POST /api/externalorders/speedycreate`
2. See validation + transformation + persistence
3. Order is saved to database

### Level 4: Complex Integration
1. Call `POST /api/externalorders/vaultcreate`
2. See lookup + validation + transformation + persistence
3. Production-ready pattern

---

## Testing Examples

### Test Transformation (No Save)
```powershell
# Speedy - Transform only
$speedyBody = @"
{
  "customerId": 1,
  "orderTimestamp": "2024-01-15T10:30:00Z",
  "lineItems": [{"productId": 1, "qty": 5, "unitPrice": 29.99}]
}
"@

Invoke-RestMethod -Uri "http://localhost:5000/api/externalorders/fromspeedy" `
  -Method Post `
  -ContentType "application/json" `
  -Body $speedyBody

# Vault - Transform with lookup
$vaultBody = @"
{
  "customerEmail": "user@example.com",
  "placedAt": 1705315800,
  "items": [{"productCode": "550e8400-e29b-41d4-a716-446655440000", "quantityOrdered": 3, "pricePerUnit": 49.99}]
}
"@

Invoke-RestMethod -Uri "http://localhost:5000/api/externalorders/fromvault" `
  -Method Post `
  -ContentType "application/json" `
  -Body $vaultBody
```

### Test Full Integration (With Save)
```powershell
# Speedy - Create and save
$speedyCreateBody = @"
{
  "customerId": 1,
  "orderTimestamp": "2024-01-15T10:30:00Z",
  "lineItems": [{"productId": 1, "qty": 5, "unitPrice": 29.99}]
}
"@

Invoke-RestMethod -Uri "http://localhost:5000/api/externalorders/speedycreate" `
  -Method Post `
  -ContentType "application/json" `
  -Body $speedyCreateBody

# Vault - Create and save
$vaultCreateBody = @"
{
  "customerEmail": "user@example.com",
  "placedAt": 1705315800,
  "items": [{"productCode": "550e8400-e29b-41d4-a716-446655440000", "quantityOrdered": 3, "pricePerUnit": 49.99}]
}
"@

Invoke-RestMethod -Uri "http://localhost:5000/api/externalorders/vaultcreate" `
  -Method Post `
  -ContentType "application/json" `
  -Body $vaultCreateBody
```

---

## Key Learning Outcomes

### For Students:
1. **Separation of Concerns**: Transform logic vs persistence logic
2. **Testing**: Can test transformations without database side effects
3. **Validation**: See where and when validation happens
4. **Entity Framework**: How to persist complex object graphs
5. **Error Handling**: Different error types at different stages
6. **Azure Functions**: Each endpoint maps to a potential function

### Azure Functions Migration:
- `fromspeedy` → Testing/utility function
- `fromvault` → Testing/utility function
- `speedycreate` → Production webhook handler
- `vaultcreate` → Production webhook handler

---

## Architecture Benefits

1. **Testability**: Transform endpoints can be tested without database
2. **Debugging**: See transformed structure before saving
3. **Flexibility**: Choose to save or not
4. **Learning**: Clear progression from simple to complex
5. **Production Ready**: Create endpoints follow best practices

---

## Next Steps

1. Add unit tests for transformation logic
2. Add integration tests for create endpoints
3. Add idempotency checking (prevent duplicate orders)
4. Convert create endpoints to Azure Functions
5. Add Application Insights logging
6. Implement retry logic with Polly

---

## Azure Functions Integration - Required API Endpoints

This section documents all API endpoints that Azure Functions will need to call when implementing the webhook integration pattern. Azure Functions act as thin integration layers that transform supplier data and delegate all database operations to the main API.

### Overview

When building Azure Functions for supplier integration:
- ✅ **DO**: Make HTTP calls to these API endpoints
- ❌ **DON'T**: Access the database directly from Functions
- ✅ **DO**: Validate data before creating orders
- ❌ **DON'T**: Skip validation steps

---

### 1. Product Endpoints

#### GET `/api/products/{id}` - Validate Product by ID

**Use Case**: Validate that a ProductId exists (used by Speedy supplier)

**Request Example**:
```powershell
# Check if ProductId 5 exists
$productId = 5
$response = Invoke-RestMethod -Uri "http://localhost:5000/api/products/$productId" -Method Get
```

**Success Response (200 OK)**:
```json
{
  "id": 5,
  "productCode": "550e8400-e29b-41d4-a716-446655440000",
  "name": "Widget Pro"
}
```

**Error Response (404 Not Found)**:
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Not Found",
  "status": 404,
  "detail": "Product with ID 5 not found."
}
```

**Azure Function Usage**:
```csharp
// Validate product exists before creating order
var response = await httpClient.GetAsync($"{apiBaseUrl}/api/products/{productId}");
if (!response.IsSuccessStatusCode)
{
    return req.CreateResponse(HttpStatusCode.BadRequest, new 
    { 
        error = $"Product with ID {productId} not found" 
    });
}
```

---

#### GET `/api/products/code/{productCode}` - Get Product by ProductCode (GUID)

**Use Case**: Resolve ProductCode (Guid) to ProductId (long) - **Critical for Vault supplier**

**Request Example**:
```powershell
# Resolve ProductCode GUID to get ProductId
$productCode = "550e8400-e29b-41d4-a716-446655440000"
$response = Invoke-RestMethod -Uri "http://localhost:5000/api/products/code/$productCode" -Method Get
$productId = $response.id  # Use this for order creation
```

**Success Response (200 OK)**:
```json
{
  "id": 5,
  "productCode": "550e8400-e29b-41d4-a716-446655440000",
  "name": "Widget Pro"
}
```

**Error Response (404 Not Found)**:
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Not Found",
  "status": 404,
  "detail": "Product with code 550e8400-e29b-41d4-a716-446655440000 not found."
}
```

**Azure Function Usage**:
```csharp
// Vault supplier: Resolve ProductCode to ProductId
var resolvedProducts = new List<(Guid ProductCode, long ProductId, decimal Price, int Quantity)>();

foreach (var item in vaultOrder.Items)
{
    var response = await httpClient.GetAsync(
        $"{apiBaseUrl}/api/products/code/{item.ProductCode}");
    
    if (!response.IsSuccessStatusCode)
    {
        _logger.LogWarning($"Product not found: {item.ProductCode}");
        return req.CreateResponse(HttpStatusCode.BadRequest, new 
        { 
            error = $"Product with code {item.ProductCode} not found" 
        });
    }
    
    var jsonContent = await response.Content.ReadAsStringAsync();
    var product = JsonSerializer.Deserialize<ProductDto>(jsonContent, _jsonOptions);
    
    resolvedProducts.Add((
        item.ProductCode, 
        product.Id,           // Use this ProductId in CreateOrderDto
        item.PricePerUnit, 
        item.QuantityOrdered
    ));
}
```

**⚠️ Important**: Vault supplier sends ProductCode (Guid), but the API requires ProductId (long). You **must** call this endpoint to resolve the mapping before creating the order.

---

### 2. Customer Endpoints

#### GET `/api/customers/{id}` - Validate Customer by ID

**Use Case**: Validate that a CustomerId exists (used by Speedy supplier)

**Request Example**:
```powershell
# Check if CustomerId 1 exists
$customerId = 1
$response = Invoke-RestMethod -Uri "http://localhost:5000/api/customers/$customerId" -Method Get
```

**Success Response (200 OK)**:
```json
{
  "id": 1,
  "name": "Acme Corporation",
  "email": "contact@acme.com",
  "phoneNumbers": [...]
}
```

**Error Response (404 Not Found)**:
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Not Found",
  "status": 404,
  "detail": "Customer with ID 1 not found."
}
```

**Azure Function Usage**:
```csharp
// Validate customer exists before creating order
var response = await httpClient.GetAsync($"{apiBaseUrl}/api/customers/{speedyOrder.CustomerId}");
if (!response.IsSuccessStatusCode)
{
    _logger.LogWarning($"Customer not found: {speedyOrder.CustomerId}");
    return req.CreateResponse(HttpStatusCode.BadRequest, new 
    { 
        error = $"Customer with ID {speedyOrder.CustomerId} not found" 
    });
}
```

---

#### GET `/api/customers/search?email={email}` - Search Customer by Email

**Use Case**: Find customer by email address (used by Vault supplier)

**Request Example**:
```powershell
# Search for customer by email
$email = "user@example.com"
$encodedEmail = [System.Web.HttpUtility]::UrlEncode($email)
$response = Invoke-RestMethod -Uri "http://localhost:5000/api/customers/search?email=$encodedEmail" -Method Get
```

**Success Response (200 OK)** - Customer found:
```json
[
  {
    "id": 15,
    "name": "John Doe",
    "email": "user@example.com",
    "phoneNumbers": [...]
  }
]
```

**Success Response (200 OK)** - No customer found (empty array):
```json
[]
```

**Azure Function Usage**:
```csharp
// Optional: Check if customer exists by email (for Vault)
var encodedEmail = Uri.EscapeDataString(vaultOrder.CustomerEmail);
var response = await httpClient.GetAsync(
    $"{apiBaseUrl}/api/customers/search?email={encodedEmail}");

if (response.IsSuccessStatusCode)
{
    var jsonContent = await response.Content.ReadAsStringAsync();
    var customers = JsonSerializer.Deserialize<List<CustomerDto>>(jsonContent, _jsonOptions);
    
    if (customers?.Count > 0)
    {
        _logger.LogInformation($"Found existing customer: {customers[0].Name}");
        // Optional: Use CustomerId instead of email in order
    }
    else
    {
        _logger.LogInformation($"New customer email: {vaultOrder.CustomerEmail}");
        // Proceed with email-based order
    }
}
```

**Note**: This is **optional validation** for Vault. The API accepts orders with either CustomerId OR CustomerEmail, so you can proceed with just the email if the customer doesn't exist yet.

---

### 3. Order Endpoints

#### POST `/api/orders` - Create New Order

**Use Case**: Create a new order in the database (primary endpoint for both suppliers)

**Request Body**: `CreateOrderDto`

**Required Fields**:
- `supplierId` (long) - 1 for Speedy, 2 for Vault
- `orderDate` (DateTime) - ISO 8601 format
- `orderStatus` (enum) - "Received", "Picking", "Dispatched", "Delivered"
- `billingAddress` (AddressDto) - **Required**
- `orderItems` (array) - At least 1 item required
- Either `customerId` (long) **OR** `customerEmail` (string) - At least one required

**Request Example**:
```powershell
$createOrder = @{
    customerId = 1
    supplierId = 1
    orderDate = "2024-01-15T10:30:00Z"
    customerEmail = $null
    billingAddress = @{
        street = "123 Main St"
        city = "London"
        county = "Greater London"
        postalCode = "SW1A 1AA"
        country = "United Kingdom"
    }
    deliveryAddress = @{
        street = "123 Main St"
        city = "London"
        county = "Greater London"
        postalCode = "SW1A 1AA"
        country = "United Kingdom"
    }
    orderStatus = "Received"
    orderItems = @(
        @{
            productId = 1
            quantity = 5
            price = 29.99
        }
    )
} | ConvertTo-Json -Depth 10

Invoke-RestMethod -Uri "http://localhost:5000/api/orders" `
    -Method Post `
    -ContentType "application/json" `
    -Body $createOrder
```

**Success Response (201 Created)**:
```json
{
  "id": 123,
  "customerId": 1,
  "supplierId": 1,
  "supplierName": "Speedy",
  "orderDate": "2024-01-15T10:30:00Z",
  "customerEmail": null,
  "billingAddress": {
    "street": "123 Main St",
    "city": "London",
    "county": "Greater London",
    "postalCode": "SW1A 1AA",
    "country": "United Kingdom"
  },
  "deliveryAddress": {...},
  "orderStatus": "Received",
  "totalAmount": 149.95,
  "orderItems": [
    {
      "id": 456,
      "productId": 1,
      "productName": "Widget Pro",
      "productCode": "550e8400-e29b-41d4-a716-446655440000",
      "quantity": 5,
      "price": 29.99,
      "lineTotal": 149.95
    }
  ]
}
```

**Error Response (400 Bad Request)** - Validation failed:
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Order": [
      "Either CustomerId or CustomerEmail must be provided."
    ],
    "OrderItems": [
      "Order must contain at least one item."
    ]
  }
}
```

**Azure Function Usage**:
```csharp
// Create order via API
var createOrderDto = new CreateOrderDto
{
    CustomerId = speedyOrder.CustomerId,  // or null for Vault
    CustomerEmail = vaultOrder?.CustomerEmail,  // or null for Speedy
    SupplierId = 1,  // 1 = Speedy, 2 = Vault
    OrderDate = speedyOrder.OrderTimestamp,  // or converted Unix timestamp for Vault
    OrderStatus = OrderStatus.Received,
    BillingAddress = new AddressDto
    {
        Street = speedyOrder.BillTo.StreetAddress,
        City = speedyOrder.BillTo.City,
        County = speedyOrder.BillTo.Region,
        PostalCode = speedyOrder.BillTo.PostCode,
        Country = speedyOrder.BillTo.Country
    },
    DeliveryAddress = new AddressDto {...},
    OrderItems = speedyOrder.LineItems.Select(item => new CreateOrderItemDto
    {
        ProductId = item.ProductId,  // or resolved ProductId for Vault
        Quantity = item.Qty,
        Price = item.UnitPrice
    }).ToList()
};

var json = JsonSerializer.Serialize(createOrderDto, _jsonOptions);
var content = new StringContent(json, Encoding.UTF8, "application/json");

var response = await httpClient.PostAsync($"{apiBaseUrl}/api/orders", content);

if (!response.IsSuccessStatusCode)
{
    var errorContent = await response.Content.ReadAsStringAsync();
    _logger.LogError($"Failed to create order: {errorContent}");
    return req.CreateResponse(HttpStatusCode.InternalServerError, new 
    { 
        error = "Failed to create order in API" 
    });
}

var createdOrderJson = await response.Content.ReadAsStringAsync();
var createdOrder = JsonSerializer.Deserialize<OrderDto>(createdOrderJson, _jsonOptions);
```

---

#### PUT `/api/orders/{id}` - Update Existing Order

**Use Case**: Update an existing order (optional - for advanced scenarios)

**Request Example**:
```powershell
$orderId = 123
$updateOrder = @{
    customerId = 1
    supplierId = 1
    orderDate = "2024-01-15T10:30:00Z"
    customerEmail = $null
    billingAddress = @{...}
    deliveryAddress = @{...}
    orderStatus = "Picking"  # Changed status
    orderItems = @(...)
} | ConvertTo-Json -Depth 10

Invoke-RestMethod -Uri "http://localhost:5000/api/orders/$orderId" `
    -Method Put `
    -ContentType "application/json" `
    -Body $updateOrder
```

**Success Response (200 OK)**:
```json
{
  "id": 123,
  "orderStatus": "Picking",
  ...
}
```

---

#### GET `/api/orders/supplier/{supplierId}` - Get Orders by Supplier

**Use Case**: Retrieve all orders from a specific supplier (useful for idempotency checking)

**Request Example**:
```powershell
# Get all Speedy orders
$supplierId = 1
$response = Invoke-RestMethod -Uri "http://localhost:5000/api/orders/supplier/$supplierId" -Method Get
```

**Success Response (200 OK)**:
```json
[
  {
    "id": 123,
    "supplierId": 1,
    "supplierName": "Speedy",
    "orderDate": "2024-01-15T10:30:00Z",
    "orderStatus": "Received",
    "totalAmount": 149.95,
    ...
  },
  {
    "id": 124,
    ...
  }
]
```

**Azure Function Usage** (Idempotency Check):
```csharp
// Check if order already exists to prevent duplicates
var response = await httpClient.GetAsync($"{apiBaseUrl}/api/orders/supplier/1");
if (response.IsSuccessStatusCode)
{
    var json = await response.Content.ReadAsStringAsync();
    var existingOrders = JsonSerializer.Deserialize<List<OrderDto>>(json, _jsonOptions);
    
    // Check if an order with same customer and date already exists
    var duplicate = existingOrders.FirstOrDefault(o => 
        o.CustomerId == speedyOrder.CustomerId && 
        o.OrderDate.Date == speedyOrder.OrderTimestamp.Date);
    
    if (duplicate != null)
    {
        _logger.LogWarning($"Duplicate order detected: {duplicate.Id}");
        return req.CreateResponse(HttpStatusCode.Conflict, new 
        { 
            message = "Order already exists",
            existingOrderId = duplicate.Id
        });
    }
}
```

---

### 4. Supplier Endpoints

#### GET `/api/suppliers` - Get All Suppliers

**Use Case**: Retrieve supplier information (reference data)

**Request Example**:
```powershell
$response = Invoke-RestMethod -Uri "http://localhost:5000/api/suppliers" -Method Get
```

**Success Response (200 OK)**:
```json
[
  {
    "id": 1,
    "name": "Speedy"
  },
  {
    "id": 2,
    "name": "Vault"
  }
]
```

---

## Common Integration Patterns

### Pattern 1: Speedy Order Flow (Simple)

```
1. Receive SpeedyOrderDto from webhook
2. Validate Customer: GET /api/customers/{customerId}
3. Validate Products: GET /api/products/{productId} (for each item)
4. Transform to CreateOrderDto
5. Create Order: POST /api/orders
6. Return success response with order ID
```

### Pattern 2: Vault Order Flow (Complex - with ProductCode resolution)

```
1. Receive VaultOrderDto from webhook
2. (Optional) Search Customer: GET /api/customers/search?email={email}
3. Resolve ProductCodes: GET /api/products/code/{guid} (for each item)
   → Store mapping: ProductCode → ProductId
4. Transform to CreateOrderDto using resolved ProductIds
5. Create Order: POST /api/orders
6. Return success response with order ID and resolution details
```

---

## Error Handling Best Practices

### HTTP Status Codes to Handle

| Status Code | Meaning | Action |
|-------------|---------|--------|
| **200 OK** | Success (GET requests) | Process response data |
| **201 Created** | Success (POST orders) | Order created successfully |
| **400 Bad Request** | Validation error | Return error to supplier with details |
| **404 Not Found** | Resource not found | Customer/Product doesn't exist - reject order |
| **409 Conflict** | Duplicate order | Order already exists - return existing order ID |
| **500 Internal Server Error** | API error | Log error, retry, or reject order |

### Example Error Handling:

```csharp
try
{
    var response = await httpClient.GetAsync($"{apiBaseUrl}/api/products/{productId}");
    
    switch (response.StatusCode)
    {
        case HttpStatusCode.OK:
            // Process success
            var json = await response.Content.ReadAsStringAsync();
            var product = JsonSerializer.Deserialize<ProductDto>(json, _jsonOptions);
            break;
            
        case HttpStatusCode.NotFound:
            _logger.LogWarning($"Product not found: {productId}");
            return req.CreateResponse(HttpStatusCode.BadRequest, new 
            { 
                error = $"Product with ID {productId} not found",
                rejected = true
            });
            
        case HttpStatusCode.InternalServerError:
            _logger.LogError($"API error when fetching product {productId}");
            return req.CreateResponse(HttpStatusCode.ServiceUnavailable, new 
            { 
                error = "Service temporarily unavailable",
                retryAfter = 60
            });
            
        default:
            _logger.LogError($"Unexpected status: {response.StatusCode}");
            throw new Exception($"Unexpected API response: {response.StatusCode}");
    }
}
catch (HttpRequestException ex)
{
    _logger.LogError(ex, "API connection failed");
    return req.CreateResponse(HttpStatusCode.ServiceUnavailable, new 
    { 
        error = "Cannot connect to API",
        retryAfter = 60
    });
}
```

---

## Testing Checklist for Azure Functions

Before submitting your Azure Functions, test these scenarios:

### Speedy Function Tests:
- [ ] ✅ Valid order with existing customer and products → 200 OK, order created
- [ ] ❌ Invalid customer ID → 400 Bad Request with error message
- [ ] ❌ Invalid product ID → 400 Bad Request with error message
- [ ] ❌ Missing required fields → 400 Bad Request with validation errors
- [ ] ⚠️ API is down → 503 Service Unavailable with retry message

### Vault Function Tests:
- [ ] ✅ Valid order with existing email and product codes → 200 OK, order created
- [ ] ❌ Invalid product code (GUID) → 400 Bad Request with error message
- [ ] ❌ Invalid email format → 400 Bad Request with validation error
- [ ] ❌ Missing required fields → 400 Bad Request with validation errors
- [ ] ⚠️ ProductCode lookup fails → 400 Bad Request with resolution error
- [ ] ⚠️ API is down → 503 Service Unavailable with retry message

---

## Quick Reference: API Endpoints Summary

| Endpoint | Method | Use Case | Speedy | Vault |
|----------|--------|----------|--------|-------|
| `/api/products/{id}` | GET | Validate ProductId | ✅ Required | ❌ Not used |
| `/api/products/code/{guid}` | GET | Resolve ProductCode | ❌ Not used | ✅ Required |
| `/api/customers/{id}` | GET | Validate CustomerId | ✅ Required | ❌ Not used |
| `/api/customers/search?email=` | GET | Search by email | ❌ Not used | ⚠️ Optional |
| `/api/orders` | POST | Create order | ✅ Required | ✅ Required |
| `/api/orders/{id}` | PUT | Update order | ⚠️ Optional | ⚠️ Optional |
| `/api/orders/supplier/{id}` | GET | Idempotency check | ⚠️ Optional | ⚠️ Optional |

**Legend**: ✅ Required | ❌ Not used | ⚠️ Optional/Advanced

---

## Configuration Tips

### API Base URL Configuration

**local.settings.json**:
```json
{
  "Values": {
    "API_BASE_URL": "http://[YourMachineName]:5000"
  }
}
```

**Finding your machine name**:
```powershell
# Get your machine name
hostname

# Or use localhost if API and Functions run on same machine
$apiUrl = "http://localhost:5000"
```

**Important**: When you run the API with `dotnet run`, check the console output for the actual listening URL:
```
Now listening on: http://localhost:5143
```
Use that exact URL and port in your `API_BASE_URL` setting!

---

## Troubleshooting API Calls

| Problem | Solution |
|---------|----------|
| **Connection refused** | Ensure API is running (`dotnet run` in API project) |
| **404 Not Found on valid endpoint** | Check API base URL and port number |
| **Deserialization fails** | Set `PropertyNameCaseInsensitive = true` in JsonSerializerOptions |
| **401 Unauthorized** | API doesn't require auth for local dev - check if using wrong URL |
| **Timeout** | API may be slow on first request - increase timeout or wait for warmup |
| **Null reference exception** | Check API response for null values, handle gracefully |

---

**Last Updated**: January 2025  
**Lab Version**: 1.0  
**Requires**: NeoWarewholesale.API running locally
