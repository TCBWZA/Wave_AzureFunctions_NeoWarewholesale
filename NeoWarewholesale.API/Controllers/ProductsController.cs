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
        /// <response code="200">Returns all products.</response>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<ProductDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<ProductDto>>> GetAll()
        {
            var products = await _productRepository.GetAllAsync();
            return Ok(products.Select(p => p.ToDto()));
        }

        /// <summary>
        /// GET /api/products/paged?page=1&pageSize=10
        /// Retrieves products with pagination support.
        /// </summary>
        /// <response code="200">Returns paginated products.</response>
        /// <response code="400">Invalid page or pageSize parameters.</response>
        [HttpGet("paged")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
        /// <response code="200">Returns products matching the search criteria.</response>
        [HttpGet("search")]
        [ProducesResponseType(typeof(IEnumerable<ProductDto>), StatusCodes.Status200OK)]
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
        /// <response code="200">Product found and returned.</response>
        /// <response code="404">Product with the specified ID was not found.</response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
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
        /// <response code="200">Product found and returned.</response>
        /// <response code="404">Product with the specified code was not found.</response>
        [HttpGet("code/{productCode}")]
        [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
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
        /// <response code="201">Product created successfully.</response>
        /// <response code="400">Invalid request body or validation failed.</response>
        /// <response code="409">A product with the specified ProductCode already exists.</response>
        [HttpPost]
        [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
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
        /// <response code="200">Product updated successfully.</response>
        /// <response code="400">Invalid request body or validation failed.</response>
        /// <response code="404">Product with the specified ID was not found.</response>
        /// <response code="409">A product with the specified ProductCode already exists.</response>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
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
        /// <response code="204">Product deleted successfully.</response>
        /// <response code="404">Product with the specified ID was not found.</response>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> Delete(long id)
        {
            var deleted = await _productRepository.DeleteAsync(id);
            if (!deleted)
                return NotFound($"Product with ID {id} not found.");

            return NoContent();
        }
    }
}
