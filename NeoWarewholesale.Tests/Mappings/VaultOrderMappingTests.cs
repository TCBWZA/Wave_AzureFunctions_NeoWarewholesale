using Moq;
using NeoWarewholesale.API.DTOs.External;
using NeoWarewholesale.API.Mappings;
using NeoWarewholesale.API.Models;
using NeoWarewholesale.API.Repositories;

namespace NeoWarewholesale.Tests.Mappings
{
    /// <summary>
    /// Tests for Vault order format to internal Order model mapping.
    /// Tests the ToOrderAsync() extension method with mocked product repository.
    /// </summary>
    [TestFixture]
    public class VaultOrderMappingTests
    {
        private Mock<IProductRepository> _mockProductRepository;

        [SetUp]
        public void Setup()
        {
            _mockProductRepository = new Mock<IProductRepository>();
        }

        [Test]
        public async Task ToOrderAsync_WithValidVaultOrder_MapsAllFieldsCorrectly()
        {
            // Arrange
            var productCode1 = Guid.NewGuid();
            var productCode2 = Guid.NewGuid();
            var unixTimestamp = 1705315800; // 2024-01-15 10:30:00 UTC

            _mockProductRepository.Setup(x => x.GetByProductCodeAsync(productCode1))
                .ReturnsAsync(new Product { Id = 1, ProductCode = productCode1, Name = "Product 1" });
            _mockProductRepository.Setup(x => x.GetByProductCodeAsync(productCode2))
                .ReturnsAsync(new Product { Id = 2, ProductCode = productCode2, Name = "Product 2" });

            var vaultOrder = new VaultOrderDto
            {
                CustomerEmail = "test@example.com",
                PlacedAt = unixTimestamp,
                Items = new List<VaultItemDto>
                {
                    new VaultItemDto
                    {
                        ProductCode = productCode1,
                        QuantityOrdered = 5,
                        PricePerUnit = 29.99m
                    },
                    new VaultItemDto
                    {
                        ProductCode = productCode2,
                        QuantityOrdered = 3,
                        PricePerUnit = 49.99m
                    }
                }
            };

            // Act
            var order = await vaultOrder.ToOrderAsync(_mockProductRepository.Object);

            // Assert
            Assert.That(order, Is.Not.Null);
            Assert.That(order.CustomerId, Is.Null, "Vault doesn't provide numeric CustomerId");
            Assert.That(order.CustomerEmail, Is.EqualTo("test@example.com"));
            Assert.That(order.SupplierId, Is.EqualTo(2), "SupplierId should be 2 for Vault");
            Assert.That(order.OrderStatus, Is.EqualTo(OrderStatus.Received));
        }

        [Test]
        public async Task ToOrderAsync_ConvertsUnixTimestamp_ToUtcDateTime()
        {
            // Arrange
            var productCode = Guid.NewGuid();
            var unixTimestamp = 1705315800; // 2024-01-15 10:50:00 UTC
            var expectedDateTime = new DateTime(2024, 1, 15, 10, 50, 0, DateTimeKind.Utc);

            _mockProductRepository.Setup(x => x.GetByProductCodeAsync(productCode))
                .ReturnsAsync(new Product { Id = 1, ProductCode = productCode, Name = "Product 1" });

            var vaultOrder = new VaultOrderDto
            {
                CustomerEmail = "test@example.com",
                PlacedAt = unixTimestamp,
                Items = new List<VaultItemDto>
                {
                    new VaultItemDto
                    {
                        ProductCode = productCode,
                        QuantityOrdered = 1,
                        PricePerUnit = 10m
                    }
                }
            };

            // Act
            var order = await vaultOrder.ToOrderAsync(_mockProductRepository.Object);

            // Assert
            Assert.That(order.OrderDate, Is.EqualTo(expectedDateTime));
        }

