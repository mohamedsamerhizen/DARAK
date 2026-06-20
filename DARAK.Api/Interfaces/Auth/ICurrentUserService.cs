namespace DARAK.Api.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
}
