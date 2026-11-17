using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

public class CurrentUserResolver : ICurrentUserResolver
{
    private readonly AppDbContext _db;

    public CurrentUserResolver(AppDbContext db)
    {
        _db = db;
    }

    public async Task<CurrentSecurityUser?> GetCurrentUserAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var windowsName = principal.Identity.Name;

        if (string.IsNullOrWhiteSpace(windowsName))
        {
            return null;
        }

        // Example:
        // AD\Domingo.DeJesus -> Domingo.DeJesus
        var userName = windowsName.Contains('\\')
            ? windowsName.Split('\\', 2)[1]
            : windowsName;

        userName = userName.Trim();

        var user = await _db.Users
            .AsNoTracking()
            .Where(x =>
                x.UserName == userName &&
                x.IsActive)
            .Select(x => new CurrentSecurityUser
            {
                UserID = x.UserID,
                UserName = x.UserName,
                Email = x.Email,
                IsActive = x.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);

        return user;
    }
}