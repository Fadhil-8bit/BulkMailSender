using BulkMailSender.Models;

namespace BulkMailSender.Services;

public interface ISettingsManager
{
    /// <summary>
    /// Gets the active Email Configuration.
    /// Priority: 1. Persistent Storage (smtp-settings.json), 2. Production Default (appsettings.json)
    /// </summary>
    Task<EmailSettings> GetActiveSettingsAsync();

    /// <summary>
    /// Saves the provided settings to persistent storage.
    /// </summary>
    Task SaveSettingsAsync(EmailSettings settings);

    /// <summary>
    /// Clears any saved settings from persistent storage.
    /// </summary>
    Task ClearSettingsAsync();

    /// <summary>
    /// Gets a specific preset configuration by name (e.g., "Debug", "ProductionDefault").
    /// </summary>
    EmailSettings GetPreset(string presetName);

    /// <summary>
    /// Checks if there are currently saved settings in persistent storage.
    /// </summary>
    Task<bool> HasSavedSettingsAsync();
}