        [Test]
        public async Task ToOrderAsync_MapsBillingAddress_FromNestedStructure()
        {
            // Arrange
            var productCode = Guid.NewGuid();
            _mockProductRepository.Setup(x => x.GetByProductCodeAsync(productCode))
                .ReturnsAsync(new Product { Id = 1, ProductCode = productCode });

            var vaultOrder = new VaultOrderDto
            {
                CustomerEmail = "test@example.com",
                PlacedAt = 1705315800,
                DeliveryDetails = new VaultDeliveryDetailsDto
                {
                    BillingLocation = new VaultLocationDto
                    {
                        AddressLine = "123 Billing St",
                        CityName = "London",
                        StateProvince = "Greater London",
                        ZipPostal = "SW1A 1AA",
                        CountryCode = "GB"
                    }
                },
                Items = new List<VaultItemDto>
                {
                    new VaultItemDto
                    {
                        ProductCode = productCode,
                        QuantityOrdered = 1,
                        PricePerUnit = 10m
                    }
                }
            };

            // Act
            var order = await vaultOrder.ToOrderAsync(_mockProductRepository.Object);

            // Assert
            Assert.That(order.BillingAddress, Is.Not.Null);
            Assert.That(order.BillingAddress.Street, Is.EqualTo("123 Billing St"), "AddressLine should map to Street");
            Assert.That(order.BillingAddress.City, Is.EqualTo("London"), "CityName should map to City");
            Assert.That(order.BillingAddress.County, Is.EqualTo("Greater London"), "StateProvince should map to County");
            Assert.That(order.BillingAddress.PostalCode, Is.EqualTo("SW1A 1AA"), "ZipPostal should map to PostalCode");
            Assert.That(order.BillingAddress.Country, Is.EqualTo("GB"), "CountryCode should map to Country");
        }

        [Test]
        public async Task ToOrderAsync_MapsDeliveryAddress_FromNestedStructure()
        {
            // Arrange
            var productCode = Guid.NewGuid();
            _mockProductRepository.Setup(x => x.GetByProductCodeAsync(productCode))
                .ReturnsAsync(new Product { Id = 1, ProductCode = productCode });

            var vaultOrder = new VaultOrderDto
            {
                CustomerEmail = "test@example.com",
                PlacedAt = 1705315800,
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
                    new VaultItemDto
                    {
                        ProductCode = productCode,
                        QuantityOrdered = 1,
                        PricePerUnit = 10m
                    }
                }
            };

            // Act
            var order = await vaultOrder.ToOrderAsync(_mockProductRepository.Object);

            // Assert
            Assert.That(order.DeliveryAddress, Is.Not.Null);
            Assert.That(order.DeliveryAddress.Street, Is.EqualTo("456 Shipping Ave"));
            Assert.That(order.DeliveryAddress.City, Is.EqualTo("Manchester"));
            Assert.That(order.DeliveryAddress.County, Is.EqualTo("Greater Manchester"));
            Assert.That(order.DeliveryAddress.PostalCode, Is.EqualTo("M1 1AA"));
            Assert.That(order.DeliveryAddress.Country, Is.EqualTo("GB"));
        }

