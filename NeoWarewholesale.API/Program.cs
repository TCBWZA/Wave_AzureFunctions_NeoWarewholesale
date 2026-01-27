using Microsoft.EntityFrameworkCore;
using NeoWarewholesale.API;
using NeoWarewholesale.API.Models;
using NeoWarewholesale.API.Repositories;
using FluentValidation;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Add FluentValidation - automatically validates models
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Configure Entity Framework Core with SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register repositories
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<ITelephoneNumberRepository, TelephoneNumberRepository>();

// Configure SeedSettings
builder.Services.Configure<SeedSettings>(builder.Configuration.GetSection("SeedSettings"));

// Configure OpenAPI/Scalar documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Seed database if enabled
var seedSettings = builder.Configuration.GetSection("SeedSettings").Get<SeedSettings>();
if (seedSettings?.EnableSeeding == true)
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        // Ensure database is created
        await context.Database.EnsureCreatedAsync();
        
        // Seed data
        await BogusDataGenerator.SeedDatabase(
            context,
            customerCount: seedSettings.CustomerCount,
            productCount: seedSettings.ProductCount,
            orderCount: seedSettings.OrderCount,
            minPhoneNumbersPerCustomer: seedSettings.MinPhoneNumbersPerCustomer,
            maxPhoneNumbersPerCustomer: seedSettings.MaxPhoneNumbersPerCustomer,
            minOrderItemsPerOrder: seedSettings.MinOrderItemsPerOrder,
            maxOrderItemsPerOrder: seedSettings.MaxOrderItemsPerOrder
        );
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // Enable Swagger middleware to generate OpenAPI document
    app.UseSwagger(options =>
    {
        options.RouteTemplate = "openapi/{documentName}.json";
    });
    
    // Use Scalar for API documentation (NOT SwaggerUI)
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("NeoWarewholesale API")
            .WithTheme(ScalarTheme.Purple)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
            .WithOpenApiRoutePattern("/openapi/{documentName}.json");
    });
}

app.UseAuthorization();

app.MapControllers();

app.Run();

