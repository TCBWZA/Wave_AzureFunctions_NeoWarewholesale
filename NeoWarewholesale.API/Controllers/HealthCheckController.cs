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
        /// EXAMPLE: Health check with database connectivity verification
        /// 
        /// GET /api/healthcheck/status
        /// 
        /// Performs a comprehensive health check of the API and verifies database connectivity.
        /// This endpoint is useful for monitoring systems, load balancers, and orchestration platforms
        /// to determine if the service is healthy and capable of handling requests.
        /// 
        /// Response when healthy (200 OK):
        /// {
        ///   "status": "Good",
        ///   "timestamp": "2025-01-25T12:34:56.789Z",
        ///   "checks": {
        ///     "application": "Running",
        ///     "database": "Available"
        ///   },
        ///   "errors": null
        /// }
        /// 
        /// Response when unhealthy (503 Service Unavailable):
        /// {
        ///   "status": "Unhealthy",
        ///   "timestamp": "2025-01-25T12:34:56.789Z",
        ///   "checks": {
        ///     "application": "Running",
        ///     "database": "Unavailable"
        ///   },
        ///   "errors": ["Database server is not available"]
        /// }
        /// 
        /// Status Codes:
        /// - 200 OK: Application and database are both healthy
        /// - 503 Service Unavailable: Database is unreachable or other critical services are down
        /// 
        /// Use Cases:
        /// - Kubernetes liveness and readiness probes
        /// - Azure Application Insights monitoring
        /// - Load balancer health checks
        /// - CI/CD deployment verification
        /// - Uptime monitoring services
        /// </summary>
        /// <response code="200">API and database are healthy and operational.</response>
        /// <response code="503">Service is unavailable - database cannot be reached or another critical service is down.</response>
        [HttpGet]
        [Route("status")]
        [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status503ServiceUnavailable)]
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
