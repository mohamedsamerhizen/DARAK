using FluentAssertions;

namespace DARAK.Tests;

public sealed class Pass20CommercialReadinessTests
{
    [Fact]
    public void Pass20_GitHubActionsWorkflow_ShouldRunBuildAndTests()
    {
        var workflow = ReadRepositoryFile(".github", "workflows", "dotnet.yml");

        workflow.Should().Contain("actions/checkout@v4");
        workflow.Should().Contain("actions/setup-dotnet@v4");
        workflow.Should().Contain("dotnet-version: '10.0.x'");
        workflow.Should().Contain("dotnet restore ./DARAK.sln");
        workflow.Should().Contain("dotnet build ./DARAK.sln --configuration Release --no-restore");
        workflow.Should().Contain("dotnet test ./DARAK.sln --configuration Release --no-build");
        workflow.Should().Contain("workflow_dispatch");
    }

    [Fact]
    public void Pass20_ReleaseGateScripts_ShouldNotCarryObsoleteStaticTestCount()
    {
        var phase7Gate = ReadRepositoryFile("tools", "Test-Phase7ReleaseGate.ps1");
        var finalGate = ReadRepositoryFile("tools", "Test-Phase1FinalHardening.ps1");

        phase7Gate.Should().NotContain("ExpectedTestCount = \"369\"");
        finalGate.Should().NotContain("ExpectedTestCount = \"369\"");
        phase7Gate.Should().Contain("[string]$ExpectedTestCount");
        finalGate.Should().Contain("[string]$ExpectedTestCount");
        phase7Gate.Should().Contain("if (-not [string]::IsNullOrWhiteSpace($ExpectedTestCount))");
        finalGate.Should().Contain("if (-not [string]::IsNullOrWhiteSpace($ExpectedTestCount))");
    }

    [Fact]
    public void Pass20_FinalHardeningGateScript_ShouldNotContainKnownMalformedPowerShellBlocks()
    {
        var finalGate = ReadRepositoryFile("tools", "Test-Phase1FinalHardening.ps1");

        finalGate.Should().NotContain("Where-Object {)");
        finalGate.Should().NotContain("-Include *.json,*.cs,*.csproj,*.ps1,*.md,*.yml,*.yaml,*.example -ErrorAction SilentlyContinue |)");
        finalGate.Should().Contain("$blocked = @(Get-ChildItem -Path $root -Force -Recurse -ErrorAction SilentlyContinue | Where-Object {");
        finalGate.Should().Contain("$secretScanFiles = @(Get-ChildItem -Path $root -File -Recurse -Include *.json,*.cs,*.csproj,*.ps1,*.md,*.yml,*.yaml,*.example -ErrorAction SilentlyContinue | Where-Object {");
    }

    [Fact]
    public void Pass20_RepositoryRoot_ShouldNotContainLegacyPhaseReadmes()
    {
        var root = FindRepositoryRoot();

        Directory.GetFiles(root, "README-PHASE*.md", SearchOption.TopDirectoryOnly)
            .Should()
            .BeEmpty("phase-specific README files belong under docs/Phase-Readme-Archive, not the repository root");
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
