using FastEndpoints;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Abstractions;
using Microsoft.AspNetCore.Identity;
using Endatix.Core.Entities.Identity;
using Microsoft.Extensions.Logging;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Seed;

namespace Endatix.Api.Endpoints.Test;

/// <summary>
/// Test endpoint for testing RolesSeeder functionality.
/// </summary>
public class UnifiedTestEndpoint : Endpoint<UnifiedTestRequest, UnifiedTestResponse>
{
    private readonly RoleManager<AppRole> _roleManager;
    private readonly UserManager<AppUser> _userManager;
    private readonly IUserContext _userContext;
    private readonly ILogger<UnifiedTestEndpoint> _logger;
    private readonly AppIdentityDbContext _dbContext;
    private readonly ILoggerFactory _loggerFactory;

    public UnifiedTestEndpoint(
        RoleManager<AppRole> roleManager,
        UserManager<AppUser> userManager,
        IUserContext userContext,
        ILogger<UnifiedTestEndpoint> logger,
        AppIdentityDbContext dbContext,
        ILoggerFactory loggerFactory)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _userContext = userContext;
        _logger = logger;
        _dbContext = dbContext;
        _loggerFactory = loggerFactory;
    }

    public override void Configure()
    {
        Get("test/data-seed");
        Summary(s =>
        {
            s.Summary = "Test endpoint for RolesSeeder";
            s.Description = "Tests the RolesSeeder.SeedSystemRolesAsync() method";
            s.Response<UnifiedTestResponse>(200, "Test completed successfully");
        });
    }

    public override async Task HandleAsync(UnifiedTestRequest req, CancellationToken ct)
    {
        var response = new UnifiedTestResponse();

        try
        {
            if (req.SeedTestData)
            {
                _logger.LogInformation("Testing RolesSeeder...");

                // Create RolesSeeder instance with required dependencies
                var rolesSeederLogger = _loggerFactory.CreateLogger<RolesSeeder>();
                var rolesSeeder = new RolesSeeder(rolesSeederLogger, _userManager, _roleManager, _dbContext);

                // Call the seeder
                await rolesSeeder.SeedSystemRolesAsync();

                response.TestDataSeeded = true;
                _logger.LogInformation("RolesSeeder test completed");
            }


            response.Success = true;
            response.Message = "RolesSeeder test completed successfully";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing RolesSeeder");
            response.Success = false;
            response.Message = $"Error: {ex.Message}";
            response.Error = ex.ToString();
        }

        await Send.OkAsync(response, ct);
    }
}

public class UnifiedTestRequest
{
    public bool SeedTestData { get; set; } = true;
    public bool TestAuth { get; set; } = false;
}

public class UnifiedTestResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Error { get; set; }
    public bool TestDataSeeded { get; set; }
}