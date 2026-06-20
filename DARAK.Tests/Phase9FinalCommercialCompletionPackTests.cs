using FluentAssertions;

namespace DARAK.Tests;

public sealed class Phase9FinalCommercialCompletionPackTests
{
    [Fact]
    public void Phase9CommercialHandoffDocuments_ShouldExist()
    {
        var requiredDocuments = new[]
        {
            Path.Combine("docs", "Financial-Disputes-Refunds-Governance.md"),
            Path.Combine("docs", "Admin-Reporting-Exports-Governance.md"),
            Path.Combine("docs", "Commercial-Feature-Matrix.md"),
            Path.Combine("docs", "API-Coverage.md"),
            Path.Combine("docs", "Buyer-Handoff.md"),
            Path.Combine("docs", "Security-Checklist.md"),
            Path.Combine("docs", "Testing-Evidence.md"),
            Path.Combine("docs", "Commercial-Value-Summary.md"),
            Path.Combine("docs", "Final-Status-Report.md")
        };

        foreach (var relativePath in requiredDocuments)
        {
            var path = RepositoryFile(relativePath);
            File.Exists(path).Should().BeTrue($"Phase 9 handoff document must exist: {path}");
            File.ReadAllText(path).Trim().Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void FinancialDisputesGovernance_ShouldDeclareNoForcedSchemaExpansion()
    {
        var source = ReadRepositoryFile("docs", "Financial-Disputes-Refunds-Governance.md");

        source.Should().Contain("Phase 9 does not add demo data, seed data, or a database migration");
        source.Should().Contain("duplicate active disputes");
        source.Should().Contain("Refunds and financial corrections are sensitive operations");
    }

    [Fact]
    public void FinalReleaseGate_ShouldValidateCommercialHandoffFilesAndArtifacts()
    {
        var script = ReadRepositoryFile("tools", "Test-FinalReleaseGate.ps1");

        script.Should().Contain("docs\\Financial-Disputes-Refunds-Governance.md");
        script.Should().Contain("docs\\Buyer-Handoff.md");
        script.Should().Contain("appsettings.Development.json");
        script.Should().Contain("dotnet build .\\DARAK.sln --no-incremental");
        script.Should().Contain("dotnet test .\\DARAK.sln --no-build");
    }

    [Fact]
    public void Readme_ShouldDeclarePhase9FinalCommercialCompletionPack()
    {
        var readme = ReadRepositoryFile("README.md");

        readme.Should().Contain("Phase 9 - Final Commercial Completion Pack");
        readme.Should().Contain("No migration is required for Phase 9");
        readme.Should().Contain("tools/Test-FinalReleaseGate.ps1");
    }

    private static string ReadRepositoryFile(params string[] parts)
    {
        var path = RepositoryFile(parts);
        File.Exists(path).Should().BeTrue($"repository file must exist: {path}");
        return File.ReadAllText(path);
    }

    private static string RepositoryFile(params string[] parts)
    {
        var root = FindRepositoryRoot();
        var pathParts = new string[parts.Length + 1];
        pathParts[0] = root;
        Array.Copy(parts, 0, pathParts, 1, parts.Length);
        return Path.Combine(pathParts);
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
