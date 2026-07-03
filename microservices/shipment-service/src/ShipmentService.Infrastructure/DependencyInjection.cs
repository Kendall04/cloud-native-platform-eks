using Amazon;
using Amazon.EventBridge;
using Amazon.SQS;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipmentService.Application.Interfaces;
using ShipmentService.Infrastructure.Configuration;
using ShipmentService.Infrastructure.Messaging;
using ShipmentService.Infrastructure.Persistence;
using ShipmentService.Infrastructure.Services;

namespace ShipmentService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<AwsOptions>()
            .Bind(configuration.GetSection(AwsOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<EventPublishingOptions>()
            .Bind(configuration.GetSection(EventPublishingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<ShipmentConsumerOptions>()
            .Bind(configuration.GetSection(ShipmentConsumerOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' was not found.");
        var databaseOptions = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()
            ?? throw new InvalidOperationException("Database configuration was not found.");
        var npgsqlConnectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            SearchPath = databaseOptions.Schema
        };

        services.AddSingleton(TimeProvider.System);

        services.AddDbContext<ShipmentDbContext>(options =>
        {
            options.UseNpgsql(
                npgsqlConnectionStringBuilder.ConnectionString,
                npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(ShipmentDbContext).Assembly.FullName);
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", databaseOptions.Schema);
                });
        });

        services.AddHealthChecks().AddDbContextCheck<ShipmentDbContext>("postgres");

        services.AddSingleton<IAmazonEventBridge>(serviceProvider =>
        {
            var awsOptions = serviceProvider.GetRequiredService<
                Microsoft.Extensions.Options.IOptions<AwsOptions>>().Value;
            return new AmazonEventBridgeClient(RegionEndpoint.GetBySystemName(awsOptions.Region));
        });

        services.AddSingleton<IAmazonSQS>(serviceProvider =>
        {
            var awsOptions = serviceProvider.GetRequiredService<
                Microsoft.Extensions.Options.IOptions<AwsOptions>>().Value;
            return new AmazonSQSClient(RegionEndpoint.GetBySystemName(awsOptions.Region));
        });

        services.AddScoped<IShipmentService, ShipmentApplicationService>();
        services.AddScoped<IAdminShipmentService, AdminShipmentService>();
        services.AddScoped<ITrackingEventProcessor, TrackingEventProcessor>();
        services.AddSingleton<ITrackingNumberGenerator, TrackingNumberGenerator>();
        services.AddSingleton<IShipmentEventPublisher, EventBridgeShipmentEventPublisher>();
        services.AddScoped<DatabaseInitializer>();
        services.AddHostedService<SqsTrackingEventsConsumer>();

        return services;
    }
}
