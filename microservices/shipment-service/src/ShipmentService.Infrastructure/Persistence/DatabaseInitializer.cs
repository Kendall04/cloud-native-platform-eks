using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using ShipmentService.Infrastructure.Configuration;

namespace ShipmentService.Infrastructure.Persistence;

public sealed class DatabaseInitializer(
    ShipmentDbContext dbContext,
    IOptions<DatabaseOptions> databaseOptions)
{
    private readonly DatabaseOptions _databaseOptions = databaseOptions.Value;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            $"""CREATE SCHEMA IF NOT EXISTS "{_databaseOptions.Schema}" """,
            cancellationToken);

        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
