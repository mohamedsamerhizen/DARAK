namespace DARAK.Api.Enums;

public enum MaintenanceSlaStatus
{
    NotApplied = 0,
    WithinSla = 1,
    ResponseBreached = 2,
    ResolutionBreached = 3,
    Escalated = 4,
    Paused = 5,
    Completed = 6,
    Cancelled = 7
}
