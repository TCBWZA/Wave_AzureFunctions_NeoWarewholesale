using NeoWarewholesale.API.Models;

namespace NeoWarewholesale.Tests.Models
{
    /// <summary>
    /// Tests for Order model business logic and calculated properties.
    /// Tests behavior without database dependencies.
    /// </summary>
    [TestFixture]
    public class OrderModelTests
    {
        [Test]
        public void Order_TotalAmount_CalculatesCorrectly_WithMultipleItems()
        {
            // Arrange
            var order = new Order
            {
                OrderItems = new List<OrderItem>
                {
                    new OrderItem { Quantity = 5, Price = 29.99m }, // 149.95
                    new OrderItem { Quantity = 3, Price = 49.99m }, // 149.97
                    new OrderItem { Quantity = 2, Price = 10.00m }  // 20.00
                }
            };

            // Act
            var total = order.TotalAmount;

            // Assert
            Assert.That(total, Is.EqualTo(319.92m));
        }

        [Test]
        public void Order_TotalAmount_IsZero_WhenNoItems()
        {
            // Arrange
            var order = new Order
            {
                BillingAddress = new Address { Street = "123 Test St" },
                OrderItems = new List<OrderItem>()
            };

            // Act
            var total = order.TotalAmount;

            // Assert
            Assert.That(total, Is.EqualTo(0m));
        }

        [Test]
        public void Order_TotalAmount_IsZero_WhenOrderItemsIsNull()
        {
            // Arrange
            var order = new Order
            {
                OrderItems = null
            };

            // Act
            var total = order.TotalAmount;

            // Assert
            Assert.That(total, Is.EqualTo(0m));
        }

        [Test]
        public void Order_CanHaveNullCustomerId_WhenUsingEmail()
        {
            // Arrange & Act
            var order = new Order
            {
                CustomerId = null,
                CustomerEmail = "test@example.com",
                SupplierId = 2 // Vault uses email
            };

            // Assert
            Assert.That(order.CustomerId, Is.Null);
            Assert.That(order.CustomerEmail, Is.Not.Null);
        }

        [Test]
        public void Order_CanHaveNullCustomerEmail_WhenUsingId()
        {
            // Arrange & Act
            var order = new Order
            {
                CustomerId = 123,
                CustomerEmail = null,
                SupplierId = 1 // Speedy uses ID
            };

            // Assert
            Assert.That(order.CustomerId, Is.Not.Null);
            Assert.That(order.CustomerEmail, Is.Null);
        }

        [Test]
        public void OrderStatus_HasCorrectValues()
        {
            // Assert
            Assert.That((int)OrderStatus.Received, Is.EqualTo(0));
            Assert.That((int)OrderStatus.Picking, Is.EqualTo(1));
            Assert.That((int)OrderStatus.Dispatched, Is.EqualTo(2));
            Assert.That((int)OrderStatus.Delivered, Is.EqualTo(3));
        }

        [Test]
        public void Order_CanTransitionThroughStatuses()
        {
            // Arrange
            var order = new Order
            {
                OrderStatus = OrderStatus.Received
            };

            // Act & Assert
            Assert.That(order.OrderStatus, Is.EqualTo(OrderStatus.Received));

            order.OrderStatus = OrderStatus.Picking;
            Assert.That(order.OrderStatus, Is.EqualTo(OrderStatus.Picking));

            order.OrderStatus = OrderStatus.Dispatched;
            Assert.That(order.OrderStatus, Is.EqualTo(OrderStatus.Dispatched));

            order.OrderStatus = OrderStatus.Delivered;
            Assert.That(order.OrderStatus, Is.EqualTo(OrderStatus.Delivered));
        }
    }

    /// <summary>
    /// Tests for OrderItem model business logic.
    /// </summary>
    [TestFixture]
    public class OrderItemModelTests
    {
        [Test]
        public void OrderItem_CalculatesLineTotal_Correctly()
        {
            // Arrange
            var orderItem = new OrderItem
            {
                Quantity = 5,
                Price = 29.99m
            };

            // Act
            var lineTotal = orderItem.Quantity * orderItem.Price;

            // Assert
            Assert.That(lineTotal, Is.EqualTo(149.95m));
        }

        [Test]
        public void OrderItem_LineTotal_IsZero_WhenQuantityIsZero()
        {
            // Arrange
            var orderItem = new OrderItem
            {
                Quantity = 0,
                Price = 29.99m
            };

            // Act
            var lineTotal = orderItem.Quantity * orderItem.Price;

            // Assert
            Assert.That(lineTotal, Is.EqualTo(0m));
        }

        [Test]
        public void OrderItem_LineTotal_IsZero_WhenPriceIsZero()
        {
            // Arrange
            var orderItem = new OrderItem
            {
                Quantity = 5,
                Price = 0m
            };

            // Act
            var lineTotal = orderItem.Quantity * orderItem.Price;

            // Assert
            Assert.That(lineTotal, Is.EqualTo(0m));
        }

        [Test]
        public void OrderItem_HandlesDecimalPrecision()
        {
            // Arrange
            var orderItem = new OrderItem
            {
                Quantity = 3,
                Price = 33.33m
            };

            // Act
            var lineTotal = orderItem.Quantity * orderItem.Price;

            // Assert
            Assert.That(lineTotal, Is.EqualTo(99.99m));
        }
    }

    /// <summary>
    /// Tests for Address model validation.
    /// </summary>
    [TestFixture]
    public class AddressModelTests
    {
        [Test]
        public void Address_CanBeCreated_WithAllFields()
        {
            // Arrange & Act
            var address = new Address
            {
                Street = "123 Main St",
                City = "London",
                County = "Greater London",
                PostalCode = "SW1A 1AA",
                Country = "United Kingdom"
            };

            // Assert
            Assert.That(address.Street, Is.EqualTo("123 Main St"));
            Assert.That(address.City, Is.EqualTo("London"));
            Assert.That(address.County, Is.EqualTo("Greater London"));
            Assert.That(address.PostalCode, Is.EqualTo("SW1A 1AA"));
            Assert.That(address.Country, Is.EqualTo("United Kingdom"));
        }

        [Test]
        public void Address_CanHaveNullableFields()
        {
            // Arrange & Act
            var address = new Address
            {
                Street = "123 Main St",
                City = null,
                County = null,
                PostalCode = null,
                Country = null
            };

            // Assert
            Assert.That(address.Street, Is.Not.Null);
            Assert.That(address.City, Is.Null);
            Assert.That(address.County, Is.Null);
            Assert.That(address.PostalCode, Is.Null);
            Assert.That(address.Country, Is.Null);
        }
    }

    /// <summary>
    /// Tests for Product model.
    /// </summary>
    [TestFixture]
    public class ProductModelTests
    {
        [Test]
        public void Product_HasProductCode_AsGuid()
        {
            // Arrange
            var productCode = Guid.NewGuid();

            // Act
            var product = new Product
            {
                Id = 1,
                ProductCode = productCode,
                Name = "Test Product"
            };

            // Assert
            Assert.That(product.ProductCode, Is.EqualTo(productCode));
            Assert.That(product.ProductCode, Is.InstanceOf<Guid>());
        }

        [Test]
        public void Product_HasNumericId()
        {
            // Arrange & Act
            var product = new Product
            {
                Id = 123,
                ProductCode = Guid.NewGuid(),
                Name = "Test Product"
            };

            // Assert
            Assert.That(product.Id, Is.EqualTo(123));
            Assert.That(product.Id, Is.InstanceOf<long>());
        }
    }
}
