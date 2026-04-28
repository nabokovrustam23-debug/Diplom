namespace BarbershopCrm.Web.Common;

/// <summary>
/// Добавляет защитные HTTP-заголовки на каждый ответ. Сводит к минимуму
/// поверхность XSS, clickjacking, MIME-sniffing и утечек реферера.
/// CSP сделан мягким (разрешает inline стили и собственные скрипты),
/// чтобы не ломать существующие Razor Pages с inline-обработчиками.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext ctx)
    {
        var headers = ctx.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
        // CSP: разрешаем self-источники, inline-стили (используются в Razor),
        // картинки/шрифты/фонты — со своего домена и data:.
        headers["Content-Security-Policy"] =
            "default-src 'self'; " +
            "script-src 'self' 'unsafe-inline'; " +
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data: https:; " +
            "font-src 'self' data:; " +
            "connect-src 'self'; " +
            "frame-ancestors 'none'; " +
            "base-uri 'self'; " +
            "form-action 'self'";
        await _next(ctx);
    }
}
