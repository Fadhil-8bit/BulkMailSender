using BulkMailSender.Services;
using System.Text.Json;

namespace BulkMailSender.Middleware;

/// <summary>
/// Middleware to automatically load SMTP settings from persistent storage into session on first request
/// </summary>
public class SettingsLoaderMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SettingsLoaderMiddleware> _logger;

    public SettingsLoaderMiddleware(RequestDelegate next, ILogger<SettingsLoaderMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, SettingsStorageService settingsStorage)
    {
        // Check if session already has settings
        var sessionSettings = context.Session.GetString("SmtpSettings");
        
        if (string.IsNullOrEmpty(sessionSettings))
        {
            // Try to load from persistent storage
            var persistedSettings = await settingsStorage.LoadSettingsAsync();
            
            if (persistedSettings != null)
            {
                // Save to session for this and future requests
                var json = JsonSerializer.Serialize(persistedSettings);
                context.Session.SetString("SmtpSettings", json);
                
                _logger.LogInformation("Auto-loaded SMTP settings from persistent storage into session");
            }
            else
            {
                _logger.LogInformation("No persisted settings found - will use defaults when needed");
            }
        }

        await _next(context);
    }
}

/// <summary>
/// Extension method to easily add the middleware
/// </summary>
public static class SettingsLoaderMiddlewareExtensions
{
    public static IApplicationBuilder UseSettingsLoader(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<SettingsLoaderMiddleware>();
    }
}
