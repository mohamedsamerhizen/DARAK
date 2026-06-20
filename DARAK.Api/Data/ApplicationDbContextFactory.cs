using DARAK.Api.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DARAK.Api.Data;

public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var basePath = ResolveBasePath();
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        ValidateDesignTimeConnectionString(connectionString);

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(connectionString!);

        return new ApplicationDbContext(optionsBuilder.Options);
    }

    private static void ValidateDesignTimeConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Design-time connection string 'DefaultConnection' is not configured. Set ConnectionStrings__DefaultConnection or pass --connection to EF commands.");
        }

        if (StartupSecurityValidator.IsPlaceholderSecret(connectionString))
        {
            throw new InvalidOperationException(
                "Design-time connection string 'DefaultConnection' still contains a placeholder value. Set ConnectionStrings__DefaultConnection or pass --connection with a real local SQL Server connection string before running EF commands.");
        }
    }

    private static string ResolveBasePath()
    {
        var currentDirectory = Directory.GetCurrentDirectory();

        if (File.Exists(Path.Combine(currentDirectory, "appsettings.json")))
        {
            return currentDirectory;
        }

        var apiDirectory = Path.Combine(currentDirectory, "DARAK.Api");

        if (File.Exists(Path.Combine(apiDirectory, "appsettings.json")))
        {
            return apiDirectory;
        }

        return currentDirectory;
    }
}