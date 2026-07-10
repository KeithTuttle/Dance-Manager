using DanceManager.Api.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace DanceManager.Api.Data;

/// <summary>
/// Used only by the EF Core tools (`dotnet ef migrations` / `database update`).
/// AppDbContext now requires an <see cref="ICurrentTenant"/>; design time has no
/// request, so we hand it an empty one. Migrations don't need a live tenant.
///
/// Reads the same configuration the app uses at runtime (appsettings.json +
/// user-secrets), so `dotnet ef` targets whatever database you've configured
/// locally rather than a hardcoded connection string.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets<AppDbContext>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = config.GetConnectionString("Default")
            ?? "Host=localhost;Port=5432;Database=dancemanager;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new AppDbContext(options, new CurrentTenant());
    }
}
