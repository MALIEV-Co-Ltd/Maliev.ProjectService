using System.IdentityModel.Tokens.Jwt;
using System.Diagnostics.CodeAnalysis;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;
using Xunit;

// Disable parallel execution to prevent race conditions on the shared singleton database
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Maliev.ProjectService.Tests.Testing;

/// <summary>
/// Base integration test factory for ProjectService.
/// Provides PostgreSQL, Redis, and RabbitMQ containers with parallel startup.
/// </summary>
/// <typeparam name="TProgram">The Program class of the service being tested</typeparam>
/// <typeparam name="TDbContext">The DbContext type for the service</typeparam>
public class BaseIntegrationTestFactory<TProgram, TDbContext> : WebApplicationFactory<TProgram>, IAsyncLifetime
    where TProgram : class
    where TDbContext : DbContext
{
    private static PostgreSqlContainer? _postgresContainer;
    private static RedisContainer? _redisContainer;
    private static RabbitMqContainer? _rabbitmqContainer;
    private static bool _containersStarted;
    private static readonly SemaphoreSlim _initLock = new(1, 1);

    private readonly RSA _testRsa;

    /// <summary>
    /// Override this property to match the connection string name used by the service.
    /// </summary>
    protected virtual string DbConnectionStringName => "ProjectDbContext";

    /// <summary>Initializes a new instance of the factory.</summary>
    public BaseIntegrationTestFactory()
    {
        _testRsa = RSA.Create(2048);
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        await _initLock.WaitAsync();
        try
        {
            if (!_containersStarted)
            {
                _postgresContainer =
#pragma warning disable CS0618
                    new PostgreSqlBuilder().WithImage("postgres:18-alpine").Build();
                _redisContainer = new RedisBuilder("redis:7.4-alpine").Build();
                _rabbitmqContainer = new RabbitMqBuilder("rabbitmq:4.0-alpine").Build();
#pragma warning restore CS0618

                await Task.WhenAll(
                    _postgresContainer.StartAsync(),
                    _redisContainer.StartAsync(),
                    _rabbitmqContainer.StartAsync());

                // Wait for PostgreSQL to accept connections
                var postgresReady = false;
                var retryCount = 0;
                while (!postgresReady && retryCount < 60)
                {
                    try
                    {
                        await using var conn = new Npgsql.NpgsqlConnection(_postgresContainer.GetConnectionString());
                        await conn.OpenAsync();
                        await using var cmd = conn.CreateCommand();
                        cmd.CommandText = "SELECT 1";
                        await cmd.ExecuteScalarAsync();
                        postgresReady = true;
                    }
                    catch
                    {
                        retryCount++;
                        await Task.Delay(1000);
                    }
                }

                if (!postgresReady)
                    throw new InvalidOperationException("PostgreSQL Testcontainer failed to become ready after 60 seconds.");

                // Wait for Redis
                using (var connection = await StackExchange.Redis.ConnectionMultiplexer.ConnectAsync(_redisContainer.GetConnectionString()))
                {
                    await connection.GetDatabase().PingAsync();
                }

                await ApplyMigrationsAsync();
                _containersStarted = true;
            }
        }
        finally
        {
            _initLock.Release();
        }

        Environment.SetEnvironmentVariable($"ConnectionStrings__{DbConnectionStringName}", _postgresContainer!.GetConnectionString());
        Environment.SetEnvironmentVariable("ConnectionStrings__redis", _redisContainer!.GetConnectionString());
        Environment.SetEnvironmentVariable("ConnectionStrings__rabbitmq", _rabbitmqContainer!.GetConnectionString());
        Environment.SetEnvironmentVariable("CORS_ALLOWED_ORIGINS", "http://localhost:3000");
        Environment.SetEnvironmentVariable("CORS__AllowedOrigins__0", "http://localhost:3000");
        Environment.SetEnvironmentVariable("IAM__RegistrationDelaySeconds", "0");
    }

    /// <inheritdoc />
    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        _testRsa.Dispose();
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
        Environment.SetEnvironmentVariable("CORS_ALLOWED_ORIGINS", null);
        Environment.SetEnvironmentVariable("CORS__AllowedOrigins__0", null);
        Environment.SetEnvironmentVariable("IAM__RegistrationDelaySeconds", null);
    }

    /// <inheritdoc />
    protected override IHost CreateHost(IHostBuilder builder)
    {
        if (!_containersStarted)
            InitializeAsync().GetAwaiter().GetResult();

        var rsaParams = _testRsa.ExportParameters(false);
        Environment.SetEnvironmentVariable("JWT_PUBLIC_KEY_MODULUS", Convert.ToBase64String(rsaParams.Modulus!));
        Environment.SetEnvironmentVariable("JWT_PUBLIC_KEY_EXPONENT", Convert.ToBase64String(rsaParams.Exponent!));

        var keyBytes = _testRsa.ExportSubjectPublicKeyInfo();
        Environment.SetEnvironmentVariable("Authentication__Jwt__PublicKey", Convert.ToBase64String(keyBytes));

        ConfigureEnvironmentVariables();
        return base.CreateHost(builder);
    }

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Service:Name"] = "ProjectService",
                ["Service:Version"] = "1.0.0-test",
                ["Services:IAMService:BaseUrl"] = "http://localhost:5000",
                ["Jwt:SecurityKey"] = "test-secret-key-at-least-32-characters-long!!",
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience",
                [$"ConnectionStrings:{DbConnectionStringName}"] = _postgresContainer!.GetConnectionString(),
                ["ConnectionStrings:redis"] = _redisContainer!.GetConnectionString(),
                ["ConnectionStrings:rabbitmq"] = _rabbitmqContainer!.GetConnectionString(),
                ["CORS:AllowedOrigins:0"] = "http://localhost:3000",
                ["CORS_ALLOWED_ORIGINS"] = "http://localhost:3000",
                ["IAM:RegistrationDelaySeconds"] = "0"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Manual Redis registration for tests
            var redisConnectionString = _redisContainer!.GetConnectionString();
            services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(_ =>
                StackExchange.Redis.ConnectionMultiplexer.Connect(redisConnectionString));

            // Mock IAM service to fail fast — JWT claims used directly
            var iamMock = new Mock<Maliev.Aspire.ServiceDefaults.IAM.IIamServiceClient>();
            iamMock.Setup(x => x.CheckPermissionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            iamMock.Setup(x => x.GetUserPermissionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Enumerable.Empty<string>());
            services.AddSingleton(iamMock.Object);

            var statusTracker = new Maliev.Aspire.ServiceDefaults.IAM.IAMRegistrationStatusTracker();
            statusTracker.MarkRegistered();
            services.AddSingleton(statusTracker);

            // Configure JWT Bearer with test RSA key
            services.PostConfigureAll<JwtBearerOptions>(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = "test-issuer",
                    ValidAudience = "test-audience",
                    IssuerSigningKey = new RsaSecurityKey(_testRsa),
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = "sub",
                    RoleClaimType = "role"
                };
                options.TokenValidationParameters.SignatureValidator = null;
            });

            services.AddMassTransitTestHarness();

            // Disable background IAM registration service in tests
            var backgroundServicesToDisable = new[] { "BackgroundIAMRegistrationService" };
            var descriptors = services
                .Where(d => d.ServiceType == typeof(IHostedService) && backgroundServicesToDisable.Contains(d.ImplementationType?.Name))
                .ToList();
            foreach (var descriptor in descriptors)
                services.Remove(descriptor);

            ConfigureAdditionalServices(services);
        });
    }

    /// <summary>Override to set additional environment variables before host creation.</summary>
    protected virtual void ConfigureEnvironmentVariables() { }

    /// <summary>Override to register additional test services.</summary>
    protected virtual void ConfigureAdditionalServices(IServiceCollection services) { }

    /// <summary>Gets a DbContext from the service provider.</summary>
    public TDbContext GetDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TDbContext>();
    }

    /// <summary>Creates a new DbContext instance connected to the test database.</summary>
    public TDbContext CreateDbContext()
    {
        var connectionString = _postgresContainer!.GetConnectionString();
        var optionsBuilder = new DbContextOptionsBuilder<TDbContext>();
        optionsBuilder.UseNpgsql(connectionString);
        return (TDbContext)Activator.CreateInstance(typeof(TDbContext), optionsBuilder.Options)!;
    }

    private async Task ApplyMigrationsAsync()
    {
        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();
    }

    /// <summary>Truncates all tables to reset test database state.</summary>
    [SuppressMessage("Security", "EF1002:Gaps in SQL queries", Justification = "Table names are from information_schema, not user input.")]
    public async Task CleanDatabaseAsync()
    {
        await using var context = CreateDbContext();
        var tableNames = await context.Database
            .SqlQueryRaw<string>(
                @"SELECT table_name
                   FROM information_schema.tables
                   WHERE table_schema = 'public'
                   AND table_type = 'BASE TABLE'
                   AND table_name != '__EFMigrationsHistory'
                   AND table_name != 'permissions'
                   AND table_name != 'roles'
                   AND table_name != 'role_permissions'
                   ORDER BY table_name")
            .ToListAsync();

        foreach (var tableName in tableNames)
        {
            try
            {
#pragma warning disable EF1002
                await context.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE \"{tableName}\" RESTART IDENTITY CASCADE");
#pragma warning restore EF1002
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P01")
            {
                // Table doesn't exist — ignore
            }
        }
    }

    /// <summary>The RSA signing credentials for creating test JWT tokens.</summary>
    public SigningCredentials SigningCredentials => new(new RsaSecurityKey(_testRsa), SecurityAlgorithms.RsaSha256);

    /// <summary>Creates a signed JWT token for test authentication.</summary>
    public string CreateTestJwtToken(
        string userId = "test-user",
        string[]? roles = null,
        string[]? permissions = null,
        Dictionary<string, string>? additionalClaims = null)
    {
        var claims = new List<Claim>
        {
            new("sub", userId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("name", "Test Employee")
        };

        foreach (var role in roles ?? [])
            claims.Add(new Claim("role", role));

        foreach (var permission in permissions ?? [])
            claims.Add(new Claim("permissions", permission));

        if (additionalClaims != null)
            foreach (var (key, value) in additionalClaims)
                claims.Add(new Claim(key, value));

        var token = new JwtSecurityToken(
            issuer: "test-issuer",
            audience: "test-audience",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(new RsaSecurityKey(_testRsa), SecurityAlgorithms.RsaSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Creates an authenticated HTTP client with the specified permissions.</summary>
    public HttpClient CreateClientWithPermissions(params string[] permissions)
    {
        var token = CreateTestJwtToken(permissions: permissions);
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Creates an anonymous (unauthenticated) HTTP client.</summary>
    public HttpClient CreateAnonymousClient() => CreateClient();
}
