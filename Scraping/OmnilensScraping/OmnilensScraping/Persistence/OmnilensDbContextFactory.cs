using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OmnilensScraping.Persistence;

public sealed class OmnilensDbContextFactory : IDesignTimeDbContextFactory<OmnilensDbContext>
{
    public OmnilensDbContext CreateDbContext(string[] args)
    {
        var basePath = Directory.GetCurrentDirectory();
        var builder = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables();

        var configuration = builder.Build();
        var databaseOptions = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();

        var filePath = Path.IsPathRooted(databaseOptions.FilePath)
            ? databaseOptions.FilePath
            : Path.Combine(basePath, databaseOptions.FilePath);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var optionsBuilder = new DbContextOptionsBuilder<OmnilensDbContext>();
        optionsBuilder.UseSqlite($"Data Source={filePath}");
        return new OmnilensDbContext(optionsBuilder.Options);
    }
}
