using MarketHub.Api.Models;
using MarketHub.Api.Services;

namespace MarketHub.Api.Endpoints;

internal static class EndpointSecurity
{
    public static User? CurrentUser(HttpContext http, MarketplaceStore store)
    {
        return http.Request.Cookies.TryGetValue(SessionCookie.Name, out var sessionId)
            ? store.GetSessionUser(sessionId)
            : null;
    }

    public static User? RequireRole(HttpContext http, MarketplaceStore store, params Role[] roles)
    {
        var user = CurrentUser(http, store);
        return user is not null && roles.Contains(user.Role) ? user : null;
    }

    public static void SignIn(HttpContext http, MarketplaceStore store, User user)
    {
        var sessionId = store.CreateSession(user.Id);
        http.Response.Cookies.Append(SessionCookie.Name, sessionId, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = false,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        });
    }
}