        [Test]
        public async Task ToOrderAsync_ResolvesProductCodes_ToProductIds()
        {
            // Arrange
            var productCode1 = Guid.NewGuid();
            var productCode2 = Guid.NewGuid();

            _mockProductRepository.Setup(x => x.GetByProductCodeAsync(productCode1))
                .ReturnsAsync(new Product { Id = 101, ProductCode = productCode1, Name = "Product A" });
            _mockProductRepository.Setup(x => x.GetByProductCodeAsync(productCode2))
                .ReturnsAsync(new Product { Id = 202, ProductCode = productCode2, Name = "Product B" });

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
                    },
                    new VaultItemDto
                    {
                        ProductCode = productCode2,
                        QuantityOrdered = 3,
                        PricePerUnit = 49.99m
                    }
                }
            };

            // Act
            var order = await vaultOrder.ToOrderAsync(_mockProductRepository.Object);

            // Assert
            Assert.That(order.OrderItems, Is.Not.Null);
            Assert.That(order.OrderItems.Count, Is.EqualTo(2));

            var firstItem = order.OrderItems[0];
            Assert.That(firstItem.ProductId, Is.EqualTo(101), "ProductCode should be resolved to ProductId");
            Assert.That(firstItem.Quantity, Is.EqualTo(5), "QuantityOrdered should map to Quantity");
            Assert.That(firstItem.Price, Is.EqualTo(29.99m), "PricePerUnit should map to Price");

            var secondItem = order.OrderItems[1];
            Assert.That(secondItem.ProductId, Is.EqualTo(202));
            Assert.That(secondItem.Quantity, Is.EqualTo(3));
            Assert.That(secondItem.Price, Is.EqualTo(49.99m));
        }

        [Test]
        public async Task ToOrderAsync_CallsRepository_ForEachProductCode()
        {
            // Arrange
            var productCode1 = Guid.NewGuid();
            var productCode2 = Guid.NewGuid();

            _mockProductRepository.Setup(x => x.GetByProductCodeAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new Product { Id = 1, ProductCode = Guid.NewGuid() });

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

            // Act
            await vaultOrder.ToOrderAsync(_mockProductRepository.Object);

            // Assert
            _mockProductRepository.Verify(x => x.GetByProductCodeAsync(productCode1), Times.Once);
            _mockProductRepository.Verify(x => x.GetByProductCodeAsync(productCode2), Times.Once);
        }

        [Test]
        public async Task ToOrderAsync_SkipsNullProducts_FromRepository()
        {
            // Arrange
            var productCode1 = Guid.NewGuid();
            var productCode2 = Guid.NewGuid();

            _mockProductRepository.Setup(x => x.GetByProductCodeAsync(productCode1))
                .ReturnsAsync(new Product { Id = 1, ProductCode = productCode1 });
            _mockProductRepository.Setup(x => x.GetByProductCodeAsync(productCode2))
                .ReturnsAsync((Product)null); // Product not found

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

            // Act
            var order = await vaultOrder.ToOrderAsync(_mockProductRepository.Object);

            // Assert
            Assert.That(order.OrderItems.Count, Is.EqualTo(1), "Only products that exist should be included");
            Assert.That(order.OrderItems[0].ProductId, Is.EqualTo(1));
        }

        [Test]
        public async Task ToOrderAsync_WithNullDeliveryDetails_SetsNullAddresses()
        {
            // Arrange
            var productCode = Guid.NewGuid();
            _mockProductRepository.Setup(x => x.GetByProductCodeAsync(productCode))
                .ReturnsAsync(new Product { Id = 1, ProductCode = productCode });

            var vaultOrder = new VaultOrderDto
            {
                CustomerEmail = "test@example.com",
                PlacedAt = 1705315800,
                DeliveryDetails = null,
                Items = new List<VaultItemDto>
                {
                    new VaultItemDto { ProductCode = productCode, QuantityOrdered = 1, PricePerUnit = 10m }
                }
            };

            // Act
            var order = await vaultOrder.ToOrderAsync(_mockProductRepository.Object);

            // Assert
            Assert.That(order.BillingAddress, Is.Null);
            Assert.That(order.DeliveryAddress, Is.Null);
        }

        [Test]
        public async Task ToOrderAsync_AlwaysSetsSupplierIdTo2()
        {
            // Arrange
            var productCode = Guid.NewGuid();
            _mockProductRepository.Setup(x => x.GetByProductCodeAsync(productCode))
                .ReturnsAsync(new Product { Id = 1, ProductCode = productCode });

            var vaultOrder = new VaultOrderDto
            {
                CustomerEmail = "test@example.com",
                PlacedAt = 1705315800,
                Items = new List<VaultItemDto>
                {
                    new VaultItemDto { ProductCode = productCode, QuantityOrdered = 1, PricePerUnit = 10m }
                }
            };

            // Act
            var order = await vaultOrder.ToOrderAsync(_mockProductRepository.Object);

            // Assert
            Assert.That(order.SupplierId, Is.EqualTo(2), "Vault orders should always have SupplierId = 2");
        }

        [Test]
        public async Task ToOrderAsync_AlwaysSetsOrderStatusToReceived()
        {
            // Arrange
            var productCode = Guid.NewGuid();
            _mockProductRepository.Setup(x => x.GetByProductCodeAsync(productCode))
                .ReturnsAsync(new Product { Id = 1, ProductCode = productCode });

            var vaultOrder = new VaultOrderDto
            {
                CustomerEmail = "test@example.com",
                PlacedAt = 1705315800,
                Items = new List<VaultItemDto>
                {
                    new VaultItemDto { ProductCode = productCode, QuantityOrdered = 1, PricePerUnit = 10m }
                }
            };

            // Act
            var order = await vaultOrder.ToOrderAsync(_mockProductRepository.Object);

            // Assert
            Assert.That(order.OrderStatus, Is.EqualTo(OrderStatus.Received), "New orders should always start with Received status");
        }

        [Test]
        public async Task ToOrderAsync_AlwaysSetsCustomerIdToNull()
        {
            // Arrange
            var productCode = Guid.NewGuid();
            _mockProductRepository.Setup(x => x.GetByProductCodeAsync(productCode))
                .ReturnsAsync(new Product { Id = 1, ProductCode = productCode });

            var vaultOrder = new VaultOrderDto
            {
                CustomerEmail = "test@example.com",
                PlacedAt = 1705315800,
                Items = new List<VaultItemDto>
                {
                    new VaultItemDto { ProductCode = productCode, QuantityOrdered = 1, PricePerUnit = 10m }
                }
            };

            // Act
            var order = await vaultOrder.ToOrderAsync(_mockProductRepository.Object);

            // Assert
            Assert.That(order.CustomerId, Is.Null, "Vault doesn't provide numeric CustomerId");
        }
    }
}
