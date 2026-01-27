using NeoWarewholesale.API.DTOs;
using NeoWarewholesale.API.Mappings;
using NeoWarewholesale.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace NeoWarewholesale.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SuppliersController(
        ISupplierRepository supplierRepository,
        ILogger<SuppliersController> logger) : ControllerBase
    {
        private readonly ISupplierRepository _supplierRepository = supplierRepository;
        private readonly ILogger<SuppliersController> _logger = logger;

        /// <summary>
        /// GET /api/suppliers
        /// Retrieves all suppliers.
        /// </summary>
        /// <response code="200">Returns all suppliers.</response>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<SupplierDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<SupplierDto>>> GetAll()
        {
            var suppliers = await _supplierRepository.GetAllAsync();
            return Ok(suppliers.Select(s => s.ToDto()));
        }

        /// <summary>
        /// GET /api/suppliers/{id}
        /// Retrieves a specific supplier by ID.
        /// </summary>
        /// <response code="200">Supplier found and returned.</response>
        /// <response code="404">Supplier with the specified ID was not found.</response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<SupplierDto>> GetById(long id)
        {
            var supplier = await _supplierRepository.GetByIdAsync(id);
            if (supplier == null)
                return NotFound($"Supplier with ID {id} not found.");

            return Ok(supplier.ToDto());
        }

        /// <summary>
        /// GET /api/suppliers/name/{name}
        /// Retrieves a specific supplier by name.
        /// </summary>
        /// <response code="200">Supplier found and returned.</response>
        /// <response code="404">Supplier with the specified name was not found.</response>
        [HttpGet("name/{name}")]
        [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<SupplierDto>> GetByName(string name)
        {
            var supplier = await _supplierRepository.GetByNameAsync(name);
            if (supplier == null)
                return NotFound($"Supplier with name {name} not found.");

            return Ok(supplier.ToDto());
        }

        /// <summary>
        /// POST /api/suppliers
        /// Creates a new supplier.
        /// </summary>
        /// <response code="201">Supplier created successfully.</response>
        /// <response code="400">Invalid request body or validation failed.</response>
        /// <response code="409">A supplier with the specified name already exists.</response>
        [HttpPost]
        [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<SupplierDto>> Create([FromBody] CreateSupplierDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Check if supplier name already exists
            if (await _supplierRepository.NameExistsAsync(dto.Name))
                return Conflict($"A supplier with name {dto.Name} already exists.");

            var supplier = dto.ToEntity();
            var created = await _supplierRepository.CreateAsync(supplier);

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created.ToDto());
        }

        /// <summary>
        /// PUT /api/suppliers/{id}
        /// Updates an existing supplier.
        /// </summary>
        /// <response code="200">Supplier updated successfully.</response>
        /// <response code="400">Invalid request body or validation failed.</response>
        /// <response code="404">Supplier with the specified ID was not found.</response>
        /// <response code="409">A supplier with the specified name already exists.</response>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<SupplierDto>> Update(long id, [FromBody] UpdateSupplierDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var supplier = await _supplierRepository.GetByIdAsync(id);
            if (supplier == null)
                return NotFound($"Supplier with ID {id} not found.");

            // Check if supplier name already exists for another supplier
            if (await _supplierRepository.NameExistsAsync(dto.Name, id))
                return Conflict($"A supplier with name {dto.Name} already exists.");

            dto.UpdateEntity(supplier);
            var updated = await _supplierRepository.UpdateAsync(supplier);

            return Ok(updated.ToDto());
        }

        /// <summary>
        /// DELETE /api/suppliers/{id}
        /// Deletes a supplier by ID.
        /// </summary>
        /// <response code="204">Supplier deleted successfully.</response>
        /// <response code="404">Supplier with the specified ID was not found.</response>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> Delete(long id)
        {
            var deleted = await _supplierRepository.DeleteAsync(id);
            if (!deleted)
                return NotFound($"Supplier with ID {id} not found.");

            return NoContent();
        }
    }
}
