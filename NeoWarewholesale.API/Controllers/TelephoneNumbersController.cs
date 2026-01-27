using NeoWarewholesale.API.DTOs;
using NeoWarewholesale.API.Mappings;
using NeoWarewholesale.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace NeoWarewholesale.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TelephoneNumbersController : ControllerBase
    {
        private readonly ITelephoneNumberRepository _telephoneNumberRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly ILogger<TelephoneNumbersController> _logger;

        public TelephoneNumbersController(
            ITelephoneNumberRepository telephoneNumberRepository,
            ICustomerRepository customerRepository,
            ILogger<TelephoneNumbersController> logger)
        {
            _telephoneNumberRepository = telephoneNumberRepository;
            _customerRepository = customerRepository;
            _logger = logger;
        }

        /// <summary>
        /// GET /api/telephonenumbers
        /// Retrieves all telephone numbers.
        /// </summary>
        /// <response code="200">Returns all telephone numbers.</response>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<TelephoneNumberDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<TelephoneNumberDto>>> GetAll()
        {
            var telephoneNumbers = await _telephoneNumberRepository.GetAllAsync();
            return Ok(telephoneNumbers.Select(t => t.ToDto()));
        }

        /// <summary>
        /// GET /api/telephonenumbers/{id}
        /// Retrieves a specific telephone number by ID.
        /// </summary>
        /// <response code="200">Telephone number found and returned.</response>
        /// <response code="404">Telephone number with the specified ID was not found.</response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(TelephoneNumberDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<TelephoneNumberDto>> GetById(long id)
        {
            var telephoneNumber = await _telephoneNumberRepository.GetByIdAsync(id);
            if (telephoneNumber == null)
                return NotFound($"Telephone number with ID {id} not found.");

            return Ok(telephoneNumber.ToDto());
        }

        /// <summary>
        /// GET /api/telephonenumbers/customer/{customerId}
        /// Retrieves all telephone numbers for a specific customer.
        /// </summary>
        /// <response code="200">Returns telephone numbers for the customer.</response>
        /// <response code="404">Customer with the specified ID was not found.</response>
        [HttpGet("customer/{customerId}")]
        [ProducesResponseType(typeof(IEnumerable<TelephoneNumberDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<IEnumerable<TelephoneNumberDto>>> GetByCustomerId(long customerId)
        {
            if (!await _customerRepository.ExistsAsync(customerId))
                return NotFound($"Customer with ID {customerId} not found.");

            var telephoneNumbers = await _telephoneNumberRepository.GetByCustomerIdAsync(customerId);
            return Ok(telephoneNumbers.Select(t => t.ToDto()));
        }

        /// <summary>
        /// POST /api/telephonenumbers
        /// Creates a new telephone number.
        /// </summary>
        /// <response code="201">Telephone number created successfully.</response>
        /// <response code="400">Invalid request body, validation failed, or customer doesn't exist.</response>
        [HttpPost]
        [ProducesResponseType(typeof(TelephoneNumberDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<TelephoneNumberDto>> Create([FromBody] CreateTelephoneNumberDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Check if customer exists
            if (!await _customerRepository.ExistsAsync(dto.CustomerId))
                return BadRequest($"Customer with ID {dto.CustomerId} does not exist.");

            var telephoneNumber = dto.ToEntity();
            var created = await _telephoneNumberRepository.CreateAsync(telephoneNumber);

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created.ToDto());
        }

        /// <summary>
        /// PUT /api/telephonenumbers/{id}
        /// Updates an existing telephone number.
        /// </summary>
        /// <response code="200">Telephone number updated successfully.</response>
        /// <response code="400">Invalid request body or validation failed.</response>
        /// <response code="404">Telephone number with the specified ID was not found.</response>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(TelephoneNumberDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<TelephoneNumberDto>> Update(long id, [FromBody] UpdateTelephoneNumberDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var telephoneNumber = await _telephoneNumberRepository.GetByIdAsync(id);
            if (telephoneNumber == null)
                return NotFound($"Telephone number with ID {id} not found.");

            dto.UpdateEntity(telephoneNumber);
            var updated = await _telephoneNumberRepository.UpdateAsync(telephoneNumber);

            return Ok(updated.ToDto());
        }

        /// <summary>
        /// DELETE /api/telephonenumbers/{id}
        /// Deletes a telephone number by ID.
        /// </summary>
        /// <response code="204">Telephone number deleted successfully.</response>
        /// <response code="404">Telephone number with the specified ID was not found.</response>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> Delete(long id)
        {
            var deleted = await _telephoneNumberRepository.DeleteAsync(id);
            if (!deleted)
                return NotFound($"Telephone number with ID {id} not found.");

            return NoContent();
        }
    }
}
