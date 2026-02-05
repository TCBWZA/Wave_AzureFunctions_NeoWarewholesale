using Microsoft.AspNetCore.Mvc;
using NeoWarewholesale.API.Models;

namespace NeoWarewholesale.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthCheckController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<HealthCheckController> _logger;

        public HealthCheckController(AppDbContext dbContext, ILogger<HealthCheckController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        /// <summary>
        /// Performs a health check of the API and database connectivity.
        /// </summary>
        /// <returns>Health status with details about database availability</returns>
        [HttpGet]
        [Route("status")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<HealthCheckResponse>> GetHealthStatus()
        {
            var response = new HealthCheckResponse
            {
                Status = "Good",
                Timestamp = DateTime.UtcNow,
                Checks = new HealthCheckDetails()
            };

            try
            {
                // Check database connectivity
                var canConnect = await _dbContext.Database.CanConnectAsync();
                
                if (!canConnect)
                {
                    response.Status = "Unhealthy";
                    response.Checks.Database = "Unavailable";
                    response.Errors = new List<string> { "Database server is not available" };
                    
                    _logger.LogWarning("Health check failed: Database unavailable");
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, response);
                }

                response.Checks.Database = "Available";
                response.Checks.Application = "Running";

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check encountered an error");
                
                response.Status = "Unhealthy";
                response.Checks.Database = "Error";
                response.Errors = new List<string> { $"Health check error: {ex.Message}" };

                return StatusCode(StatusCodes.Status503ServiceUnavailable, response);
            }
        }
    }

    /// <summary>
    /// Response model for health check status
    /// </summary>
    public class HealthCheckResponse
    {
        public string Status { get; set; } = "Good";
        public DateTime Timestamp { get; set; }
        public HealthCheckDetails Checks { get; set; } = new();
        public List<string>? Errors { get; set; }
    }

    /// <summary>
    /// Details of individual health checks
    /// </summary>
    public class HealthCheckDetails
    {
        public string Application { get; set; } = "Running";
        public string Database { get; set; } = "Checking";
    }
}
