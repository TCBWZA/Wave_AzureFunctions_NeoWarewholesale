# NeoWarewholesale Test Suite

## Overview
This test suite contains unit tests for the NeoWarewholesale API focusing on **production-ready scenarios** without using EF InMemory or other non-production patterns. All tests use mocking where necessary to isolate the code under test.

## Test Organization

```
NeoWarewholesale.Tests/
├── Mappings/
│   ├── SpeedyOrderMappingTests.cs     - Tests for Speedy → Order transformation
│   └── VaultOrderMappingTests.cs      - Tests for Vault → Order transformation with ProductCode lookup
├── Controllers/
│   ├── ExternalOrdersController_TransformTests.cs    - Tests for fromspeedy/fromvault (transform-only)
│   └── ExternalOrdersController_CreateTests.cs       - Tests for speedycreate/vaultcreate (validate & save)
├── Models/
│   └── ModelTests.cs                   - Tests for domain models and business logic
└── Main.cs                             - Test framework verification
```

## Test Categories

### 1. Mapping Tests (`Mappings/`)

#### SpeedyOrderMappingTests (12 tests)
Tests the `ToOrder()` extension method that transforms Speedy's format to internal Order model.

**Key Tests:**
- ✅ All fields map correctly (CustomerId, OrderDate, SupplierId)
- ✅ Billing and delivery addresses map with field name changes (Region → County, PostCode → PostalCode)
- ✅ Line items map correctly (Qty → Quantity, UnitPrice → Price)
- ✅ Null handling for optional fields (BillTo, ShipTo, LineItems)
- ✅ Automatic SupplierId=1 assignment
- ✅ Automatic OrderStatus=Received assignment
- ✅ CustomerEmail always null (Speedy doesn't provide)

**Example Test:**
```csharp
[Test]
public void ToOrder_MapsLineItems_Correctly()
{
    var speedyOrder = new SpeedyOrderDto
    {
        CustomerId = 123,
        OrderTimestamp = DateTime.UtcNow,
        LineItems = new List<SpeedyLineItemDto>
        {
            new SpeedyLineItemDto { ProductId = 1, Qty = 5, UnitPrice = 29.99m },
            new SpeedyLineItemDto { ProductId = 2, Qty = 3, UnitPrice = 49.99m }
        }
    };

    var order = speedyOrder.ToOrder();

    Assert.That(order.OrderItems.Count, Is.EqualTo(2));
    Assert.That(order.OrderItems[0].Quantity, Is.EqualTo(5));
}
```

#### VaultOrderMappingTests (12 tests)
Tests the `ToOrderAsync()` extension method that transforms Vault's format including ProductCode → ProductId resolution.

**Key Tests:**
- ✅ All fields map correctly (CustomerEmail, Unix timestamp conversion, SupplierId)
- ✅ Nested address structure maps correctly (DeliveryDetails.BillingLocation/ShippingLocation)
- ✅ Field name transformations (AddressLine → Street, CityName → City, StateProvince → County, etc.)
- ✅ ProductCode (Guid) resolves to ProductId (long) via mocked repository
- ✅ Repository called for each ProductCode
- ✅ Null products skipped (when repository returns null)
- ✅ Unix timestamp converts to UTC DateTime
- ✅ Automatic SupplierId=2 assignment
- ✅ CustomerId always null (Vault uses email)

**Example Test:**
```csharp
[Test]
public async Task ToOrderAsync_ResolvesProductCodes_ToProductIds()
{
    var productCode1 = Guid.NewGuid();
    _mockProductRepository.Setup(x => x.GetByProductCodeAsync(productCode1))
        .ReturnsAsync(new Product { Id = 101, ProductCode = productCode1 });

    var vaultOrder = new VaultOrderDto
    {
        CustomerEmail = "test@example.com",
        PlacedAt = 1705315800,
        Items = new List<VaultItemDto>
        {
            new VaultItemDto
            {
                ProductCode = productCode1,
                QuantityOrdered = 5,
                PricePerUnit = 29.99m
            }
        }
    };

    var order = await vaultOrder.ToOrderAsync(_mockProductRepository.Object);

    Assert.That(order.OrderItems[0].ProductId, Is.EqualTo(101));
}
```

### 2. Controller Tests (`Controllers/`)

#### ExternalOrdersController_TransformTests (12 tests)
Tests the transform-only endpoints (`OrderFromSpeedy`, `OrderFromVault`, `GetSupportedSuppliers`) that convert external formats without saving.

**Key Tests:**
- ✅ Successful transformation returns OK result
- ✅ Field mapping correctness (CustomerId, SupplierId, OrderDate)
- ✅ Null and empty data handling
- ✅ ProductCode resolution for Vault
- ✅ Unix timestamp conversion
- ✅ Logging of transformations and errors
- ✅ Response structure validation
- ✅ GetSupportedSuppliers returns supplier information

**Example Test:**
```csharp
[Test]
public async Task OrderFromVault_ResolvesProductCodes()
{
    var productCode1 = Guid.NewGuid();
    _mockProductRepository.Setup(x => x.GetByProductCodeAsync(productCode1))
        .ReturnsAsync(new Product { Id = 101, ProductCode = productCode1 });

    var vaultOrder = new VaultOrderDto
    {
        CustomerEmail = "test@example.com",
        PlacedAt = 1705315800,
        Items = new List<VaultItemDto>
        {
            new VaultItemDto { ProductCode = productCode1, QuantityOrdered = 2, PricePerUnit = 29.99m }
        }
    };

    var result = await _controller.OrderFromVault(vaultOrder);

    var okResult = result.Result as OkObjectResult;
    Assert.That(okResult, Is.Not.Null);
    _mockProductRepository.Verify(x => x.GetByProductCodeAsync(productCode1), Times.Once);
}
```

#### ExternalOrdersController_CreateTests (22 tests)
Tests the create endpoints (`SpeedyCreate`, `VaultCreate`) that validate and save orders via repositories.

**Key Tests:**
- ✅ Successful order creation returns Created (201)
- ✅ Customer validation (customer must exist for Speedy)
- ✅ Product validation (all products must exist)
- ✅ ProductCode validation and resolution for Vault
- ✅ Repository called with correct data
- ✅ Response includes order reference (SPEEDY-{id}, VAULT-{id})
- ✅ Product resolution information returned for Vault
- ✅ Error handling (400 for validation, 500 for exceptions)
- ✅ Logging of warnings and success messages
- ✅ Order not created when validation fails

**Example Test:**
```csharp
[Test]
public async Task SpeedyCreate_ValidatesCustomerExists()
{
    var speedyOrder = new SpeedyOrderDto
    {
        CustomerId = 999, // Non-existent
        OrderTimestamp = DateTime.UtcNow,
        LineItems = new List<SpeedyLineItemDto>
        {
            new SpeedyLineItemDto { ProductId = 1, Qty = 1, UnitPrice = 10m }
        }
    };

    _mockCustomerRepository.Setup(x => x.ExistsAsync(999))
        .ReturnsAsync(false);

    var result = await _controller.SpeedyCreate(speedyOrder);

    var badRequestResult = result.Result as BadRequestObjectResult;
    Assert.That(badRequestResult, Is.Not.Null);
    
    // Verify order was not created
    _mockOrderRepository.Verify(x => x.CreateAsync(It.IsAny<Order>()), Times.Never);
}
```

### 3. Model Tests (`Models/`)

#### OrderModelTests (7 tests)
Tests business logic in the Order model.

**Key Tests:**
- ✅ TotalAmount calculates correctly (sum of Quantity * Price)
- ✅ TotalAmount is 0 when no items or null OrderItems
- ✅ Can have null CustomerId (when using email - Vault)
- ✅ Can have null CustomerEmail (when using ID - Speedy)
- ✅ OrderStatus enum values are correct
- ✅ Can transition through order statuses

#### OrderItemModelTests (4 tests)
Tests OrderItem calculations.

**Key Tests:**
- ✅ Line total calculates correctly (Quantity * Price)
- ✅ Handles zero quantity and zero price
- ✅ Handles decimal precision correctly

#### AddressModelTests (2 tests)
Tests Address model structure.

**Key Tests:**
- ✅ Can be created with all fields
- ✅ Supports nullable fields

#### ProductModelTests (2 tests)
Tests Product model structure.

**Key Tests:**
- ✅ Has ProductCode as Guid
- ✅ Has numeric Id (long)

## Technology Stack

- **Test Framework**: NUnit 4.4.0
- **Mocking**: Moq 4.20.72
- **Assertions**: NUnit standard assertions (`Assert.That`, `Is.EqualTo`, etc.)
- **Target Framework**: .NET 8

## Running the Tests

### Visual Studio
1. Open Test Explorer (Test > Test Explorer)
2. Click "Run All Tests"

### Command Line
```powershell
dotnet test
```

### With Code Coverage
```powershell
dotnet test /p:CollectCoverage=true
```

## Test Patterns Used

### 1. Arrange-Act-Assert (AAA)
All tests follow the AAA pattern:
```csharp
[Test]
public void TestMethod()
{
    // Arrange - Set up test data and mocks
    var dto = new SpeedyOrderDto { ... };
    
    // Act - Execute the code under test
    var result = dto.ToOrder();
    
    // Assert - Verify the results
    Assert.That(result.SupplierId, Is.EqualTo(1));
}
```

### 2. Mocking External Dependencies
Repository interfaces are mocked to isolate the code under test:
```csharp
_mockProductRepository.Setup(x => x.GetByProductCodeAsync(productCode))
    .ReturnsAsync(new Product { Id = 1, ProductCode = productCode });
```

### 3. Testing Edge Cases
- Null values
- Empty collections
- Zero values
- Missing optional fields

### 4. Testing Business Rules
- SupplierId assignment based on source
- OrderStatus defaulting to Received
- Field name transformations between formats
- Calculated properties (TotalAmount)

## What is NOT Tested

❌ **Repository Implementations** - Not tested because:
- These use EF Core and require database
- EF InMemory is not production-like
- These should be tested with integration tests against real database

❌ **Database Migrations** - Not unit testable

❌ **Configuration/Startup** - Not unit testable

## Test Coverage

| Component | Tests | Coverage |
|-----------|-------|----------|
| Speedy Mapping | 12 tests | All transformation scenarios |
| Vault Mapping | 12 tests | All transformation scenarios + ProductCode lookup |
| Transform Endpoints | 12 tests | fromspeedy, fromvault, GetSupportedSuppliers |
| Create Endpoints | 22 tests | speedycreate, vaultcreate with validation |
| Order Model | 7 tests | Business logic and calculations |
| OrderItem Model | 4 tests | Calculations |
| Address Model | 2 tests | Structure |
| Product Model | 2 tests | Structure |
| **Total** | **73 tests** | **Core business logic + Controller behavior** |

## Key Testing Principles

1. **No Database Dependencies** - All repository calls are mocked
2. **No EF InMemory** - Not production-like, avoided per requirements
3. **Focus on Business Logic** - Tests validate transformations, calculations, and rules
4. **Production Scenarios** - Tests reflect real-world data flows
5. **Clear Test Names** - Each test name describes what it tests
6. **Isolated Tests** - Each test is independent and can run in any order

## Benefits

✅ **Fast** - No database, all tests run in milliseconds  
✅ **Reliable** - Mocked dependencies, no external factors  
✅ **Maintainable** - Clear test structure, easy to update  
✅ **Production-Focused** - Tests real business scenarios  
✅ **CI/CD Ready** - Can run in any environment  

## Future Test Additions

When adding new tests, consider:

1. **New Mapping Logic** - Add tests to `Mappings/` folder
2. **New Business Rules** - Add tests to `Models/` folder
3. **New Calculations** - Test in relevant model test class
4. **Integration Tests** - Create separate project for database-dependent tests

## Integration Testing

For testing with actual database:
- Use real SQL Server or PostgreSQL test database
- Test repository implementations
- Test complete API endpoints
- Use test data seeding
- Clean up after each test

