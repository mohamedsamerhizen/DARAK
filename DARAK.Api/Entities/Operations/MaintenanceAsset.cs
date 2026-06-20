using DARAK.Api.Enums;

namespace DARAK.Api.Entities;

public sealed class MaintenanceAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CompoundId { get; set; }

    public Compound Compound { get; set; } = null!;

    public Guid? BuildingId { get; set; }

    public Building? Building { get; set; }

    public Guid? FloorId { get; set; }

    public Floor? Floor { get; set; }

    public Guid? PropertyUnitId { get; set; }

    public PropertyUnit? PropertyUnit { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public MaintenanceAssetType AssetType { get; set; } = MaintenanceAssetType.Other;

    public MaintenanceAssetStatus Status { get; set; } = MaintenanceAssetStatus.Active;

    public string? LocationDescription { get; set; }

    public string? Manufacturer { get; set; }

    public string? Model { get; set; }

    public string? SerialNumber { get; set; }

    public DateTime? InstalledAtUtc { get; set; }

    public DateTime? WarrantyExpiresAtUtc { get; set; }

    public DateTime? LastServiceAtUtc { get; set; }

    public DateTime? NextServiceDueAtUtc { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public ICollection<PreventiveMaintenancePlan> PreventiveMaintenancePlans { get; set; } = new List<PreventiveMaintenancePlan>();

    public ICollection<WorkOrder> WorkOrders { get; set; } = new List<WorkOrder>();
}
