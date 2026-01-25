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
        [HttpGet]
        public async Task<ActionResult<IEnumerable<SupplierDto>>> GetAll()
        {
            var suppliers = await _supplierRepository.GetAllAsync();
            return Ok(suppliers.Select(s => s.ToDto()));
        }

        /// <summary>
        /// GET /api/suppliers/{id}
        /// Retrieves a specific supplier by ID.
        /// </summary>
        [HttpGet("{id}")]
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
        [HttpGet("name/{name}")]
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
        [HttpPost]
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
        [HttpPut("{id}")]
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
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(long id)
        {
            var deleted = await _supplierRepository.DeleteAsync(id);
            if (!deleted)
                return NotFound($"Supplier with ID {id} not found.");

            return NoContent();
        }
    }
}
