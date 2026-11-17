using System.Security.Claims;

public interface ICurrentUserResolver
{
    Task<CurrentSecurityUser?> GetCurrentUserAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default);
}
