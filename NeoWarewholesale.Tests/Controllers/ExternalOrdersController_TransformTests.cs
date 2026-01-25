using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NeoWarewholesale.API.Controllers;
using NeoWarewholesale.API.DTOs.External;
using NeoWarewholesale.API.Repositories;

namespace NeoWarewholesale.Tests.Controllers
{
    /// <summary>
    /// Tests for ExternalOrdersController transform-only endpoints (fromspeedy, fromvault).
    /// These endpoints transform data without saving to database.
    /// </summary>
    [TestFixture]
    public class ExternalOrdersController_TransformTests
    {
        private Mock<IOrderRepository> _mockOrderRepository;
        private Mock<ICustomerRepository> _mockCustomerRepository;
        private Mock<IProductRepository> _mockProductRepository;
        private Mock<ILogger<ExternalOrdersController>> _mockLogger;
        private ExternalOrdersController _controller;

        [SetUp]
        public void Setup()
        {
            _mockOrderRepository = new Mock<IOrderRepository>();
            _mockCustomerRepository = new Mock<ICustomerRepository>();
            _mockProductRepository = new Mock<IProductRepository>();
            _mockLogger = new Mock<ILogger<ExternalOrdersController>>();

            _controller = new ExternalOrdersController(
                _mockOrderRepository.Object,
                _mockCustomerRepository.Object,
                _mockProductRepository.Object,
                _mockLogger.Object
            );
        }

        #region OrderFromSpeedy Tests

        [Test]
        public async Task OrderFromSpeedy_WithValidOrder_ReturnsOkWithTransformedOrder()
        {
            // Arrange
            var speedyOrder = new SpeedyOrderDto
            {
                CustomerId = 123,
                OrderTimestamp = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
                LineItems = new List<SpeedyLineItemDto>
                {
                    new SpeedyLineItemDto { ProductId = 1, Qty = 5, UnitPrice = 29.99m }
                }
            };

            // Act
            var result = await _controller.OrderFromSpeedy(speedyOrder);

            // Assert
            Assert.That(result, Is.InstanceOf<ActionResult<object>>());
            var okResult = result.Result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            Assert.That(okResult.StatusCode, Is.EqualTo(200));

            // Verify response structure
            var response = okResult.Value;
            Assert.That(response, Is.Not.Null);
            
            var messageProperty = response.GetType().GetProperty("message");
            Assert.That(messageProperty, Is.Not.Null);
            Assert.That(messageProperty.GetValue(response)?.ToString(), Does.Contain("transformed"));
        }

        [Test]
        public async Task OrderFromSpeedy_MapsFieldsCorrectly()
        {
            // Arrange
            var speedyOrder = new SpeedyOrderDto
            {
                CustomerId = 123,
                OrderTimestamp = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
                BillTo = new SpeedyAddressDto
                {
                    StreetAddress = "123 Billing St",
                    City = "London",
                    Region = "Greater London",
                    PostCode = "SW1A 1AA",
                    Country = "United Kingdom"
                },
                LineItems = new List<SpeedyLineItemDto>
                {
                    new SpeedyLineItemDto { ProductId = 1, Qty = 5, UnitPrice = 29.99m }
                }
            };

            // Act
            var result = await _controller.OrderFromSpeedy(speedyOrder);

            // Assert
            var okResult = result.Result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);

            var response = okResult.Value;
            var transformedOrderProperty = response.GetType().GetProperty("transformedOrder");
            Assert.That(transformedOrderProperty, Is.Not.Null);

            var transformedOrder = transformedOrderProperty.GetValue(response);
            Assert.That(transformedOrder, Is.Not.Null);

            // Check CustomerId
            var customerIdProperty = transformedOrder.GetType().GetProperty("CustomerId");
            Assert.That(customerIdProperty?.GetValue(transformedOrder), Is.EqualTo(123));

            // Check SupplierId
            var supplierIdProperty = transformedOrder.GetType().GetProperty("SupplierId");
            Assert.That(supplierIdProperty?.GetValue(transformedOrder), Is.EqualTo(1), "SupplierId should be 1 for Speedy");

