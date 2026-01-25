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

        [HttpGet]
        public async Task<ActionResult<IEnumerable<TelephoneNumberDto>>> GetAll()
        {
            var telephoneNumbers = await _telephoneNumberRepository.GetAllAsync();
            return Ok(telephoneNumbers.Select(t => t.ToDto()));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<TelephoneNumberDto>> GetById(long id)
        {
            var telephoneNumber = await _telephoneNumberRepository.GetByIdAsync(id);
            if (telephoneNumber == null)
                return NotFound($"Telephone number with ID {id} not found.");

            return Ok(telephoneNumber.ToDto());
        }

        [HttpGet("customer/{customerId}")]
        public async Task<ActionResult<IEnumerable<TelephoneNumberDto>>> GetByCustomerId(long customerId)
        {
            if (!await _customerRepository.ExistsAsync(customerId))
                return NotFound($"Customer with ID {customerId} not found.");

            var telephoneNumbers = await _telephoneNumberRepository.GetByCustomerIdAsync(customerId);
            return Ok(telephoneNumbers.Select(t => t.ToDto()));
        }

        [HttpPost]
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

        [HttpPut("{id}")]
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

        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(long id)
        {
            var deleted = await _telephoneNumberRepository.DeleteAsync(id);
            if (!deleted)
                return NotFound($"Telephone number with ID {id} not found.");

            return NoContent();
        }
    }
}
