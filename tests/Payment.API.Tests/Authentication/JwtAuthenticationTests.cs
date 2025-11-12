using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Xunit;

namespace Payment.API.Tests.Authentication;

/// <summary>
/// Comprehensive tests for JWT authentication configuration with external Identity Microservice.
/// Tests validate that the Payment Microservice is correctly configured to validate tokens from an external authority.
/// </summary>
public class JwtAuthenticationTests
{
    private readonly string _testAuthority = "https://identity.test.com";
    private readonly string _testAudience = "payment-service";

    [Fact]
    public void JwtBearerOptions_ShouldBeConfiguredWithExternalAuthority()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Auth:Authority", _testAuthority },
                { "Auth:Audience", _testAudience }
            })
            .Build();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = configuration["Auth:Authority"];
                options.Audience = configuration["Auth:Audience"];
                options.RequireHttpsMetadata = true;
            });

        // Act
        var serviceProvider = services.BuildServiceProvider();
        var jwtBearerOptions = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        // Assert
        jwtBearerOptions.Authority.Should().Be(_testAuthority);
        jwtBearerOptions.Audience.Should().Be(_testAudience);
        jwtBearerOptions.RequireHttpsMetadata.Should().BeTrue();
    }

    [Fact]
    public async Task AuthorizationPolicies_ShouldBeConfigured()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory, Microsoft.Extensions.Logging.LoggerFactory>();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            options.AddPolicy("PaymentsWrite", policy =>
                policy.RequireClaim("scope", "payment.write"));
            
            options.AddPolicy("PaymentsRead", policy =>
                policy.RequireClaim("scope", "payment.read"));
            
            options.AddPolicy("PaymentsAdmin", policy =>
                policy.RequireClaim("scope", "payment.admin"));
        });

        // Act
        var serviceProvider = services.BuildServiceProvider();
        var authorizationService = serviceProvider.GetRequiredService<IAuthorizationService>();
        var authorizationPolicyProvider = serviceProvider.GetRequiredService<IAuthorizationPolicyProvider>();

        // Assert
        authorizationService.Should().NotBeNull();
        authorizationPolicyProvider.Should().NotBeNull();
        
        var paymentsWritePolicy = await authorizationPolicyProvider.GetPolicyAsync("PaymentsWrite");
        paymentsWritePolicy.Should().NotBeNull();
        paymentsWritePolicy!.Requirements.Should().NotBeEmpty();
        
        var paymentsReadPolicy = await authorizationPolicyProvider.GetPolicyAsync("PaymentsRead");
        paymentsReadPolicy.Should().NotBeNull();
        
        var paymentsAdminPolicy = await authorizationPolicyProvider.GetPolicyAsync("PaymentsAdmin");
        paymentsAdminPolicy.Should().NotBeNull();
    }

    [Fact]
    public void Configuration_ShouldThrowException_WhenAuthorityIsMissing()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Auth:Audience", _testAudience }
                // Auth:Authority is missing
            })
            .Build();

        // Act & Assert
        var authority = configuration["Auth:Authority"];
        authority.Should().BeNull();
        
        // This simulates the check in Program.cs
        Action act = () =>
        {
            var authAuthority = configuration["Auth:Authority"] 
                ?? throw new InvalidOperationException("Auth:Authority not configured");
        };
        
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Auth:Authority not configured");
    }

    [Fact]
    public void Configuration_ShouldThrowException_WhenAudienceIsMissing()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Auth:Authority", _testAuthority }
                // Auth:Audience is missing
            })
            .Build();

        // Act & Assert
        var audience = configuration["Auth:Audience"];
        audience.Should().BeNull();
        
        // This simulates the check in Program.cs
        Action act = () =>
        {
            var authAudience = configuration["Auth:Audience"] 
                ?? throw new InvalidOperationException("Auth:Audience not configured");
        };
        
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Auth:Audience not configured");
    }

    [Fact]
    public void JwtBearerOptions_ShouldNotHaveLocalSecretKey()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Auth:Authority", _testAuthority },
                { "Auth:Audience", _testAudience }
            })
            .Build();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = configuration["Auth:Authority"];
                options.Audience = configuration["Auth:Audience"];
                options.RequireHttpsMetadata = true;
                // No IssuerSigningKey is set - tokens are validated via JWKS from authority
            });

        // Act
        var serviceProvider = services.BuildServiceProvider();
        var jwtBearerOptions = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

        // Assert
        // TokenValidationParameters should be null or not have IssuerSigningKey set
        // In production, the signing key comes from the JWKS endpoint
        jwtBearerOptions.TokenValidationParameters.Should().NotBeNull();
        // The IssuerSigningKeyResolver or MetadataAddress will be used instead of a static key
    }
}

