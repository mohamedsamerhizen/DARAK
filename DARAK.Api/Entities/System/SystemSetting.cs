using DARAK.Api.Enums;
using DARAK.Api.Identity;

namespace DARAK.Api.Entities;

public sealed class SystemSetting
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? CompoundId { get; set; }

    public Compound? Compound { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public SystemSettingValueType ValueType { get; set; } = SystemSettingValueType.String;

    public SystemSettingScope Scope => CompoundId.HasValue ? SystemSettingScope.Compound : SystemSettingScope.Global;

    public string? Description { get; set; }

    public bool IsSensitive { get; set; }

    public bool IsReadOnly { get; set; }

    public Guid? UpdatedByUserId { get; set; }

    public ApplicationUser? UpdatedByUser { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }
}
