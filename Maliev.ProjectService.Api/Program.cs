using Maliev.ProjectService.Application.Abstractions;
using Maliev.ProjectService.Infrastructure.HttpClients;
using Maliev.ProjectService.Infrastructure.Consumers;
using Maliev.ProjectService.Infrastructure.Persistence;
using Maliev.ProjectService.Infrastructure.Services;
using Maliev.Aspire.ServiceDefaults;

// Initialize bootstrap logging
using var loggerFactory = LoggerFactory.Create(logBuilder => logBuilder.AddConsole());
var bootstrapLogger = loggerFactory.CreateLogger("Program");

try
{
    Program.Log.StartingHost(bootstrapLogger, "Project Service");

    var builder = WebApplication.CreateBuilder(args);

    // --- Secrets & Configuration ---
    builder.AddGoogleSecretManagerVolume();

    // --- Infrastructure & Observability ---
    builder.AddServiceDefaults();
    builder.AddStandardMiddleware(options =>
    {
        options.EnableRequestLogging = true;
    });
    builder.AddServiceMeters("project-meter");

    builder.Services.AddHttpContextAccessor();

    builder.AddPostgresDbContext<ProjectDbContext>(connectionName: "ProjectDbContext");
    builder.AddStandardCache("project:");
    builder.AddMassTransitWithRabbitMq(x =>
    {
        x.AddConsumer<QuotationAcceptedEventConsumer>();
        x.AddConsumer<OrderCreatedEventConsumer>();
        x.AddConsumer<JobStatusChangedEventConsumer>();
    });

    builder.AddStandardCors();
    builder.AddDefaultApiVersioning();
    builder.AddJwtAuthentication();

    if (!builder.Environment.IsProduction())
    {
        builder.AddStandardOpenApi(
            title: "MALIEV Project Service API",
            description: "Project lifecycle management — orchestrates customer engagement from quoting through delivery.");
    }

    builder.Services.AddControllers();

    // Application services
    builder.Services.AddScoped<ProjectNumberGenerator>();
    builder.Services.AddScoped<IProjectService, ProjectManagementService>();

    // External HTTP clients
    builder.Services.AddHttpClient<IPricingServiceClient, PricingServiceClient>(client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["PricingService:BaseUrl"] ?? "http://pricing-service");
    }).AddHttpMessageHandler<Maliev.Aspire.ServiceDefaults.IAM.ServiceAccountAuthenticationHandler>();

    builder.Services.AddHttpClient<IQuotationServiceClient, QuotationServiceClient>(client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["QuotationService:BaseUrl"] ?? "http://quotation-service");
    }).AddHttpMessageHandler<Maliev.Aspire.ServiceDefaults.IAM.ServiceAccountAuthenticationHandler>();

    builder.Services.AddPermissionAuthorization();
    builder.AddIAMServiceClient("project");

    var app = builder.Build();
    var logger = app.Services.GetRequiredService<ILogger<Program>>();

    await app.MigrateDatabaseAsync<ProjectDbContext>();

    app.UseStandardMiddleware();
    if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();
    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapDefaultEndpoints(servicePrefix: "project");
    app.MapApiDocumentation(servicePrefix: "project");

    Program.Log.ServiceStarted(logger, "Project Service");
    await app.RunAsync();
}
catch (Exception ex)
{
    Program.Log.HostTerminated(bootstrapLogger, ex, "Project Service");
    throw;
}
finally
{
    loggerFactory.Dispose();
}

/// <summary>
/// Main program class for the Project Service application.
/// </summary>
public partial class Program
{
    internal static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Starting {ServiceName} host")]
        public static partial void StartingHost(ILogger logger, string serviceName);

        [LoggerMessage(Level = LogLevel.Critical, Message = "{ServiceName} host terminated unexpectedly during startup")]
        public static partial void HostTerminated(ILogger logger, Exception ex, string serviceName);

        [LoggerMessage(Level = LogLevel.Information, Message = "{ServiceName} started successfully")]
        public static partial void ServiceStarted(ILogger logger, string serviceName);
    }
}
