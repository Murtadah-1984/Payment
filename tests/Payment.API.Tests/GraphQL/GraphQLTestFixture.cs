using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Payment.API;
using Payment.Infrastructure.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;

namespace Payment.API.Tests.GraphQL;

/// <summary>
/// Test fixture for GraphQL integration tests.
/// Provides WebApplicationFactory with mocked authentication and in-memory database.
/// </summary>
public class GraphQLTestFixture : WebApplicationFactory<Program>, IDisposable
{
    private const string TestSecretKey = "TestSecretKeyThatIsAtLeast32CharactersLong!";
    private const string TestIssuer = "https://test-identity.com";
    private const string TestAudience = "payment-service";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Auth:Authority", TestIssuer },
                { "Auth:Audience", TestAudience },
                { "ConnectionStrings:DefaultConnection", "Host=localhost;Database=payment_test;Username=postgres;Password=postgres" },
                { "ConnectionStrings:Redis", "" },
                { "FeatureManagement:EnableTapToPay", "true" }
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove existing DbContext registration
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<PaymentDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Add in-memory database for testing
            services.AddDbContext<PaymentDbContext>(options =>
            {
                options.UseInMemoryDatabase("PaymentTestDb");
            });

            // Override JWT authentication for testing
            services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = false, // Disable lifetime validation for testing
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = TestIssuer,
                    ValidAudience = TestAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSecretKey))
                };
            });
        });
    }

    /// <summary>
    /// Generates a test JWT token with specified claims.
    /// </summary>
    public string GenerateTestToken(params Claim[] additionalClaims)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "test-user-id"),
            new Claim(ClaimTypes.Name, "test-user"),
            new Claim("scope", "payment.read"),
            new Claim("scope", "payment.write")
        };

        claims.AddRange(additionalClaims);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            audience: TestAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Creates an HTTP client with authentication token.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(params Claim[] additionalClaims)
    {
        var client = CreateClient();
        var token = GenerateTestToken(additionalClaims);
        client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public new void Dispose()
    {
        base.Dispose();
    }
}

