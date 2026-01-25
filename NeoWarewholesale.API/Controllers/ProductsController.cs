using NeoWarewholesale.API.DTOs;
using NeoWarewholesale.API.Mappings;
using NeoWarewholesale.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace NeoWarewholesale.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController(
        IProductRepository productRepository,
        ILogger<ProductsController> logger) : ControllerBase
    {
        private readonly IProductRepository _productRepository = productRepository;
        private readonly ILogger<ProductsController> _logger = logger;

        /// <summary>
        /// GET /api/products
        /// Retrieves all products.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProductDto>>> GetAll()
        {
            var products = await _productRepository.GetAllAsync();
            return Ok(products.Select(p => p.ToDto()));
        }

        /// <summary>
        /// GET /api/products/paged?page=1&pageSize=10
        /// Retrieves products with pagination support.
        /// </summary>
        [HttpGet("paged")]
        public async Task<ActionResult<object>> GetPaged(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            if (page < 1 || pageSize < 1)
                return BadRequest("Page and pageSize must be greater than 0.");

            var (items, totalCount) = await _productRepository.GetPagedAsync(page, pageSize);
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            return Ok(new
            {
                Items = items.Select(p => p.ToDto()),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages
            });
        }

        /// <summary>
        /// GET /api/products/search?name=widget&productCode=guid
        /// Search products by name or product code.
        /// </summary>
        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<ProductDto>>> Search(
            [FromQuery] string? name,
            [FromQuery] Guid? productCode)
        {
            var products = await _productRepository.SearchAsync(name, productCode);
            return Ok(products.Select(p => p.ToDto()));
        }

        /// <summary>
        /// GET /api/products/{id}
        /// Retrieves a specific product by ID.
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ProductDto>> GetById(long id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
                return NotFound($"Product with ID {id} not found.");

            return Ok(product.ToDto());
        }

        /// <summary>
        /// GET /api/products/code/{productCode}
        /// Retrieves a specific product by product code (GUID).
        /// </summary>
        [HttpGet("code/{productCode}")]
        public async Task<ActionResult<ProductDto>> GetByProductCode(Guid productCode)
        {
            var product = await _productRepository.GetByProductCodeAsync(productCode);
            if (product == null)
                return NotFound($"Product with code {productCode} not found.");

            return Ok(product.ToDto());
        }

        /// <summary>
        /// POST /api/products
        /// Creates a new product.
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ProductDto>> Create([FromBody] CreateProductDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Check if product code already exists
            if (await _productRepository.ProductCodeExistsAsync(dto.ProductCode))
                return Conflict($"A product with code {dto.ProductCode} already exists.");

            var product = dto.ToEntity();
            var created = await _productRepository.CreateAsync(product);

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created.ToDto());
        }

        /// <summary>
        /// PUT /api/products/{id}
        /// Updates an existing product.
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<ProductDto>> Update(long id, [FromBody] UpdateProductDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
                return NotFound($"Product with ID {id} not found.");

            // Check if product code already exists for another product
            if (await _productRepository.ProductCodeExistsAsync(dto.ProductCode, id))
                return Conflict($"A product with code {dto.ProductCode} already exists.");

            dto.UpdateEntity(product);
            var updated = await _productRepository.UpdateAsync(product);

            return Ok(updated.ToDto());
        }

        /// <summary>
        /// DELETE /api/products/{id}
        /// Deletes a product by ID.
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(long id)
        {
            var deleted = await _productRepository.DeleteAsync(id);
            if (!deleted)
                return NotFound($"Product with ID {id} not found.");

            return NoContent();
        }
    }
}
