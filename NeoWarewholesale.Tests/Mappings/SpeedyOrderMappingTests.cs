using NeoWarewholesale.API.DTOs.External;
using NeoWarewholesale.API.Mappings;
using NeoWarewholesale.API.Models;

namespace NeoWarewholesale.Tests.Mappings
{
    /// <summary>
    /// Tests for Speedy order format to internal Order model mapping.
    /// Tests the ToOrder() extension method without database dependencies.
    /// </summary>
    [TestFixture]
    public class SpeedyOrderMappingTests
    {
        [Test]
        public void ToOrder_WithValidSpeedyOrder_MapsAllFieldsCorrectly()
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
                ShipTo = new SpeedyAddressDto
                {
                    StreetAddress = "456 Shipping Ave",
                    City = "Manchester",
                    Region = "Greater Manchester",
                    PostCode = "M1 1AA",
                    Country = "United Kingdom"
                },
                LineItems = new List<SpeedyLineItemDto>
                {
                    new SpeedyLineItemDto
                    {
                        ProductId = 1,
                        Qty = 5,
                        UnitPrice = 29.99m
                    },
                    new SpeedyLineItemDto
                    {
                        ProductId = 2,
                        Qty = 3,
                        UnitPrice = 49.99m
                    }
                }
            };

            // Act
            var order = speedyOrder.ToOrder();

