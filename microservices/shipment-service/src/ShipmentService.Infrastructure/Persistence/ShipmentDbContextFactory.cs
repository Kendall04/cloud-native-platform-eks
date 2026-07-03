using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Npgsql;
using ShipmentService.Infrastructure.Configuration;

namespace ShipmentService.Infrastructure.Persistence;

public sealed class ShipmentDbContextFactory : IDesignTimeDbContextFactory<ShipmentDbContext>
{
    public ShipmentDbContext CreateDbContext(string[] args)
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var apiDirectory = Path.GetFullPath(Path.Combine(currentDirectory, "../ShipmentService.Api"));
        var basePath = Directory.Exists(apiDirectory) ? apiDirectory : currentDirectory;

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Postgres")
            ?? "Host=localhost;Database=platform;Username=postgres;Password=postgres";
        var databaseOptions = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()
            ?? new DatabaseOptions();
        var npgsqlConnectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            SearchPath = databaseOptions.Schema
        };

        var optionsBuilder = new DbContextOptionsBuilder<ShipmentDbContext>();
        optionsBuilder.UseNpgsql(
            npgsqlConnectionStringBuilder.ConnectionString,
            npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(ShipmentDbContext).Assembly.FullName);
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", databaseOptions.Schema);
            });

        return new ShipmentDbContext(optionsBuilder.Options);
    }
}
