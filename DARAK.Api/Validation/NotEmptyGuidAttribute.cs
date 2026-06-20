using System.ComponentModel.DataAnnotations;

namespace DARAK.Api.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class NotEmptyGuidAttribute : ValidationAttribute
{
    public NotEmptyGuidAttribute()
        : base("{0} must not be empty.")
    {
    }

    public override bool IsValid(object? value)
    {
        return value is not Guid guid || guid != Guid.Empty;
    }
}
