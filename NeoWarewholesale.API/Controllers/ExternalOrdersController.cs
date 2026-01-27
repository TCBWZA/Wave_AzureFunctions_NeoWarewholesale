using NeoWarewholesale.API.DTOs.External;
using NeoWarewholesale.API.Mappings;
using NeoWarewholesale.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace NeoWarewholesale.API.Controllers
{
    /// <summary>
    /// External Orders Controller - Teaching Framework for Azure Functions
    /// 
    /// This controller demonstrates how to accept orders from different suppliers
    /// with different data formats and normalize them into your internal Order model.
    /// 
    /// Teaching Points:
    /// 1. Different suppliers have different API formats
    /// 2. Mapping external formats to internal domain models
    /// 3. Automatically setting supplier based on endpoint
    /// 4. This pattern can be converted to Azure Functions (one function per supplier)
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ExternalOrdersController(
        IOrderRepository orderRepository,
        ICustomerRepository customerRepository,
        IProductRepository productRepository,
        ILogger<ExternalOrdersController> logger) : ControllerBase
    {
        private readonly IOrderRepository _orderRepository = orderRepository;
        private readonly ICustomerRepository _customerRepository = customerRepository;
        private readonly IProductRepository _productRepository = productRepository;
        private readonly ILogger<ExternalOrdersController> _logger = logger;

        /// <summary>
        /// POST /api/externalorders/fromspeedy
        /// 
        /// Accepts orders in Speedy's format and converts to internal Order model.
        /// DOES NOT SAVE - only returns the transformed Order object as JSON.
        /// 
        /// Teaching Point: Shows data transformation without persistence.
        /// Use /speedycreate to actually save the order.
        /// </summary>
        /// <remarks>
        /// Example Request Body:
        /// {
        ///   "customerId": 1,
        ///   "orderTimestamp": "2024-01-15T10:30:00Z",
        ///   "shipTo": {
        ///     "streetAddress": "123 Main St",
        ///     "city": "London",
        ///     "region": "Greater London",
        ///     "postCode": "SW1A 1AA",
        ///     "country": "United Kingdom"
        ///   },
        ///   "lineItems": [
        ///     {
        ///       "productId": 1,
        ///       "qty": 5,
        ///       "unitPrice": 29.99
        ///     }
        ///   ]
        /// }
        /// </remarks>
        /// <response code="200">Order transformed successfully. Returns the internal Order representation.</response>
        /// <response code="400">Invalid request body or validation failed.</response>
        [HttpPost("fromspeedy")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<object>> OrderFromSpeedy([FromBody] SpeedyOrderDto speedyOrder)
        {
            _logger.LogInformation("Transforming order from Speedy format (no save)");

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                // Step 1: Map external format to internal Order model
                // The ToOrder() extension automatically sets SupplierId = 1 (Speedy)
                var order = speedyOrder.ToOrder();

                _logger.LogInformation("Successfully transformed Speedy order to internal format");

                // Step 2: Return the transformed Order object as JSON
                return Ok(new
                {
                    message = "Order transformed successfully (not saved)",
                    transformedOrder = new
                    {
                        order.CustomerId,
                        order.CustomerEmail,
                        order.SupplierId,
                        supplierName = "Speedy",
                        order.OrderDate,
                        order.OrderStatus,
                        order.BillingAddress,
                        order.DeliveryAddress,
                        orderItems = order.OrderItems?.Select(item => new
                        {
                            item.ProductId,
                            item.Quantity,
                            item.Price,
                            lineTotal = item.Quantity * item.Price
                        }).ToList(),
                        totalAmount = order.OrderItems?.Sum(i => i.Quantity * i.Price) ?? 0,
                        itemCount = order.OrderItems?.Count ?? 0
                    },
                    note = "Use POST /api/externalorders/speedycreate to save this order to the database"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error transforming Speedy order");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error transforming order from Speedy",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// POST /api/externalorders/fromvault
        /// 
        /// Accepts orders in Vault's format and converts to internal Order model.
        /// DOES NOT SAVE - only returns the transformed Order object as JSON.
        /// 
        /// Teaching Point: Shows data transformation with ProductCode lookup without persistence.
        /// Use /vaultcreate to actually save the order.
        /// 
        /// Example Request Body:
        /// {
        ///   "customerEmail": "user@example.com",
        ///   "placedAt": 1705315800,
        ///   "deliveryDetails": {
        ///     "billingLocation": {
        ///       "addressLine": "456 High Street",
        ///       "cityName": "Manchester",
        ///       "stateProvince": "Greater Manchester",
        ///       "zipPostal": "M1 1AA",
        ///       "countryCode": "GB"
        ///     },
        ///     "shippingLocation": {
        ///       "addressLine": "456 High Street",
        ///       "cityName": "Manchester",
        ///       "stateProvince": "Greater Manchester",
        ///       "zipPostal": "M1 1AA",
        ///       "countryCode": "GB"
        ///     }
        ///   },
        ///   "items": [
        ///     {
        ///       "productCode": "550e8400-e29b-41d4-a716-446655440000",
        ///       "quantityOrdered": 3,
        ///       "pricePerUnit": 49.99
        ///     }
        ///   ],
        ///   "fulfillmentInstructions": "Handle with care"
        /// }
        /// </summary>
        [HttpPost("fromvault")]
        public async Task<ActionResult<object>> OrderFromVault([FromBody] VaultOrderDto vaultOrder)
        {
            _logger.LogInformation("Transforming order from Vault format (no save)");

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                // Step 1: Validate all products exist by ProductCode BEFORE mapping
                // This is important because Vault uses ProductCode (Guid)
                var productLookups = new List<object>();
                foreach (var item in vaultOrder.Items)
                {
                    var product = await _productRepository.GetByProductCodeAsync(item.ProductCode);
                    if (product == null)
                    {
                        _logger.LogWarning("Product Code {ProductCode} from Vault not found", item.ProductCode);
                        return BadRequest($"Product with code {item.ProductCode} does not exist.");
                    }
                    productLookups.Add(new
                    {
                        vaultProductCode = item.ProductCode,
                        resolvedProductId = product.Id,
                        productName = product.Name
                    });
                }

                // Step 2: Map external format to internal Order model
                // The ToOrderAsync() extension automatically sets SupplierId = 2 (Vault)
                // and looks up ProductId by ProductCode
                var order = await vaultOrder.ToOrderAsync(_productRepository);

                _logger.LogInformation("Successfully transformed Vault order to internal format");

                // Step 3: Return the transformed Order object as JSON
                return Ok(new
                {
                    message = "Order transformed successfully (not saved)",
                    productCodeResolution = productLookups,
                    transformedOrder = new
                    {
                        order.CustomerId,
                        order.CustomerEmail,
                        order.SupplierId,
                        supplierName = "Vault",
                        order.OrderDate,
                        order.OrderStatus,
                        order.BillingAddress,
                        order.DeliveryAddress,
                        orderItems = order.OrderItems?.Select(item => new
                        {
                            item.ProductId,
                            item.Quantity,
                            item.Price,
                            lineTotal = item.Quantity * item.Price
                        }).ToList(),
                        totalAmount = order.OrderItems?.Sum(i => i.Quantity * i.Price) ?? 0,
                        itemCount = order.OrderItems?.Count ?? 0
                    },
                    note = "Use POST /api/externalorders/vaultcreate to save this order to the database"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error transforming Vault order");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error transforming order from Vault",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// POST /api/externalorders/speedycreate
        /// 
        /// Accepts orders in Speedy's format, transforms to internal model, 
        /// and SAVES using Entity Framework.
        /// 
        /// Teaching Point: Shows full integration with data persistence using EF Core.
        /// Validates data, transforms format, and persists to database.
        /// 
        /// This demonstrates the complete workflow that would be in an Azure Function.
        /// </summary>
        /// <response code="201">Order created and saved successfully. Returns order details with generated ID.</response>
        /// <response code="400">Validation failed. Customer or products don't exist, or invalid request body.</response>
        /// <response code="500">Internal server error during order processing.</response>
        [HttpPost("speedycreate")]
        [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<object>> SpeedyCreate([FromBody] SpeedyOrderDto speedyOrder)
        {
            _logger.LogInformation("Creating and saving order from Speedy supplier");

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                // Step 1: Map external format to internal Order model
                var order = speedyOrder.ToOrder();

                // Step 2: Validate customer exists (CustomerId is required for Speedy)
                if (!await _customerRepository.ExistsAsync(order.CustomerId!.Value))
                {
                    _logger.LogWarning("Customer ID {CustomerId} from Speedy not found", order.CustomerId);
                    return BadRequest(new
                    {
                        success = false,
                        error = $"Customer with ID {order.CustomerId} does not exist."
                    });
                }

                // Step 3: Validate all products exist
                foreach (var item in order.OrderItems!)
                {
                    if (!await _productRepository.ExistsAsync(item.ProductId))
                    {
                        _logger.LogWarning("Product ID {ProductId} from Speedy not found", item.ProductId);
                        return BadRequest(new
                        {
                            success = false,
                            error = $"Product with ID {item.ProductId} does not exist."
                        });
                    }
                }

                // ============================================================================
                // Step 4: Save the order using Entity Framework (via repository)
                // ============================================================================
                // 
                // CURRENT APPROACH (Direct EF Core Access):
                // This approach uses the repository pattern to directly save to the database.
                // Best for: Controller within the same application as the database.
                //
                var created = await _orderRepository.CreateAsync(order);
                
                // ============================================================================
                // AZURE FUNCTIONS ALTERNATIVE - Replace the above line with HTTP API call:
                // ============================================================================
                //
                // For Azure Functions that don't have direct DB access, use HttpClient to call
                // the standard /api/orders endpoint:
                //
                // // 1. Create HttpClient (reuse static instance in production)
                // var httpClient = new HttpClient();
                // var apiBaseUrl = "https://your-api.azurewebsites.net"; // or from config
                // 
                // // 2. Convert Order to CreateOrderDto
                // var createOrderDto = new CreateOrderDto
                // {
                //     CustomerId = order.CustomerId,
                //     CustomerEmail = order.CustomerEmail,
                //     SupplierId = order.SupplierId,
                //     OrderDate = order.OrderDate,
                //     OrderStatus = order.OrderStatus,
                //     BillingAddress = order.BillingAddress != null ? new AddressDto
                //     {
                //         Street = order.BillingAddress.Street,
                //         City = order.BillingAddress.City,
                //         County = order.BillingAddress.County,
                //         PostalCode = order.BillingAddress.PostalCode,
                //         Country = order.BillingAddress.Country
                //     } : null,
                //     DeliveryAddress = order.DeliveryAddress != null ? new AddressDto
                //     {
                //         Street = order.DeliveryAddress.Street,
                //         City = order.DeliveryAddress.City,
                //         County = order.DeliveryAddress.County,
                //         PostalCode = order.DeliveryAddress.PostalCode,
                //         Country = order.DeliveryAddress.Country
                //     } : null,
                //     OrderItems = order.OrderItems?.Select(item => new CreateOrderItemDto
                //     {
                //         ProductId = item.ProductId,
                //         Quantity = item.Quantity,
                //         Price = item.Price
                //     }).ToList()
                // };
                // 
                // // 3. Serialize to JSON and call API
                // var json = JsonConvert.SerializeObject(createOrderDto);
                // var content = new StringContent(json, Encoding.UTF8, "application/json");
                // var response = await httpClient.PostAsync($"{apiBaseUrl}/api/orders", content);
                // 
                // // 4. Handle response
                // if (!response.IsSuccessStatusCode)
                // {
                //     var errorContent = await response.Content.ReadAsStringAsync();
                //     _logger.LogError("API call failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
                //     return StatusCode((int)response.StatusCode, new { error = "Failed to create order via API" });
                // }
                // 
                // // 5. Deserialize created order
                // var responseContent = await response.Content.ReadAsStringAsync();
                // var created = JsonConvert.DeserializeObject<OrderDto>(responseContent);
                //
                // ============================================================================
                // WHEN TO USE WHICH APPROACH:
                // ============================================================================
                // - Use EF Core (current): When controller and DB are in same app/context
                // - Use API call: For Azure Functions, microservices, or external integrations
                // ============================================================================
                
                _logger.LogInformation("Order {OrderId} from Speedy created and saved successfully", created.Id);

                // Step 5: Return success response with saved order details
                return CreatedAtAction(
                    nameof(GetSupportedSuppliers), // Placeholder action
                    new { id = created.Id },
                    new
                    {
                        success = true,
                        orderId = created.Id,
                        message = "Order from Speedy created and saved successfully",
                        supplier = "Speedy",
                        supplierId = 1,
                        orderReference = $"SPEEDY-{created.Id}",
                        totalAmount = created.TotalAmount,
                        itemCount = created.OrderItems?.Count ?? 0,
                        orderDate = created.OrderDate,
                        orderStatus = created.OrderStatus.ToString()
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Speedy order");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error creating order from Speedy",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// POST /api/externalorders/vaultcreate
        /// 
        /// Accepts orders in Vault's format, transforms to internal model (including ProductCode lookup),
        /// and SAVES using Entity Framework.
        /// 
        /// Teaching Point: Shows full integration with complex data transformation.
        /// Demonstrates ProductCode → ProductId resolution and async operations with EF Core.
        /// 
        /// This demonstrates a more complex workflow suitable for Azure Functions with database access.
        /// </summary>
        /// <response code="201">Order created and saved successfully. Returns order details with ProductCode resolution info.</response>
        /// <response code="400">Validation failed. ProductCodes don't exist or invalid request body.</response>
        /// <response code="500">Internal server error during order processing.</response>
        [HttpPost("vaultcreate")]
        [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<object>> VaultCreate([FromBody] VaultOrderDto vaultOrder)
        {
            _logger.LogInformation("Creating and saving order from Vault supplier");

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                // Step 1: Validate all products exist by ProductCode BEFORE mapping
                var productValidations = new List<object>();
                foreach (var item in vaultOrder.Items)
                {
                    var product = await _productRepository.GetByProductCodeAsync(item.ProductCode);
                    if (product == null)
                    {
                        _logger.LogWarning("Product Code {ProductCode} from Vault not found", item.ProductCode);
                        return BadRequest(new
                        {
                            success = false,
                            error = $"Product with code {item.ProductCode} does not exist."
                        });
                    }
                    productValidations.Add(new
                    {
                        productCode = item.ProductCode,
                        resolvedProductId = product.Id,
                        productName = product.Name
                    });
                }


                // Step 2: Map external format to internal Order model
                // This performs ProductCode ? ProductId lookup
                var order = await vaultOrder.ToOrderAsync(_productRepository);

                // ============================================================================
                // Step 3: Save the order using Entity Framework (via repository)
                // ============================================================================
                // 
                // CURRENT APPROACH (Direct EF Core Access):
                // This approach uses the repository pattern to directly save to the database.
                // Best for: Controller within the same application as the database.
                //
                var created = await _orderRepository.CreateAsync(order);
                
                // ============================================================================
                // AZURE FUNCTIONS ALTERNATIVE - Replace the above line with HTTP API call:
                // ============================================================================
                //
                // For Azure Functions that don't have direct DB access, use HttpClient to call
                // the standard /api/orders endpoint. This is especially useful for Vault since
                // ProductCode ? ProductId resolution is already done in Step 1 & 2.
                //
                // // 1. Create HttpClient (reuse static instance in production)
                // var httpClient = new HttpClient();
                // var apiBaseUrl = "https://your-api.azurewebsites.net"; // or from config
                // 
                // // 2. Convert Order to CreateOrderDto
                // // Note: At this point, ProductCodes are already resolved to ProductIds
                // var createOrderDto = new CreateOrderDto
                // {
                //     CustomerId = order.CustomerId,
                //     CustomerEmail = order.CustomerEmail,
                //     SupplierId = order.SupplierId,
                //     OrderDate = order.OrderDate,
                //     OrderStatus = order.OrderStatus,
                //     BillingAddress = order.BillingAddress != null ? new AddressDto
                //     {
                //         Street = order.BillingAddress.Street,
                //         City = order.BillingAddress.City,
                //         County = order.BillingAddress.County,
                //         PostalCode = order.BillingAddress.PostalCode,
                //         Country = order.BillingAddress.Country
                //     } : null,
                //     DeliveryAddress = order.DeliveryAddress != null ? new AddressDto
                //     {
                //         Street = order.DeliveryAddress.Street,
                //         City = order.DeliveryAddress.City,
                //         County = order.DeliveryAddress.County,
                //         PostalCode = order.DeliveryAddress.PostalCode,
                //         Country = order.DeliveryAddress.Country
                //     } : null,
                //     OrderItems = order.OrderItems?.Select(item => new CreateOrderItemDto
                //     {
                //         ProductId = item.ProductId, // Already resolved from ProductCode
                //         Quantity = item.Quantity,
                //         Price = item.Price
                //     }).ToList()
                // };
                // 
                // // 3. Serialize to JSON and call API
                // var json = JsonConvert.SerializeObject(createOrderDto);
                // var content = new StringContent(json, Encoding.UTF8, "application/json");
                // var response = await httpClient.PostAsync($"{apiBaseUrl}/api/orders", content);
                // 
                // // 4. Handle response
                // if (!response.IsSuccessStatusCode)
                // {
                //     var errorContent = await response.Content.ReadAsStringAsync();
                //     _logger.LogError("API call failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
                //     return StatusCode((int)response.StatusCode, new { error = "Failed to create order via API" });
                // }
                // 
                // // 5. Deserialize created order
                // var responseContent = await response.Content.ReadAsStringAsync();
                // var created = JsonConvert.DeserializeObject<OrderDto>(responseContent);
                //
                // ============================================================================
                // ALTERNATIVE: Call API for ProductCode lookup too
                // ============================================================================
                // If your Azure Function doesn't want to do ProductCode lookups, you could:
                // 1. Call GET /api/products/code/{guid} for each product (like in Step 1)
                // 2. Build CreateOrderDto with resolved ProductIds
                // 3. Call POST /api/orders with the DTO
                //
                // This separates concerns: Function does format conversion, API does DB work.
                // ============================================================================
                
                _logger.LogInformation("Order {OrderId} from Vault created and saved successfully", created.Id);

                // Step 4: Return success response with saved order details
                return CreatedAtAction(
                    nameof(GetSupportedSuppliers), // Placeholder action
                    new { id = created.Id },
                    new
                    {
                        success = true,
                        orderId = created.Id,
                        message = "Order from Vault created and saved successfully",
                        supplier = "Vault",
                        supplierId = 2,
                        orderReference = $"VAULT-{created.Id}",
                        productResolutions = productValidations,
                        totalAmount = created.TotalAmount,
                        itemCount = created.OrderItems?.Count ?? 0,
                        orderDate = created.OrderDate,
                        orderStatus = created.OrderStatus.ToString(),
                        customerEmail = created.CustomerEmail
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Vault order");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Error creating order from Vault",
                    error = ex.Message
                });
            }
        }

        /// <summary>
        /// GET /api/externalorders/suppliers
        /// 
        /// Returns information about supported suppliers.
        /// Teaching Point: Shows students what suppliers are integrated.
        /// </summary>
        [HttpGet("suppliers")]
        public ActionResult<object> GetSupportedSuppliers()
        {
            return Ok(new
            {
                suppliers = new[]
                {
                    new
                    {
                        id = 1,
                        name = "Speedy",
                        transformEndpoint = "/api/externalorders/fromspeedy",
                        createEndpoint = "/api/externalorders/speedycreate",
                        format = "Speedy uses: customerId (long), productId (long), qty, unitPrice"
                    },
                    new
                    {
                        id = 2,
                        name = "Vault",
                        transformEndpoint = "/api/externalorders/fromvault",
                        createEndpoint = "/api/externalorders/vaultcreate",
                        format = "Vault uses: customerEmail (string), productCode (Guid), placedAt (Unix timestamp)"
                    }
                },
                message = "Transform endpoints show data conversion only. Create endpoints save to database using Entity Framework.",
                teachingNote = "Use 'fromspeedy' and 'fromvault' to see transformation. Use 'speedycreate' and 'vaultcreate' for full integration."
            });
        }
    }
}

