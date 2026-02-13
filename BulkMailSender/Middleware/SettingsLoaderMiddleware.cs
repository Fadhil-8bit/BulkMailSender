using BulkMailSender.Services;
using BulkMailSender.Models;
using Microsoft.Extensions.Options;
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

    public async Task InvokeAsync(HttpContext context, SettingsStorageService settingsStorage, IOptions<EmailPresets> presets)
    {
        // 1. Sync Active Profile Name from disk to ensure we're using the latest selection
        // This handles cases where the profile was switched in another tab or stored state
        var activeProfileFromDisk = await settingsStorage.LoadActiveProfileNameAsync();
        var currentProfile = !string.IsNullOrEmpty(activeProfileFromDisk) ? activeProfileFromDisk : "smtp-settings";

        // Update session to match disk state
        context.Session.SetString("CurrentProfileName", currentProfile);

        // 2. FORCE LOAD from persistent storage every time (to support real-time updates)
        var persistedSettings = await settingsStorage.LoadSettingsAsync(currentProfile);

        if (persistedSettings != null)
        {
            var json = JsonSerializer.Serialize(persistedSettings);
            context.Session.SetString("SmtpSettings", json);
            // Logging at Debug level to avoid spamming logs on every request
            _logger.LogDebug("Loaded SMTP settings from storage (Profile: {Profile})", currentProfile);
        }
        else
        {
            // Fallback: unified appsettings.json (ProductionDefault)
            var defaultSettings = presets.Value.ProductionDefault;
            if (defaultSettings != null)
            {
                var json = JsonSerializer.Serialize(defaultSettings);
                context.Session.SetString("SmtpSettings", json);
                _logger.LogDebug("Loaded default 'ProductionDefault' into session.");
            }
            else
            {
                _logger.LogWarning("No persisted settings and no 'ProductionDefault' preset found.");
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
