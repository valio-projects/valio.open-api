using Microsoft.AspNetCore.Localization;

namespace ValIO.Web.Endpoints;

/// <summary>GET /culture/set?culture=ru&amp;returnUrl=/generate — sets the culture cookie and redirects.</summary>
public static class CultureEndpoint
{
    public static IResult SetCulture(HttpContext ctx, string culture, string? returnUrl)
    {
        var allowed = new[] { "en", "ru", "de", "zh-CN" };
        if (!allowed.Contains(culture, StringComparer.OrdinalIgnoreCase))
            return Results.BadRequest($"Unsupported culture: {culture}");

        ctx.Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true });

        return Results.Redirect(string.IsNullOrEmpty(returnUrl) ? "/" : "/" + returnUrl);
    }
}
