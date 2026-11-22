using Microsoft.AspNetCore.Localization;

namespace ValIO.Web.Endpoints;

/// <summary>GET /culture/set?culture=ru&amp;returnUrl=/generate — sets the culture cookie and redirects.</summary>
public static class CultureEndpoint
{
    /// <summary>Stores the chosen supported culture and returns the browser to the requested page.</summary>
    /// <param name="ctx">The HTTP context receiving the culture cookie.</param>
    /// <param name="culture">The requested culture identifier.</param>
    /// <param name="returnUrl">The page to open after changing the culture.</param>
    /// <param name="clock">The host clock used to set the cookie expiration.</param>
    /// <returns>A redirect for a supported culture or a request error.</returns>
    public static IResult SetCulture(HttpContext ctx, string culture, string? returnUrl, TimeProvider clock)
    {
        var allowed = new[] { "en", "ru", "de", "zh-CN" };
        if (!allowed.Contains(culture, StringComparer.OrdinalIgnoreCase))
            return Results.BadRequest($"Unsupported culture: {culture}");

        ctx.Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = clock.GetUtcNow().AddYears(1), IsEssential = true });

        return Results.Redirect(string.IsNullOrEmpty(returnUrl) ? "/" : "/" + returnUrl);
    }
}
