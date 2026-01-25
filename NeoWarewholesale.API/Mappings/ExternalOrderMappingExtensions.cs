using NeoWarewholesale.API.DTOs.External;
using NeoWarewholesale.API.Models;
using NeoWarewholesale.API.Repositories;

namespace NeoWarewholesale.API.Mappings
{
    /// <summary>
    /// Extension methods to map external supplier formats to internal Order model.
    /// Teaching point: Shows how to normalize different external formats into your domain model.
    /// </summary>
    public static class ExternalOrderMappingExtensions
    {
        /// <summary>
        /// Maps Speedy's order format to internal Order model.
        /// Automatically sets SupplierId to Speedy (Id = 1).
        /// Speedy uses numeric CustomerId and ProductId.
        /// </summary>
        public static Order ToOrder(this SpeedyOrderDto dto)
        {
            return new Order
            {
                // Speedy uses numeric CustomerId (required)
                CustomerId = dto.CustomerId,
                CustomerEmail = null, // Speedy doesn't provide email

                // Automatically set supplier to Speedy (Id = 1)
                SupplierId = 1, // Speedy

                // Map order date
                OrderDate = dto.OrderTimestamp,

                // Set default status for new orders
                OrderStatus = OrderStatus.Received,

                // Map billing address (Speedy calls it "BillTo")
                BillingAddress = dto.BillTo != null ? new Address
                {
                    Street = dto.BillTo.StreetAddress,
                    City = dto.BillTo.City,
                    County = dto.BillTo.Region, // Speedy calls it "Region"
                    PostalCode = dto.BillTo.PostCode,
                    Country = dto.BillTo.Country
                } : null,

                // Map delivery address (Speedy calls it "ShipTo")
                DeliveryAddress = dto.ShipTo != null ? new Address
                {
                    Street = dto.ShipTo.StreetAddress,
                    City = dto.ShipTo.City,
                    County = dto.ShipTo.Region, // Speedy calls it "Region"
                    PostalCode = dto.ShipTo.PostCode,
                    Country = dto.ShipTo.Country
                } : null,

                // Map line items - Speedy uses numeric ProductId
                OrderItems = dto.LineItems?.Select(item => new OrderItem
                {
                    ProductId = item.ProductId, // Direct mapping - both use long
                    Quantity = item.Qty,  // Speedy calls it "Qty"
                    Price = item.UnitPrice // Speedy calls it "UnitPrice"
                }).ToList() ?? new List<OrderItem>()
            };
        }

        /// <summary>
        /// Maps Vault's order format to internal Order model.
        /// Automatically sets SupplierId to Vault (Id = 2).
        /// Vault uses CustomerEmail and ProductCode (Guid).
        /// 
        /// TEACHING NOTE FOR AZURE FUNCTIONS:
        /// This method uses IProductRepository (direct DB access) for ProductCode lookup.
        /// In an Azure Function, replace repository calls with HTTP API calls:
        /// - Instead of: await productRepository.GetByProductCodeAsync(item.ProductCode)
        /// - Use: HTTP GET to /api/products/code/{productCode}
        /// </summary>
        public static async Task<Order> ToOrderAsync(this VaultOrderDto dto, IProductRepository productRepository)
        {
            // Convert Unix timestamp to DateTime
            var orderDate = DateTimeOffset.FromUnixTimeSeconds(dto.PlacedAt).UtcDateTime;

            // ============================================================================
            // Map items - need to look up ProductId by ProductCode
            // ============================================================================
            var orderItems = new List<OrderItem>();
            foreach (var item in dto.Items)
            {
                // ========================================================================
                // CURRENT APPROACH (Direct Repository/EF Core Access):
                // ========================================================================
                // Look up product by ProductCode (Guid) to get ProductId (long)
                var product = await productRepository.GetByProductCodeAsync(item.ProductCode);
                
                // ========================================================================
                // AZURE FUNCTIONS ALTERNATIVE - Replace repository with HTTP API call:
                // ========================================================================
                //
                // In Azure Functions, you don't have direct database access.
                // Instead, call the API endpoint to look up the product:
                //
                // var httpClient = new HttpClient(); // Reuse static instance
                // var apiBaseUrl = "https://your-api.azurewebsites.net";
                // 
                // // Call GET /api/products/code/{productCode}
                // var response = await httpClient.GetAsync(
                //     $"{apiBaseUrl}/api/products/code/{item.ProductCode}");
                // 
                // if (!response.IsSuccessStatusCode)
                // {
                //     // Product not found - handle error
                //     throw new Exception($"Product with code {item.ProductCode} not found");
                // }
                // 
                // var jsonContent = await response.Content.ReadAsStringAsync();
                // var product = JsonConvert.DeserializeObject<ProductDto>(jsonContent);
                //
                // ========================================================================
                // WHY USE API CALLS IN AZURE FUNCTIONS?
                // ========================================================================
                // 1. No direct database connection needed (stateless)
                // 2. Centralized validation logic in API
                // 3. Easier security management (API handles auth)
                // 4. Better separation of concerns
                // 5. Can scale Functions independently
                // ========================================================================
                
                if (product != null)
                {
                    orderItems.Add(new OrderItem
                    {
                        ProductId = product.Id, // Convert from Guid to long via lookup
                        Quantity = item.QuantityOrdered,
                        Price = item.PricePerUnit
                    });
                }
            }

            return new Order
            {
                // Vault uses email for customer identification
                CustomerId = null, // Vault doesn't provide numeric ID
                CustomerEmail = dto.CustomerEmail,

                // Automatically set supplier to Vault (Id = 2)
                SupplierId = 2, // Vault

                // Map order date (convert from Unix timestamp)
                OrderDate = orderDate,

                // Set default status for new orders
                OrderStatus = OrderStatus.Received,

                // Map billing address (Vault uses nested structure)
                BillingAddress = dto.DeliveryDetails?.BillingLocation != null ? new Address
                {
                    Street = dto.DeliveryDetails.BillingLocation.AddressLine,
                    City = dto.DeliveryDetails.BillingLocation.CityName,
                    County = dto.DeliveryDetails.BillingLocation.StateProvince,
                    PostalCode = dto.DeliveryDetails.BillingLocation.ZipPostal,
                    Country = dto.DeliveryDetails.BillingLocation.CountryCode
                } : null,

                // Map delivery address
                DeliveryAddress = dto.DeliveryDetails?.ShippingLocation != null ? new Address
                {
                    Street = dto.DeliveryDetails.ShippingLocation.AddressLine,
                    City = dto.DeliveryDetails.ShippingLocation.CityName,
                    County = dto.DeliveryDetails.ShippingLocation.StateProvince,
                    PostalCode = dto.DeliveryDetails.ShippingLocation.ZipPostal,
                    Country = dto.DeliveryDetails.ShippingLocation.CountryCode
                } : null,

                // Use the mapped items with ProductId looked up
                OrderItems = orderItems
            };
        }
    }
}


