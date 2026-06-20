using System.Security.Claims;
using DARAK.Api.Interfaces;

namespace DARAK.Api.Services;

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor)
    : ICurrentUserService
{
    public Guid? UserId
    {
        get
        {
            var userIdValue = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(userIdValue, out var userId) ? userId : null;
        }
    }
}
