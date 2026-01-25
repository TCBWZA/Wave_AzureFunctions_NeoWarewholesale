using NeoWarewholesale.API.DTOs;
using NeoWarewholesale.API.Mappings;
using NeoWarewholesale.API.Repositories;
using NeoWarewholesale.API.Validators;
using Microsoft.AspNetCore.Mvc;

namespace NeoWarewholesale.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController(
        IOrderRepository orderRepository,
        ICustomerRepository customerRepository,
        ISupplierRepository supplierRepository,
        IProductRepository productRepository,
        CreateOrderDtoValidator createOrderValidator,
        UpdateOrderDtoValidator updateOrderValidator,
        ILogger<OrdersController> logger) : ControllerBase
    {
        private readonly IOrderRepository _orderRepository = orderRepository;
        private readonly ICustomerRepository _customerRepository = customerRepository;
        private readonly ISupplierRepository _supplierRepository = supplierRepository;
        private readonly IProductRepository _productRepository = productRepository;
        private readonly CreateOrderDtoValidator _createOrderValidator = createOrderValidator;
        private readonly UpdateOrderDtoValidator _updateOrderValidator = updateOrderValidator;
        private readonly ILogger<OrdersController> _logger = logger;

        /// <summary>
        /// GET /api/orders?includeRelated=true
        /// Retrieves all orders with optional related data.
        /// By default, includes OrderItems and Products to show order details.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<OrderDto>>> GetAll([FromQuery] bool includeRelated = true)
        {
            var orders = await _orderRepository.GetAllAsync(includeRelated);
            return Ok(orders.Select(o => o.ToDto()));
        }

        /// <summary>
        /// GET /api/orders/paged?page=1&pageSize=10&includeRelated=true
        /// Retrieves orders with pagination support.
        /// </summary>
        [HttpGet("paged")]
        public async Task<ActionResult<object>> GetPaged(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] bool includeRelated = false)
        {
            if (page < 1 || pageSize < 1)
                return BadRequest("Page and pageSize must be greater than 0.");

            var (items, totalCount) = await _orderRepository.GetPagedAsync(page, pageSize, includeRelated);
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            return Ok(new
            {
                Items = items.Select(o => o.ToDto()),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages
            });
        }

        /// <summary>
        /// GET /api/orders/{id}
        /// Retrieves a specific order by ID.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<OrderDto>> GetById(long id)
        {
            var order = await _orderRepository.GetByIdAsync(id, includeRelated: true);
            if (order == null)
                return NotFound($"Order with ID {id} not found.");

            return Ok(order.ToDto());
        }

        /// <summary>
        /// GET /api/orders/customer/{customerId}
        /// Retrieves all orders for a specific customer.
        /// </summary>
        [HttpGet("customer/{customerId}")]
        public async Task<ActionResult<IEnumerable<OrderDto>>> GetByCustomerId(
            long customerId,
            [FromQuery] bool includeRelated = false)
        {
            if (!await _customerRepository.ExistsAsync(customerId))
                return NotFound($"Customer with ID {customerId} not found.");

            var orders = await _orderRepository.GetByCustomerIdAsync(customerId, includeRelated);
            return Ok(orders.Select(o => o.ToDto()));
        }

        /// <summary>
        /// GET /api/orders/supplier/{supplierId}
        /// Retrieves all orders from a specific supplier.
        /// </summary>
        [HttpGet("supplier/{supplierId}")]
        public async Task<ActionResult<IEnumerable<OrderDto>>> GetBySupplierId(
            long supplierId,
            [FromQuery] bool includeRelated = false)
        {
            if (!await _supplierRepository.ExistsAsync(supplierId))
                return NotFound($"Supplier with ID {supplierId} not found.");

            var orders = await _orderRepository.GetBySupplierIdAsync(supplierId, includeRelated);
            return Ok(orders.Select(o => o.ToDto()));
        }

        /// <summary>
        /// POST /api/orders
        /// Creates a new order with order items.
        /// 
        /// Request body example:
        /// {
        ///   "customerId": 1,
        ///   "supplierId": 1,
        ///   "orderDate": "2024-01-15T00:00:00Z",
        ///   "orderItems": [
        ///     {
        ///       "productId": 1,
        ///       "quantity": 5,
        ///       "price": 29.99
        ///     },
        ///     {
        ///       "productId": 2,
        ///       "quantity": 3,
        ///       "price": 49.99
        ///     }
        ///   ]
        /// }
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<OrderDto>> Create([FromBody] CreateOrderDto dto)
        {
            // Validate using FluentValidation
            var validationResult = await _createOrderValidator.ValidateAsync(dto);
            if (!validationResult.IsValid)
            {
                foreach (var error in validationResult.Errors)
                {
                    ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                }
                return BadRequest(ModelState);
            }

            // Validate customer exists if CustomerId is provided
            if (dto.CustomerId.HasValue && !await _customerRepository.ExistsAsync(dto.CustomerId.Value))
                return BadRequest($"Customer with ID {dto.CustomerId} does not exist.");

            // Validate supplier exists
            if (!await _supplierRepository.ExistsAsync(dto.SupplierId))
                return BadRequest($"Supplier with ID {dto.SupplierId} does not exist.");

            // Validate all products exist
            foreach (var item in dto.OrderItems)
            {
                if (!await _productRepository.ExistsAsync(item.ProductId))
                    return BadRequest($"Product with ID {item.ProductId} does not exist.");
            }

            var order = dto.ToEntity();
            var created = await _orderRepository.CreateAsync(order);

            // Reload with related data
            var createdOrder = await _orderRepository.GetByIdAsync(created.Id, includeRelated: true);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, createdOrder!.ToDto());
        }

        /// <summary>
        /// PUT /api/orders/{id}
        /// Updates an existing order.
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<OrderDto>> Update(long id, [FromBody] UpdateOrderDto dto)
        {
            // Validate using FluentValidation
            var validationResult = await _updateOrderValidator.ValidateAsync(dto);
            if (!validationResult.IsValid)
            {
                foreach (var error in validationResult.Errors)
                {
                    ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
                }
                return BadRequest(ModelState);
            }

            var order = await _orderRepository.GetByIdAsync(id, includeRelated: true);
            if (order == null)
                return NotFound($"Order with ID {id} not found.");

            // Validate customer exists if CustomerId is provided
            if (dto.CustomerId.HasValue && !await _customerRepository.ExistsAsync(dto.CustomerId.Value))
                return BadRequest($"Customer with ID {dto.CustomerId} does not exist.");

            // Validate supplier exists
            if (!await _supplierRepository.ExistsAsync(dto.SupplierId))
                return BadRequest($"Supplier with ID {dto.SupplierId} does not exist.");

            // Validate all products exist
            foreach (var item in dto.OrderItems)
            {
                if (!await _productRepository.ExistsAsync(item.ProductId))
                    return BadRequest($"Product with ID {item.ProductId} does not exist.");
            }

            dto.UpdateEntity(order);
            var updated = await _orderRepository.UpdateAsync(order);

            // Reload with related data
            var updatedOrder = await _orderRepository.GetByIdAsync(updated.Id, includeRelated: true);
            return Ok(updatedOrder!.ToDto());
        }

        /// <summary>
        /// DELETE /api/orders/{id}
        /// Deletes an order by ID.
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(long id)
        {
            var deleted = await _orderRepository.DeleteAsync(id);
            if (!deleted)
                return NotFound($"Order with ID {id} not found.");

            return NoContent();
        }
    }
}