            // Assert
            Assert.That(order, Is.Not.Null);
            Assert.That(order.CustomerId, Is.EqualTo(123));
            Assert.That(order.CustomerEmail, Is.Null);
            Assert.That(order.SupplierId, Is.EqualTo(1), "SupplierId should be 1 for Speedy");
            Assert.That(order.OrderDate, Is.EqualTo(new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc)));
            Assert.That(order.OrderStatus, Is.EqualTo(OrderStatus.Received));
        }

        [Test]
        public void ToOrder_MapsBillingAddress_Correctly()
        {
            // Arrange
            var speedyOrder = new SpeedyOrderDto
            {
                CustomerId = 123,
                OrderTimestamp = DateTime.UtcNow,
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
                    new SpeedyLineItemDto { ProductId = 1, Qty = 1, UnitPrice = 10m }
                }
            };

            // Act
            var order = speedyOrder.ToOrder();

            // Assert
            Assert.That(order.BillingAddress, Is.Not.Null);
            Assert.That(order.BillingAddress.Street, Is.EqualTo("123 Billing St"));
            Assert.That(order.BillingAddress.City, Is.EqualTo("London"));
            Assert.That(order.BillingAddress.County, Is.EqualTo("Greater London"), "Region should map to County");
            Assert.That(order.BillingAddress.PostalCode, Is.EqualTo("SW1A 1AA"));
            Assert.That(order.BillingAddress.Country, Is.EqualTo("United Kingdom"));
        }

        [Test]
        public void ToOrder_MapsDeliveryAddress_Correctly()
        {
            // Arrange
            var speedyOrder = new SpeedyOrderDto
            {
                CustomerId = 123,
                OrderTimestamp = DateTime.UtcNow,
                ShipTo = new SpeedyAddressDto
                {
                    StreetAddress = "456 Shipping Ave",
                    City = "Manchester",
                    Region = "Greater Manchester",
                    PostCode = "M1 1AA",
                    Country = "United Kingdom"
                },
                LineItems = new List<SpeedyLineItemDto>
                {
                    new SpeedyLineItemDto { ProductId = 1, Qty = 1, UnitPrice = 10m }
                }
            };

            // Act
            var order = speedyOrder.ToOrder();

            // Assert
            Assert.That(order.DeliveryAddress, Is.Not.Null);
            Assert.That(order.DeliveryAddress.Street, Is.EqualTo("456 Shipping Ave"));
            Assert.That(order.DeliveryAddress.City, Is.EqualTo("Manchester"));
            Assert.That(order.DeliveryAddress.County, Is.EqualTo("Greater Manchester"));
            Assert.That(order.DeliveryAddress.PostalCode, Is.EqualTo("M1 1AA"));
            Assert.That(order.DeliveryAddress.Country, Is.EqualTo("United Kingdom"));
        }

        [Test]
        public void ToOrder_MapsLineItems_Correctly()
        {
            // Arrange
            var speedyOrder = new SpeedyOrderDto
            {
                CustomerId = 123,
                OrderTimestamp = DateTime.UtcNow,
                LineItems = new List<SpeedyLineItemDto>
                {
                    new SpeedyLineItemDto
                    {
                        ProductId = 1,
                        Qty = 5,
                        UnitPrice = 29.99m
                    },
                    new SpeedyLineItemDto
                    {
                        ProductId = 2,
                        Qty = 3,
                        UnitPrice = 49.99m
                    }
                }
            };

            // Act
            var order = speedyOrder.ToOrder();

            // Assert
            Assert.That(order.OrderItems, Is.Not.Null);
            Assert.That(order.OrderItems.Count, Is.EqualTo(2));
            
            var firstItem = order.OrderItems[0];
            Assert.That(firstItem.ProductId, Is.EqualTo(1));
            Assert.That(firstItem.Quantity, Is.EqualTo(5), "Qty should map to Quantity");
            Assert.That(firstItem.Price, Is.EqualTo(29.99m), "UnitPrice should map to Price");

            var secondItem = order.OrderItems[1];
            Assert.That(secondItem.ProductId, Is.EqualTo(2));
            Assert.That(secondItem.Quantity, Is.EqualTo(3));
            Assert.That(secondItem.Price, Is.EqualTo(49.99m));
        }

        [Test]
        public void ToOrder_WithNullBillTo_SetsNullBillingAddress()
        {
            // Arrange
            var speedyOrder = new SpeedyOrderDto
            {
                CustomerId = 123,
                OrderTimestamp = DateTime.UtcNow,
                BillTo = null,
                LineItems = new List<SpeedyLineItemDto>
                {
                    new SpeedyLineItemDto { ProductId = 1, Qty = 1, UnitPrice = 10m }
                }
            };

            // Act
            var order = speedyOrder.ToOrder();

            // Assert
            Assert.That(order.BillingAddress, Is.Null);
        }

        [Test]
        public void ToOrder_WithNullShipTo_SetsNullDeliveryAddress()
        {
            // Arrange
            var speedyOrder = new SpeedyOrderDto
            {
                CustomerId = 123,
                OrderTimestamp = DateTime.UtcNow,
                ShipTo = null,
                LineItems = new List<SpeedyLineItemDto>
                {
                    new SpeedyLineItemDto { ProductId = 1, Qty = 1, UnitPrice = 10m }
                }
            };

            // Act
            var order = speedyOrder.ToOrder();

            // Assert
            Assert.That(order.DeliveryAddress, Is.Null);
        }

        [Test]
        public void ToOrder_WithEmptyLineItems_CreatesEmptyOrderItems()
        {
            // Arrange
            var speedyOrder = new SpeedyOrderDto
            {
                CustomerId = 123,
                OrderTimestamp = DateTime.UtcNow,
                LineItems = new List<SpeedyLineItemDto>()
            };

            // Act
            var order = speedyOrder.ToOrder();

            // Assert
            Assert.That(order.OrderItems, Is.Not.Null);
            Assert.That(order.OrderItems.Count, Is.EqualTo(0));
        }

        [Test]
        public void ToOrder_WithNullLineItems_CreatesEmptyOrderItems()
        {
            // Arrange
            var speedyOrder = new SpeedyOrderDto
            {
                CustomerId = 123,
                OrderTimestamp = DateTime.UtcNow,
                LineItems = null
            };

            // Act
            var order = speedyOrder.ToOrder();

            // Assert
            Assert.That(order.OrderItems, Is.Not.Null);
            Assert.That(order.OrderItems.Count, Is.EqualTo(0));
        }

        [Test]
        public void ToOrder_AlwaysSetsSupplierIdTo1()
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

            // Act
            var order = speedyOrder.ToOrder();

            // Assert
            Assert.That(order.SupplierId, Is.EqualTo(1), "Speedy orders should always have SupplierId = 1");
        }

        [Test]
        public void ToOrder_AlwaysSetsOrderStatusToReceived()
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
            var order = speedyOrder.ToOrder();

            // Assert
            Assert.That(order.OrderStatus, Is.EqualTo(OrderStatus.Received), "New orders should always start with Received status");
        }

        [Test]
        public void ToOrder_AlwaysSetsCustomerEmailToNull()
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
            var order = speedyOrder.ToOrder();

            // Assert
            Assert.That(order.CustomerEmail, Is.Null, "Speedy doesn't provide email, so it should be null");
        }
    }
}
