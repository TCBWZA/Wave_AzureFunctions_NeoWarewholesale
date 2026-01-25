using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using NeoWarewholesale.API.Controllers;
using NeoWarewholesale.API.DTOs.External;
using NeoWarewholesale.API.Models;
using NeoWarewholesale.API.Repositories;

namespace NeoWarewholesale.Tests.Controllers
{
    /// <summary>
    /// Tests for ExternalOrdersController create endpoints (speedycreate, vaultcreate).
    /// These endpoints validate data and save to database via repositories.
    /// </summary>
    [TestFixture]
    public class ExternalOrdersController_CreateTests
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

        #region SpeedyCreate Tests

        [Test]
        public async Task SpeedyCreate_WithValidOrder_CreatesOrderAndReturnsCreated()
        {
            // Arrange
            var speedyOrder = new SpeedyOrderDto
            {
                CustomerId = 123,
                OrderTimestamp = DateTime.UtcNow,
                LineItems = new List<SpeedyLineItemDto>
                {
                    new SpeedyLineItemDto { ProductId = 1, Qty = 5, UnitPrice = 29.99m }
                }
            };

            _mockCustomerRepository.Setup(x => x.ExistsAsync(123))
                .ReturnsAsync(true);
            _mockProductRepository.Setup(x => x.ExistsAsync(1))
                .ReturnsAsync(true);
            _mockOrderRepository.Setup(x => x.CreateAsync(It.IsAny<Order>()))
                .ReturnsAsync(new Order
                {
                    Id = 999,
                    CustomerId = 123,
                    SupplierId = 1,
                    OrderStatus = OrderStatus.Received,
                    OrderItems = new List<OrderItem>
                    {
                        new OrderItem { ProductId = 1, Quantity = 5, Price = 29.99m }
                    }
                });

            // Act
            var result = await _controller.SpeedyCreate(speedyOrder);

            // Assert
            Assert.That(result, Is.InstanceOf<ActionResult<object>>());
            var createdResult = result.Result as CreatedAtActionResult;
            Assert.That(createdResult, Is.Not.Null);
            Assert.That(createdResult.StatusCode, Is.EqualTo(201));
        }

        [Test]
        public async Task SpeedyCreate_ValidatesCustomerExists()
        {
            // Arrange
            var speedyOrder = new SpeedyOrderDto
            {
                CustomerId = 999, // Non-existent customer
                OrderTimestamp = DateTime.UtcNow,
                LineItems = new List<SpeedyLineItemDto>
                {
                    new SpeedyLineItemDto { ProductId = 1, Qty = 1, UnitPrice = 10m }
                }
            };

            _mockCustomerRepository.Setup(x => x.ExistsAsync(999))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.SpeedyCreate(speedyOrder);

            // Assert
            var badRequestResult = result.Result as BadRequestObjectResult;
            Assert.That(badRequestResult, Is.Not.Null);
            Assert.That(badRequestResult.StatusCode, Is.EqualTo(400));

            // Verify we didn't try to create the order
            _mockOrderRepository.Verify(x => x.CreateAsync(It.IsAny<Order>()), Times.Never);
        }

        [Test]
        public async Task SpeedyCreate_ValidatesAllProductsExist()
        {
            // Arrange
            var speedyOrder = new SpeedyOrderDto
            {
                CustomerId = 123,
                OrderTimestamp = DateTime.UtcNow,
                LineItems = new List<SpeedyLineItemDto>
                {
                    new SpeedyLineItemDto { ProductId = 1, Qty = 1, UnitPrice = 10m },
                    new SpeedyLineItemDto { ProductId = 999, Qty = 1, UnitPrice = 20m } // Non-existent
                }
            };

            _mockCustomerRepository.Setup(x => x.ExistsAsync(123))
                .ReturnsAsync(true);
            _mockProductRepository.Setup(x => x.ExistsAsync(1))
                .ReturnsAsync(true);
            _mockProductRepository.Setup(x => x.ExistsAsync(999))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.SpeedyCreate(speedyOrder);

            // Assert
            var badRequestResult = result.Result as BadRequestObjectResult;
            Assert.That(badRequestResult, Is.Not.Null);
            Assert.That(badRequestResult.StatusCode, Is.EqualTo(400));

            // Verify we didn't try to create the order
            _mockOrderRepository.Verify(x => x.CreateAsync(It.IsAny<Order>()), Times.Never);
        }

        [Test]
        public async Task SpeedyCreate_CallsOrderRepository_WithCorrectData()
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

