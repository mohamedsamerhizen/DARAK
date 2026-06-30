namespace DARAK.Api.Data;

public sealed class DemoSeedOptions
{
    public const string SectionName = "DemoSeed";

    public bool Enabled { get; set; }

    public bool SeedUsers { get; set; } = true;

    public string DemoPassword { get; set; } = string.Empty;

    public bool AllowProduction { get; set; }
}
