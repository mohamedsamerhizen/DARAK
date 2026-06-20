using FluentAssertions;

namespace DARAK.Tests;

public sealed class FinalCommercialHardeningTests
{
    [Fact]
    public void Phase23_CommercialHandoverDocs_ShouldExistWithBuyerCriticalSections()
    {
        var root = FindRepositoryRoot();
        var requiredDocs = new[]
        {
            "Commercial-Handover-Report.md",
            "Production-Readiness-Checklist.md",
            "Security-Authorization-Final-Review.md",
            "Deployment-Runbook.md",
            "Buyer-Operations-Runbook.md",
            "Final-Commercial-Release-Notes.md"
        };

        foreach (var doc in requiredDocs)
        {
            var path = Path.Combine(root, "docs", doc);
            File.Exists(path).Should().BeTrue($"{doc} must be included in the commercial handover package");
            File.ReadAllText(path).Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void Phase23_ProductionReadinessChecklist_ShouldCoverSecurityDataOperationsAndHandover()
    {
        var root = FindRepositoryRoot();
        var checklist = File.ReadAllText(Path.Combine(root, "docs", "Production-Readiness-Checklist.md"));

        checklist.Should().Contain("Build and Test Gate");
        checklist.Should().Contain("Security Gate");
        checklist.Should().Contain("Data Integrity Gate");
        checklist.Should().Contain("Operations Gate");
        checklist.Should().Contain("Handover Gate");
        checklist.Should().Contain(".env is not included");
        checklist.Should().Contain("Database backups");
    }

    [Fact]
    public void Phase23_SecurityReview_ShouldDocumentRoleAndCompoundBoundaries()
    {
        var root = FindRepositoryRoot();
        var review = File.ReadAllText(Path.Combine(root, "docs", "Security-Authorization-Final-Review.md"));

        review.Should().Contain("Resident");
        review.Should().Contain("Guard");
        review.Should().Contain("CompoundAdmin");
        review.Should().Contain("Accountant");
        review.Should().Contain("SuperAdmin");
        review.ToLowerInvariant().Should().Contain("compound");
        review.Should().Contain("Approval decisions");
    }

    [Fact]
    public void Phase23_CommercialReadinessScript_ShouldBlockLocalArtifactsAndSecrets()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "tools", "Test-CommercialReadiness.ps1"));

        script.Should().Contain("DARAK.sln");
        script.Should().Contain("Commercial-Handover-Report.md");
        script.Should().Contain(".env exists");
        script.Should().Contain("*.json,*.cs,*.csproj,*.ps1,*.md,*.yml,*.yaml,*.example");
        script.Should().Contain("Darak_dev_");
        script.Should().Contain("2026!");
        script.Should().NotContain("Darak_dev_" + "2026!");
        script.Should().Contain("Commercial readiness validation passed");
        script.Should().Contain(".zip");
        script.Should().Contain("DARAK.Api\\App_Data\\Uploads");
    }

    [Fact]
    public void Phase23_CommercialReleaseScript_ShouldCleanValidateAndCreateBuyerPackage()
    {
        var root = FindRepositoryRoot();
        var script = File.ReadAllText(Path.Combine(root, "tools", "New-CommercialReleasePackage.ps1"));

        script.Should().Contain("Clean-BeforeGitHub.ps1");
        script.Should().Contain("Test-CommercialReadiness.ps1");
        script.Should().Contain("DARAK-commercial-release.zip");
        script.Should().Contain("Compress-Archive");
        script.Should().Contain(".env.local.backup");
    }

    [Fact]
    public void Phase23_Readme_ShouldDocumentFinalCommercialWorkflow()
    {
        var root = FindRepositoryRoot();
        var readme = File.ReadAllText(Path.Combine(root, "README.md"));

        readme.Should().Contain("Phase 23");
        readme.Should().Contain("Final Commercial Review & Hardening Pass");
        readme.Should().Contain("Test-CommercialReadiness.ps1");
        readme.Should().Contain("New-CommercialReleasePackage.ps1");
        readme.Should().Contain("Migration required: none");
    }

    [Fact]
    public void Phase23_DockerAndGitIgnore_ShouldKeepCommercialPackageClean()
    {
        var root = FindRepositoryRoot();
        var gitIgnore = File.ReadAllText(Path.Combine(root, ".gitignore"));
        var dockerIgnore = File.ReadAllText(Path.Combine(root, ".dockerignore"));

        gitIgnore.Should().Contain("*.zip");
        gitIgnore.Should().Contain(".env");
        gitIgnore.Should().Contain("DARAK.Api/App_Data/Uploads/");
        dockerIgnore.Should().Contain("**/*.zip");
        dockerIgnore.Should().Contain(".env.*");
        dockerIgnore.Should().Contain("DARAK.Api/App_Data/Uploads");
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



