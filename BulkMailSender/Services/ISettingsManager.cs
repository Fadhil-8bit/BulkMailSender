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
    /// Gets a specific preset configuration by name (e.g., "Debug", "ProductionDefault").
    /// </summary>
    EmailSettings GetPreset(string presetName);

    /// <summary>
    /// Checks if there are currently saved settings in persistent storage.
    /// </summary>
    Task<bool> HasSavedSettingsAsync();

    /// <summary>
    /// Updates the current session with the provided settings.
    /// </summary>
    Task UpdateSessionAsync(EmailSettings settings);

    /// <summary>
    /// Gets the name of the current active profile.
    /// </summary>
    string CurrentProfileName { get; }

    /// <summary>
    /// Gets the active profile name from the session, returning 'Default' if empty.
    /// </summary>
    string GetActiveProfileName();

    /// <summary>
    /// Switches the active profile and loads its settings.
    /// </summary>
    Task SwitchProfileAsync(string profileName);

    /// <summary>
    /// Lists all available profiles.
    /// </summary>
    List<string> GetAvailableProfiles();

    /// <summary>
    /// Deletes the specified profile.
    /// </summary>
    Task DeleteProfileAsync(string profileName);
}
