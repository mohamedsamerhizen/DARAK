using System.Text.RegularExpressions;
using FluentAssertions;

namespace DARAK.Tests;

public sealed class MigrationGovernancePass09Tests
{
    private static readonly string[] AllowedNoOpMigrations =
    {
        "20260610093701_Phase17FinancialConcurrencyTokens.cs",
        "20260616194734_Phase31FinancialGovernanceAdjustmentLinks.cs"
    };

    private static readonly string[] AllowedManualMigrationsWithoutDesigner =
    {
        "20260613121500_Phase3ARowVersionConcurrencyHandlers.cs",
        "20260613133000_Phase3BIndexesAndOutboxAtomicity.cs",
        "20260613143000_Phase5AOwnershipOccupancyLifecycle.cs"
    };

    [Fact]
    public void Pass09_CommercialReleasePackage_ShouldUseFinalReleaseGateOrExplicitEvidence()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "tools", "New-CommercialReleasePackage.ps1"));

        script.Should().Contain("Test-FinalReleaseGate.ps1");
        script.Should().Contain("EvidencePath");
        script.Should().Contain("SkipDatabase");
        script.Should().Contain("ConnectionString");
        script.Should().NotContain("Test-Phase7ReleaseGate.ps1 -SkipDotnet");
    }

    [Fact]
    public void Pass09_MigrationGovernance_ShouldDocumentHistoricalExceptions()
    {
        var root = FindRepositoryRoot();
        var governance = File.ReadAllText(Path.Combine(root, "docs", "Migration-Governance.md"));

        governance.Should().Contain("Historical no-op migrations");
        governance.Should().Contain("Historical manual migrations without designer files");
        governance.Should().Contain("Commercial Packaging Schema Gate");

        foreach (var migration in AllowedNoOpMigrations.Concat(AllowedManualMigrationsWithoutDesigner))
        {
            governance.Should().Contain(migration);
        }
    }

    [Fact]
    public void Pass09_MigrationHygiene_ShouldNotAllowUndocumentedNoOpMigrations()
    {
        var root = FindRepositoryRoot();
        var migrationsDir = Path.Combine(root, "DARAK.Api", "Migrations");
        var allowed = AllowedNoOpMigrations.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var undocumentedNoOpMigrations = Directory
            .GetFiles(migrationsDir, "*.cs")
            .Where(IsMainMigrationFile)
            .Where(IsNoOpMigration)
            .Select(Path.GetFileName)
            .Where(fileName => fileName is not null && !allowed.Contains(fileName))
            .ToArray();

        undocumentedNoOpMigrations.Should().BeEmpty("future empty migrations must be removed or explicitly documented");
    }

    [Fact]
    public void Pass09_MigrationHygiene_ShouldNotAllowUndocumentedUnpairedManualMigrations()
    {
        var root = FindRepositoryRoot();
        var migrationsDir = Path.Combine(root, "DARAK.Api", "Migrations");
        var allowed = AllowedManualMigrationsWithoutDesigner.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var undocumentedUnpairedMigrations = Directory
            .GetFiles(migrationsDir, "*.cs")
            .Where(IsMainMigrationFile)
            .Where(file => !File.Exists(Path.Combine(migrationsDir, Path.GetFileNameWithoutExtension(file) + ".Designer.cs")))
            .Select(Path.GetFileName)
            .Where(fileName => fileName is not null && !allowed.Contains(fileName))
            .ToArray();

        undocumentedUnpairedMigrations.Should().BeEmpty("manual migrations without designer metadata must be documented by exact file name");
    }

    private static bool IsMainMigrationFile(string path)
    {
        var fileName = Path.GetFileName(path);
        return !fileName.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase)
            && !fileName.Equals("ApplicationDbContextModelSnapshot.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNoOpMigration(string path)
    {
        var content = File.ReadAllText(path);
        var match = Regex.Match(
            content,
            @"protected override void Up\(MigrationBuilder migrationBuilder\)\s*\{(?<body>[\s\S]*?)\n\s*\}",
            RegexOptions.CultureInvariant);

        if (!match.Success)
        {
            return false;
        }

        return !Regex.IsMatch(match.Groups["body"].Value, @"migrationBuilder\.", RegexOptions.CultureInvariant);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DARAK.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("DARAK repository root could not be located.");
    }
}