            _mockCustomerRepository.Setup(x => x.ExistsAsync(123))
                .ReturnsAsync(true);
            _mockProductRepository.Setup(x => x.ExistsAsync(1))
                .ReturnsAsync(true);
            _mockOrderRepository.Setup(x => x.CreateAsync(It.IsAny<Order>()))
                .ReturnsAsync(new Order { Id = 999 });

            // Act
            await _controller.SpeedyCreate(speedyOrder);

            // Assert
            _mockOrderRepository.Verify(x => x.CreateAsync(It.Is<Order>(o =>
                o.CustomerId == 123 &&
                o.SupplierId == 1 &&
                o.OrderStatus == OrderStatus.Received &&
                o.OrderItems.Count == 1 &&
                o.OrderItems[0].ProductId == 1 &&
                o.OrderItems[0].Quantity == 5 &&
                o.OrderItems[0].Price == 29.99m
            )), Times.Once);
        }

        [Test]
        public async Task SpeedyCreate_ReturnsCorrectResponseStructure()
        {
            // Arrange
            var speedyOrder = new SpeedyOrderDto
            {
                CustomerId = 123,
                OrderTimestamp = DateTime.UtcNow,
                LineItems = new List<SpeedyLineItemDto>
                {
                    new SpeedyLineItemDto { ProductId = 1, Qty = 5, UnitPrice = 29.99m }
                }
            };

            _mockCustomerRepository.Setup(x => x.ExistsAsync(123))
                .ReturnsAsync(true);
            _mockProductRepository.Setup(x => x.ExistsAsync(1))
                .ReturnsAsync(true);
            _mockOrderRepository.Setup(x => x.CreateAsync(It.IsAny<Order>()))
                .ReturnsAsync(new Order
                {
                    Id = 999,
                    CustomerId = 123,
                    OrderDate = DateTime.UtcNow,
                    OrderStatus = OrderStatus.Received,
                    OrderItems = new List<OrderItem>
                    {
                        new OrderItem { Quantity = 5, Price = 29.99m }
                    }
                });

            // Act
            var result = await _controller.SpeedyCreate(speedyOrder);

            // Assert
            var createdResult = result.Result as CreatedAtActionResult;
            Assert.That(createdResult, Is.Not.Null);

            var response = createdResult.Value;
            Assert.That(response, Is.Not.Null);

            // Check expected properties
            var successProperty = response.GetType().GetProperty("success");
            var orderIdProperty = response.GetType().GetProperty("orderId");
            var supplierProperty = response.GetType().GetProperty("supplier");
            var orderReferenceProperty = response.GetType().GetProperty("orderReference");

            Assert.That(successProperty?.GetValue(response), Is.EqualTo(true));
            Assert.That(orderIdProperty?.GetValue(response), Is.EqualTo(999));
            Assert.That(supplierProperty?.GetValue(response), Is.EqualTo("Speedy"));
            
            var orderReference = orderReferenceProperty?.GetValue(response)?.ToString();
            Assert.That(orderReference, Does.StartWith("SPEEDY-"));
        }

        [Test]
        public async Task SpeedyCreate_LogsWarning_WhenCustomerNotFound()
        {
            // Arrange
            var speedyOrder = new SpeedyOrderDto
            {
                CustomerId = 999,
                OrderTimestamp = DateTime.UtcNow,
                LineItems = new List<SpeedyLineItemDto>
                {
                    new SpeedyLineItemDto { ProductId = 1, Qty = 1, UnitPrice = 10m }
                }
            };

            _mockCustomerRepository.Setup(x => x.ExistsAsync(999))
                .ReturnsAsync(false);

            // Act
            await _controller.SpeedyCreate(speedyOrder);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Customer ID") && v.ToString().Contains("not found")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Test]
        public async Task SpeedyCreate_LogsWarning_WhenProductNotFound()
        {
            // Arrange
            var speedyOrder = new SpeedyOrderDto
            {
                CustomerId = 123,
                OrderTimestamp = DateTime.UtcNow,
                LineItems = new List<SpeedyLineItemDto>
                {
                    new SpeedyLineItemDto { ProductId = 999, Qty = 1, UnitPrice = 10m }
                }
            };

            _mockCustomerRepository.Setup(x => x.ExistsAsync(123))
                .ReturnsAsync(true);
            _mockProductRepository.Setup(x => x.ExistsAsync(999))
                .ReturnsAsync(false);

            // Act
            await _controller.SpeedyCreate(speedyOrder);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Product ID") && v.ToString().Contains("not found")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Test]
        public async Task SpeedyCreate_LogsInformation_OnSuccess()
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

            _mockCustomerRepository.Setup(x => x.ExistsAsync(123))
                .ReturnsAsync(true);
            _mockProductRepository.Setup(x => x.ExistsAsync(1))
                .ReturnsAsync(true);
            _mockOrderRepository.Setup(x => x.CreateAsync(It.IsAny<Order>()))
                .ReturnsAsync(new Order { Id = 999 });

            // Act
            await _controller.SpeedyCreate(speedyOrder);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("created and saved successfully")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        #endregion

        #region VaultCreate Tests

        [Test]
        public async Task VaultCreate_WithValidOrder_CreatesOrderAndReturnsCreated()
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
                .ReturnsAsync(new Product { Id = 1, ProductCode = productCode, Name = "Test Product" });
            _mockOrderRepository.Setup(x => x.CreateAsync(It.IsAny<Order>()))
                .ReturnsAsync(new Order
                {
                    Id = 999,
                    CustomerEmail = "test@example.com",
                    SupplierId = 2,
                    OrderStatus = OrderStatus.Received,
                    OrderItems = new List<OrderItem>
                    {
                        new OrderItem { ProductId = 1, Quantity = 3, Price = 49.99m }
                    }
                });

            // Act
            var result = await _controller.VaultCreate(vaultOrder);

            // Assert
            var createdResult = result.Result as CreatedAtActionResult;
            Assert.That(createdResult, Is.Not.Null);
            Assert.That(createdResult.StatusCode, Is.EqualTo(201));
        }

        [Test]
        public async Task VaultCreate_ValidatesAllProductCodesExist()
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
                    new VaultItemDto { ProductCode = productCode1, QuantityOrdered = 1, PricePerUnit = 10m },
                    new VaultItemDto { ProductCode = productCode2, QuantityOrdered = 1, PricePerUnit = 20m }
                }
            };

            _mockProductRepository.Setup(x => x.GetByProductCodeAsync(productCode1))
                .ReturnsAsync(new Product { Id = 1, ProductCode = productCode1 });
            _mockProductRepository.Setup(x => x.GetByProductCodeAsync(productCode2))
                .ReturnsAsync((Product)null); // Second product doesn't exist

            // Act
            var result = await _controller.VaultCreate(vaultOrder);

            // Assert
            var badRequestResult = result.Result as BadRequestObjectResult;
            Assert.That(badRequestResult, Is.Not.Null);
            Assert.That(badRequestResult.StatusCode, Is.EqualTo(400));

            // Verify we didn't try to create the order
            _mockOrderRepository.Verify(x => x.CreateAsync(It.IsAny<Order>()), Times.Never);
        }

        [Test]
        public async Task VaultCreate_CallsOrderRepository_WithCorrectData()
        {
            // Arrange
            var productCode = Guid.NewGuid();
            var vaultOrder = new VaultOrderDto
            {
                CustomerEmail = "test@example.com",
                PlacedAt = 1705315800,
                Items = new List<VaultItemDto>
                {
                    new VaultItemDto { ProductCode = productCode, QuantityOrdered = 3, PricePerUnit = 49.99m }
                }
            };

            _mockProductRepository.Setup(x => x.GetByProductCodeAsync(productCode))
                .ReturnsAsync(new Product { Id = 1, ProductCode = productCode });
            _mockOrderRepository.Setup(x => x.CreateAsync(It.IsAny<Order>()))
                .ReturnsAsync(new Order { Id = 999 });

            // Act
            await _controller.VaultCreate(vaultOrder);

            // Assert
            _mockOrderRepository.Verify(x => x.CreateAsync(It.Is<Order>(o =>
                o.CustomerId == null &&
                o.CustomerEmail == "test@example.com" &&
                o.SupplierId == 2 &&
                o.OrderStatus == OrderStatus.Received &&
                o.OrderItems.Count == 1 &&
                o.OrderItems[0].ProductId == 1 &&
                o.OrderItems[0].Quantity == 3 &&
                o.OrderItems[0].Price == 49.99m
            )), Times.Once);
        }

        [Test]
        public async Task VaultCreate_ReturnsCorrectResponseStructure()
        {
            // Arrange
            var productCode = Guid.NewGuid();
            var vaultOrder = new VaultOrderDto
            {
                CustomerEmail = "test@example.com",
                PlacedAt = 1705315800,
                Items = new List<VaultItemDto>
                {
                    new VaultItemDto { ProductCode = productCode, QuantityOrdered = 3, PricePerUnit = 49.99m }
                }
            };

            _mockProductRepository.Setup(x => x.GetByProductCodeAsync(productCode))
                .ReturnsAsync(new Product { Id = 1, ProductCode = productCode, Name = "Test Product" });
            _mockOrderRepository.Setup(x => x.CreateAsync(It.IsAny<Order>()))
                .ReturnsAsync(new Order
                {
                    Id = 999,
                    CustomerEmail = "test@example.com",
                    OrderDate = DateTime.UtcNow,
                    OrderStatus = OrderStatus.Received,
                    OrderItems = new List<OrderItem>
                    {
                        new OrderItem { Quantity = 3, Price = 49.99m }
                    }
                });

            // Act
            var result = await _controller.VaultCreate(vaultOrder);

            // Assert
            var createdResult = result.Result as CreatedAtActionResult;
            Assert.That(createdResult, Is.Not.Null);

            var response = createdResult.Value;
            Assert.That(response, Is.Not.Null);

            // Check expected properties
            var successProperty = response.GetType().GetProperty("success");
            var orderIdProperty = response.GetType().GetProperty("orderId");
            var supplierProperty = response.GetType().GetProperty("supplier");
            var orderReferenceProperty = response.GetType().GetProperty("orderReference");
            var productResolutionsProperty = response.GetType().GetProperty("productResolutions");

            Assert.That(successProperty?.GetValue(response), Is.EqualTo(true));
            Assert.That(orderIdProperty?.GetValue(response), Is.EqualTo(999));
            Assert.That(supplierProperty?.GetValue(response), Is.EqualTo("Vault"));
            
            var orderReference = orderReferenceProperty?.GetValue(response)?.ToString();
            Assert.That(orderReference, Does.StartWith("VAULT-"));
            
            Assert.That(productResolutionsProperty, Is.Not.Null, "Should include product resolution information");
        }

        [Test]
        public async Task VaultCreate_IncludesProductResolutionInResponse()
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
                .ReturnsAsync(new Product { Id = 101, ProductCode = productCode, Name = "Test Product" });
            _mockOrderRepository.Setup(x => x.CreateAsync(It.IsAny<Order>()))
                .ReturnsAsync(new Order { Id = 999, OrderItems = new List<OrderItem>() });

            // Act
            var result = await _controller.VaultCreate(vaultOrder);

            // Assert
            var createdResult = result.Result as CreatedAtActionResult;
            var response = createdResult.Value;
            
            var productResolutionsProperty = response.GetType().GetProperty("productResolutions");
            var productResolutions = productResolutionsProperty?.GetValue(response);
            
            Assert.That(productResolutions, Is.Not.Null);
        }

        [Test]
        public async Task VaultCreate_LogsWarning_WhenProductCodeNotFound()
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
                .ReturnsAsync((Product)null);

            // Act
            await _controller.VaultCreate(vaultOrder);

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

        [Test]
        public async Task VaultCreate_LogsInformation_OnSuccess()
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
                .ReturnsAsync(new Product { Id = 1, ProductCode = productCode });
            _mockOrderRepository.Setup(x => x.CreateAsync(It.IsAny<Order>()))
                .ReturnsAsync(new Order { Id = 999, OrderItems = new List<OrderItem>() });

            // Act
            await _controller.VaultCreate(vaultOrder);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("created and saved successfully")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        #endregion

        #region Error Handling Tests

        [Test]
        public async Task SpeedyCreate_WithException_Returns500()
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

            _mockCustomerRepository.Setup(x => x.ExistsAsync(It.IsAny<long>()))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.SpeedyCreate(speedyOrder);

            // Assert
            var statusCodeResult = result.Result as ObjectResult;
            Assert.That(statusCodeResult, Is.Not.Null);
            Assert.That(statusCodeResult.StatusCode, Is.EqualTo(500));

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Test]
        public async Task VaultCreate_WithException_Returns500()
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

            _mockProductRepository.Setup(x => x.GetByProductCodeAsync(It.IsAny<Guid>()))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.VaultCreate(vaultOrder);

            // Assert
            var statusCodeResult = result.Result as ObjectResult;
            Assert.That(statusCodeResult, Is.Not.Null);
            Assert.That(statusCodeResult.StatusCode, Is.EqualTo(500));

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        #endregion
    }
}
