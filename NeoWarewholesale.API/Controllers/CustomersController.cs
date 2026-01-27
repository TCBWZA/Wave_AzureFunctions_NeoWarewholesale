using NeoWarewholesale.API.DTOs;
using NeoWarewholesale.API.Mappings;
using NeoWarewholesale.API.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace NeoWarewholesale.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CustomersController(
        ICustomerRepository customerRepository,
        ILogger<CustomersController> logger) : ControllerBase
    {
        private readonly ICustomerRepository _customerRepository = customerRepository;
        private readonly ILogger<CustomersController> _logger = logger;

        /// <summary>
        /// GET /api/customers
        /// Retrieves all customers with optional related data.
        /// </summary>
        /// <response code="200">Returns all customers.</response>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<CustomerDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<CustomerDto>>> GetAll([FromQuery] bool includeRelated = false)
        {
            var customers = await _customerRepository.GetAllAsync(includeRelated);
            return Ok(customers.Select(c => c.ToDto()));
        }

        /// <summary>
        /// EXAMPLE: Split Queries to avoid cartesian explosion
        /// 
        /// GET /api/customers/with-split-queries
        /// 
        /// This endpoint demonstrates how to use AsSplitQuery() to optimize queries
        /// that load multiple related collections. Instead of using JOINs (which create
        /// a cartesian product), EF Core executes separate queries:
        /// 
        /// Query 1: SELECT * FROM Customers
        /// Query 2: SELECT * FROM TelephoneNumbers WHERE CustomerId IN (1,2,3,...)
        /// 
        /// Benefits:
        /// - No data duplication (customer data not repeated for each phone)
        /// - Reduced data transfer (~75% less for typical datasets)
        /// - Better performance with large collections
        /// 
        /// Trade-off:
        /// - Multiple database round-trips (usually negligible with modern networks)
        /// 
        /// When to use:
        /// - Loading 2+ collections with Include()
        /// - Collections have many items per parent
        /// - Large datasets (1000+ parent records)
        /// </summary>
        /// <response code="200">Returns all customers with phone numbers loaded via split queries.</response>
        [HttpGet("with-split-queries")]
        [ProducesResponseType(typeof(IEnumerable<CustomerDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<CustomerDto>>> GetAllWithSplitQueries()
        {
            var customers = await _customerRepository.GetAllAsync(includeRelated: true);
            return Ok(customers.Select(c => c.ToDto()));
        }


        /// <summary>
        /// EXAMPLE: Dynamic filtering and search
        /// 
        /// GET /api/customers/search?name=acme&email=@acme.com
        /// 
        /// Demonstrates flexible querying with multiple optional filters.
        /// Only specified filters are applied - allows any combination.
        /// 
        /// Query Parameters (all optional):
        /// - name: Partial match on customer name (case-insensitive)
        /// - email: Partial match on email address
        /// 
        /// Examples:
        /// - /api/customers/search?name=corp
        ///   Returns customers with "corp" in name
        /// 
        /// - /api/customers/search?email=gmail
        ///   Returns customers with Gmail addresses
        /// 
        /// - /api/customers/search?name=acme&email=acme.com
        ///   Returns Acme customers with acme.com in email
        /// 
        /// SQL Pattern:
        /// All filters combined into single WHERE clause:
        /// WHERE Name LIKE '%acme%' AND Email LIKE '%gmail%'
        /// 
        /// Performance tips:
        /// - Create indexes on Name and Email columns
        /// - Consider adding pagination for large result sets
        /// </summary>
        /// <response code="200">Returns customers matching the search criteria.</response>
        [HttpGet("search")]
        [ProducesResponseType(typeof(IEnumerable<CustomerDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<CustomerDto>>> Search(
            [FromQuery] string? name,
            [FromQuery] string? email)
        {
            var customers = await _customerRepository.SearchAsync(name, email);
            return Ok(customers.Select(c => c.ToDto()));
        }

        /// <summary>
        /// EXAMPLE: Sorting with dynamic OrderBy
        /// 
        /// GET /api/customers/sorted?sortBy=email&descending=true
        /// 
        /// Allows client to control sort order for list views.
        /// Supports sorting by different fields and directions.
        /// 
        /// Query Parameters:
        /// - sortBy: Field to sort by (name, email). Default: name
        /// - descending: Sort direction (true/false). Default: false
        /// 
        /// Examples:
        /// - /api/customers/sorted
        ///   Sort by name ascending (A-Z)
        /// 
        /// - /api/customers/sorted?sortBy=email&descending=true
        ///   Sort by email descending (Z-A)
        /// 
        /// SQL Generated:
        /// SELECT * FROM Customers ORDER BY [Name|Email] [ASC|DESC]
        /// 
        /// Implementation note:
        /// Uses switch expression (C# 8.0) for clean, readable sorting logic.
        /// 
        /// Performance tips:
        /// - Create indexes on frequently sorted columns
        /// - Combine with pagination for better UX on large datasets
        /// </summary>
        /// <response code="200">Returns sorted customers.</response>
        [HttpGet("sorted")]
        [ProducesResponseType(typeof(IEnumerable<CustomerDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<CustomerDto>>> GetSorted(
            [FromQuery] string sortBy = "name",
            [FromQuery] bool descending = false)
        {
            // Get all customers as IQueryable (no SQL executed yet)
            var query = _customerRepository.GetAllAsync(includeRelated: false)
                .Result.AsQueryable();

            // Apply sorting based on parameters
            // Switch expression provides clean, type-safe sorting logic
            query = sortBy.ToLower() switch
            {
                "email" => descending 
                    ? query.OrderByDescending(c => c.Email) 
                    : query.OrderBy(c => c.Email),
                
                // Default to sorting by name
                _ => descending 
                    ? query.OrderByDescending(c => c.Name)
                    : query.OrderBy(c => c.Name)
            };

            return Ok(query.Select(c => c.ToDto()));
        }

        /// <summary>
        /// GET /api/customers/{id}
        /// Retrieves a specific customer by ID with related phone numbers.
        /// </summary>
        /// <response code="200">Customer found and returned.</response>
        /// <response code="404">Customer with the specified ID was not found.</response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<CustomerDto>> GetById(long id)
        {
            var customer = await _customerRepository.GetByIdAsync(id, includeRelated: true);
            if (customer == null)
                return NotFound($"Customer with ID {id} not found.");

            return Ok(customer.ToDto());
        }

        /// <summary>
        /// GET /api/customers/email/{email}
        /// Retrieves a customer by email address.
        /// </summary>
        /// <response code="200">Customer found and returned.</response>
        /// <response code="404">Customer with the specified email was not found.</response>
        [HttpGet("email/{email}")]
        [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<CustomerDto>> GetByEmail(string email)
        {
            var customer = await _customerRepository.GetByEmailAsync(email);
            if (customer == null)
                return NotFound($"Customer with email {email} not found.");

            return Ok(customer.ToDto());
        }

        /// <summary>
        /// EXAMPLE: Creating a customer with related entities
        /// 
        /// POST /api/customers
        /// 
        /// Creates a new customer and optionally includes related phone numbers
        /// in a single atomic transaction. All entities are saved together.
        /// 
        /// Request body example:
        /// {
        ///   "name": "Acme Corp",
        ///   "email": "contact@acme.com",
        ///   "phoneNumbers": [
        ///     {
        ///       "type": "Mobile",
        ///       "number": "555-1234"
        ///     },
        ///     {
        ///       "type": "Work",
        ///       "number": "555-5678"
        ///     }
        ///   ]
        /// }
        /// 
        /// EF Core behavior:
        /// - Creates Customer record
        /// - Creates related TelephoneNumber records with CustomerId automatically set
        /// - All operations happen in a single database transaction
        /// - If any operation fails, entire transaction is rolled back
        /// 
        /// Note: PhoneNumbers are optional - you can create a customer without them.
        /// </summary>
        /// <response code="201">Customer created successfully. Returns the created customer with ID.</response>
        /// <response code="400">Invalid request body or validation failed.</response>
        /// <response code="409">A customer with the specified email already exists.</response>
        [HttpPost]
        [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<CustomerDto>> Create([FromBody] CreateCustomerDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Check if email already exists
            if (await _customerRepository.EmailExistsAsync(dto.Email))
                return Conflict($"A customer with email {dto.Email} already exists.");

            var customer = dto.ToEntity();
            var created = await _customerRepository.CreateAsync(customer);

            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created.ToDto());
        }

        /// <summary>
        /// PUT /api/customers/{id}
        /// Updates an existing customer.
        /// </summary>
        /// <response code="200">Customer updated successfully. Returns the updated customer.</response>
        /// <response code="400">Invalid request body or validation failed.</response>
        /// <response code="404">Customer with the specified ID was not found.</response>
        /// <response code="409">A customer with the specified email already exists.</response>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<CustomerDto>> Update(long id, [FromBody] UpdateCustomerDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var customer = await _customerRepository.GetByIdAsync(id);
            if (customer == null)
                return NotFound($"Customer with ID {id} not found.");

            // Check if email already exists for another customer
            if (await _customerRepository.EmailExistsAsync(dto.Email, id))
                return Conflict($"A customer with email {dto.Email} already exists.");

            dto.UpdateEntity(customer);
            var updated = await _customerRepository.UpdateAsync(customer);

            return Ok(updated.ToDto());
        }

        /// <summary>
        /// DELETE /api/customers/{id}
        /// Deletes a customer by ID.
        /// </summary>
        /// <response code="204">Customer deleted successfully.</response>
        /// <response code="404">Customer with the specified ID was not found.</response>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> Delete(long id)
        {
            var deleted = await _customerRepository.DeleteAsync(id);
            if (!deleted)
                return NotFound($"Customer with ID {id} not found.");

            return NoContent();
        }
    }
}