            // Check SupplierName
            var supplierNameProperty = transformedOrder.GetType().GetProperty("supplierName");
            Assert.That(supplierNameProperty?.GetValue(transformedOrder), Is.EqualTo("Speedy"));
        }

        [Test]
        public async Task OrderFromSpeedy_WithNullLineItems_HandlesGracefully()
        {
            // Arrange
            var speedyOrder = new SpeedyOrderDto
            {
                CustomerId = 123,
                OrderTimestamp = DateTime.UtcNow,
                LineItems = null
            };

            // Act
            var result = await _controller.OrderFromSpeedy(speedyOrder);

            // Assert
            var okResult = result.Result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null, "Should transform even with null line items");
        }

        [Test]
        public async Task OrderFromSpeedy_WithEmptyLineItems_ReturnsZeroTotal()
        {
            // Arrange
            var speedyOrder = new SpeedyOrderDto
            {
                CustomerId = 123,
                OrderTimestamp = DateTime.UtcNow,
                LineItems = new List<SpeedyLineItemDto>()
            };

            // Act
            var result = await _controller.OrderFromSpeedy(speedyOrder);

            // Assert
            var okResult = result.Result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);

            var response = okResult.Value;
            var transformedOrderProperty = response.GetType().GetProperty("transformedOrder");
            var transformedOrder = transformedOrderProperty?.GetValue(response);
            
            var totalAmountProperty = transformedOrder?.GetType().GetProperty("totalAmount");
            Assert.That(totalAmountProperty?.GetValue(transformedOrder), Is.EqualTo(0m));
        }

        [Test]
        public async Task OrderFromSpeedy_LogsInformation()
        {
            // Arrange
            var speedyOrder = new SpeedyOrderDto
            {
                CustomerId = 123,
                OrderTimestamp = DateTime.UtcNow,
                LineItems = new List<SpeedyLineItemDto>
                {
                    new SpeedyLineItemDto { ProductId = 1, Qty = 1, UnitPrice = 10m }
                }
            };

            // Act
            await _controller.OrderFromSpeedy(speedyOrder);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Transforming order from Speedy")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.AtLeastOnce);
        }

        #endregion

        #region OrderFromVault Tests

        [Test]
        public async Task OrderFromVault_WithValidOrder_ReturnsOkWithTransformedOrder()
        {
            // Arrange
            var productCode = Guid.NewGuid();
            var vaultOrder = new VaultOrderDto
            {
                CustomerEmail = "test@example.com",
                PlacedAt = 1705315800,
                Items = new List<VaultItemDto>
                {
                    new VaultItemDto
                    {
                        ProductCode = productCode,
                        QuantityOrdered = 3,
                        PricePerUnit = 49.99m
                    }
                }
            };

            _mockProductRepository.Setup(x => x.GetByProductCodeAsync(productCode))
                .ReturnsAsync(new API.Models.Product { Id = 1, ProductCode = productCode, Name = "Test Product" });

            // Act
            var result = await _controller.OrderFromVault(vaultOrder);

            // Assert
            var okResult = result.Result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            Assert.That(okResult.StatusCode, Is.EqualTo(200));
        }

        [Test]
        public async Task OrderFromVault_ResolvesProductCodes()
        {
            // Arrange
            var productCode1 = Guid.NewGuid();
            var productCode2 = Guid.NewGuid();
            var vaultOrder = new VaultOrderDto
            {
                CustomerEmail = "test@example.com",
                PlacedAt = 1705315800,
                Items = new List<VaultItemDto>
                {
                    new VaultItemDto { ProductCode = productCode1, QuantityOrdered = 2, PricePerUnit = 29.99m },
                    new VaultItemDto { ProductCode = productCode2, QuantityOrdered = 1, PricePerUnit = 49.99m }
                }
            };

            _mockProductRepository.Setup(x => x.GetByProductCodeAsync(productCode1))
                .ReturnsAsync(new API.Models.Product { Id = 101, ProductCode = productCode1, Name = "Product A" });
            _mockProductRepository.Setup(x => x.GetByProductCodeAsync(productCode2))
                .ReturnsAsync(new API.Models.Product { Id = 102, ProductCode = productCode2, Name = "Product B" });

            // Act
            var result = await _controller.OrderFromVault(vaultOrder);

            // Assert
            var okResult = result.Result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);

            // Controller calls GetByProductCodeAsync twice per product:
            // 1. During validation loop
            // 2. During ToOrderAsync mapping
            _mockProductRepository.Verify(x => x.GetByProductCodeAsync(productCode1), Times.Exactly(2));
            _mockProductRepository.Verify(x => x.GetByProductCodeAsync(productCode2), Times.Exactly(2));

            // Verify response contains product resolutions
            var response = okResult.Value;
            var productCodeResolutionProperty = response.GetType().GetProperty("productCodeResolution");
            Assert.That(productCodeResolutionProperty, Is.Not.Null);
        }

        [Test]
        public async Task OrderFromVault_WithInvalidProductCode_ReturnsBadRequest()
        {
            // Arrange
            var productCode = Guid.NewGuid();
            var vaultOrder = new VaultOrderDto
            {
                CustomerEmail = "test@example.com",
                PlacedAt = 1705315800,
                Items = new List<VaultItemDto>
                {
                    new VaultItemDto { ProductCode = productCode, QuantityOrdered = 1, PricePerUnit = 10m }
                }
            };

            _mockProductRepository.Setup(x => x.GetByProductCodeAsync(productCode))
                .ReturnsAsync((API.Models.Product)null);

            // Act
            var result = await _controller.OrderFromVault(vaultOrder);

            // Assert
            var badRequestResult = result.Result as BadRequestObjectResult;
            Assert.That(badRequestResult, Is.Not.Null);
            Assert.That(badRequestResult.StatusCode, Is.EqualTo(400));
        }

        [Test]
        public async Task OrderFromVault_MapsFieldsCorrectly()
        {
            // Arrange
            var productCode = Guid.NewGuid();
            var vaultOrder = new VaultOrderDto
            {
                CustomerEmail = "test@example.com",
                PlacedAt = 1705315800, // 2024-01-15 10:50:00 UTC
                DeliveryDetails = new VaultDeliveryDetailsDto
                {
                    ShippingLocation = new VaultLocationDto
                    {
                        AddressLine = "456 Shipping Ave",
                        CityName = "Manchester",
                        StateProvince = "Greater Manchester",
                        ZipPostal = "M1 1AA",
                        CountryCode = "GB"
                    }
                },
                Items = new List<VaultItemDto>
                {
                    new VaultItemDto { ProductCode = productCode, QuantityOrdered = 3, PricePerUnit = 49.99m }
                }
            };

            _mockProductRepository.Setup(x => x.GetByProductCodeAsync(productCode))
                .ReturnsAsync(new API.Models.Product { Id = 1, ProductCode = productCode });

            // Act
            var result = await _controller.OrderFromVault(vaultOrder);

            // Assert
            var okResult = result.Result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);

            var response = okResult.Value;
            var transformedOrderProperty = response.GetType().GetProperty("transformedOrder");
            var transformedOrder = transformedOrderProperty?.GetValue(response);

            // Check CustomerEmail
            var customerEmailProperty = transformedOrder?.GetType().GetProperty("CustomerEmail");
            Assert.That(customerEmailProperty?.GetValue(transformedOrder), Is.EqualTo("test@example.com"));

            // Check SupplierId
            var supplierIdProperty = transformedOrder?.GetType().GetProperty("SupplierId");
            Assert.That(supplierIdProperty?.GetValue(transformedOrder), Is.EqualTo(2), "SupplierId should be 2 for Vault");

            // Check SupplierName
            var supplierNameProperty = transformedOrder?.GetType().GetProperty("supplierName");
            Assert.That(supplierNameProperty?.GetValue(transformedOrder), Is.EqualTo("Vault"));
        }

        [Test]
        public async Task OrderFromVault_ConvertsUnixTimestamp()
        {
            // Arrange
            var productCode = Guid.NewGuid();
            var unixTimestamp = 1705315800; // 2024-01-15 10:50:00 UTC
            var expectedDateTime = new DateTime(2024, 1, 15, 10, 50, 0, DateTimeKind.Utc);

            var vaultOrder = new VaultOrderDto
            {
                CustomerEmail = "test@example.com",
                PlacedAt = unixTimestamp,
                Items = new List<VaultItemDto>
                {
                    new VaultItemDto { ProductCode = productCode, QuantityOrdered = 1, PricePerUnit = 10m }
                }
            };

            _mockProductRepository.Setup(x => x.GetByProductCodeAsync(productCode))
                .ReturnsAsync(new API.Models.Product { Id = 1, ProductCode = productCode });

            // Act
            var result = await _controller.OrderFromVault(vaultOrder);

            // Assert
            var okResult = result.Result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);

            var response = okResult.Value;
            var transformedOrderProperty = response.GetType().GetProperty("transformedOrder");
            var transformedOrder = transformedOrderProperty?.GetValue(response);
            
            var orderDateProperty = transformedOrder?.GetType().GetProperty("OrderDate");
            var orderDate = (DateTime?)orderDateProperty?.GetValue(transformedOrder);
            
            Assert.That(orderDate, Is.EqualTo(expectedDateTime));
            
            // Verify repository was called twice (validation + mapping)
            _mockProductRepository.Verify(x => x.GetByProductCodeAsync(productCode), Times.Exactly(2));
        }

        [Test]
        public async Task OrderFromVault_LogsWarning_WhenProductNotFound()
        {
            // Arrange
            var productCode = Guid.NewGuid();
            var vaultOrder = new VaultOrderDto
            {
                CustomerEmail = "test@example.com",
                PlacedAt = 1705315800,
                Items = new List<VaultItemDto>
                {
                    new VaultItemDto { ProductCode = productCode, QuantityOrdered = 1, PricePerUnit = 10m }
                }
            };

            _mockProductRepository.Setup(x => x.GetByProductCodeAsync(productCode))
                .ReturnsAsync((API.Models.Product)null);

            // Act
            await _controller.OrderFromVault(vaultOrder);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Product Code") && v.ToString().Contains("not found")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        #endregion

        #region GetSupportedSuppliers Tests

        [Test]
        public void GetSupportedSuppliers_ReturnsOkResult()
        {
            // Act
            var result = _controller.GetSupportedSuppliers();

            // Assert
            var okResult = result.Result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);
            Assert.That(okResult.StatusCode, Is.EqualTo(200));
        }

        [Test]
        public void GetSupportedSuppliers_ReturnsSupplierInformation()
        {
            // Act
            var result = _controller.GetSupportedSuppliers();

            // Assert
            var okResult = result.Result as OkObjectResult;
            Assert.That(okResult, Is.Not.Null);

            var response = okResult.Value;
            var suppliersProperty = response.GetType().GetProperty("suppliers");
            Assert.That(suppliersProperty, Is.Not.Null);

            var suppliers = suppliersProperty.GetValue(response) as Array;
            Assert.That(suppliers, Is.Not.Null);
            Assert.That(suppliers.Length, Is.GreaterThanOrEqualTo(2), "Should have at least Speedy and Vault");
        }

        [Test]
        public void GetSupportedSuppliers_IncludesSpeedyInformation()
        {
            // Act
            var result = _controller.GetSupportedSuppliers();

            // Assert
            var okResult = result.Result as OkObjectResult;
            var response = okResult.Value;
            var suppliersProperty = response.GetType().GetProperty("suppliers");
            var suppliers = suppliersProperty.GetValue(response) as Array;

            var speedySupplier = suppliers.GetValue(0);
            var nameProperty = speedySupplier.GetType().GetProperty("name");
            
            Assert.That(nameProperty?.GetValue(speedySupplier), Is.EqualTo("Speedy"));
        }

        [Test]
        public void GetSupportedSuppliers_IncludesVaultInformation()
        {
            // Act
            var result = _controller.GetSupportedSuppliers();

            // Assert
            var okResult = result.Result as OkObjectResult;
            var response = okResult.Value;
            var suppliersProperty = response.GetType().GetProperty("suppliers");
            var suppliers = suppliersProperty.GetValue(response) as Array;

            var vaultSupplier = suppliers.GetValue(1);
            var nameProperty = vaultSupplier.GetType().GetProperty("name");
            
            Assert.That(nameProperty?.GetValue(vaultSupplier), Is.EqualTo("Vault"));
        }

        #endregion
    }
}
