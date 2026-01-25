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
  - Resolves ProductCode (Guid) ? ProductId (long)
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
  - Resolves ProductCode ? ProductId
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
| **Transformation** | ? Yes | ? Yes |
| **Validation** | ? No | ? Yes |
| **Database Save** | ? No | ? Yes (EF Core) |
| **Returns** | Transformed JSON | Created order with DB ID |
| **Status Code** | 200 OK | 201 Created |
| **Side Effects** | None | Order persisted to DB |
| **Use Case** | Testing, viewing structure | Production webhooks |

---

## Teaching Progression

### Level 1: Understanding Transformation
1. Call `POST /api/externalorders/fromspeedy` 
2. See how Speedy format ? Internal Order
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
```bash
# Speedy - Transform only
curl -X POST http://localhost:5000/api/externalorders/fromspeedy \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": 1,
    "orderTimestamp": "2024-01-15T10:30:00Z",
    "lineItems": [{"productId": 1, "qty": 5, "unitPrice": 29.99}]
  }'

# Vault - Transform with lookup
curl -X POST http://localhost:5000/api/externalorders/fromvault \
  -H "Content-Type: application/json" \
  -d '{
    "customerEmail": "user@example.com",
    "placedAt": 1705315800,
    "items": [{"productCode": "550e8400-e29b-41d4-a716-446655440000", "quantityOrdered": 3, "pricePerUnit": 49.99}]
  }'
```

### Test Full Integration (With Save)
```bash
# Speedy - Create and save
curl -X POST http://localhost:5000/api/externalorders/speedycreate \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": 1,
    "orderTimestamp": "2024-01-15T10:30:00Z",
    "lineItems": [{"productId": 1, "qty": 5, "unitPrice": 29.99}]
  }'

# Vault - Create and save
curl -X POST http://localhost:5000/api/externalorders/vaultcreate \
  -H "Content-Type: application/json" \
  -d '{
    "customerEmail": "user@example.com",
    "placedAt": 1705315800,
    "items": [{"productCode": "550e8400-e29b-41d4-a716-446655440000", "quantityOrdered": 3, "pricePerUnit": 49.99}]
  }'
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
- `fromspeedy` ? Testing/utility function
- `fromvault` ? Testing/utility function
- `speedycreate` ? Production webhook handler
- `vaultcreate` ? Production webhook handler

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
