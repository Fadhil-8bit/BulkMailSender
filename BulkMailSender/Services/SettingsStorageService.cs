using System.Text.Json;
using BulkMailSender.Models;

namespace BulkMailSender.Services;

/// <summary>
/// Service for persisting SMTP settings to a JSON file with support for multiple profiles
/// </summary>
public class SettingsStorageService
{
    private readonly string _settingsDirectory;
    private readonly ILogger<SettingsStorageService> _logger;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private const string DefaultProfileName = "smtp-settings";

    public SettingsStorageService(IWebHostEnvironment env, ILogger<SettingsStorageService> logger)
    {
        _logger = logger;
        // Store settings in App_Data/Settings folder relative to running binaries
        var appDataPath = Path.Combine(AppContext.BaseDirectory, "App_Data");
        _settingsDirectory = Path.Combine(appDataPath, "Settings");
        Directory.CreateDirectory(_settingsDirectory);

        // Migration check: Move old file if it exists
        var oldPath = Path.Combine(appDataPath, "smtp-settings.json");
        var newPath = Path.Combine(_settingsDirectory, $"{DefaultProfileName}.json");
        if (File.Exists(oldPath) && !File.Exists(newPath))
        {
            try
            {
                File.Move(oldPath, newPath);
                _logger.LogInformation("Migrated old settings file to {NewPath}", newPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to migrate old settings file");
            }
        }
    }

    private string GetFilePath(string profileName)
    {
        // Simple sanitization to prevent directory traversal
        var safeName = string.Join("_", profileName.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_settingsDirectory, $"{safeName}.json");
    }

    /// <summary>
    /// Save SMTP settings to JSON file for a specific profile
    /// </summary>
    public async Task<bool> SaveSettingsAsync(EmailSettings settings, string profileName = DefaultProfileName)
    {
        var filePath = GetFilePath(profileName);
        await _fileLock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(filePath, json);
            _logger.LogInformation("SMTP settings saved to {FilePath}", filePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save SMTP settings to file {FilePath}", filePath);
            return false;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Load SMTP settings from JSON file for a specific profile
    /// </summary>
    public async Task<EmailSettings?> LoadSettingsAsync(string profileName = DefaultProfileName)
    {
        var filePath = GetFilePath(profileName);
        await _fileLock.WaitAsync();
        try
        {
            if (!File.Exists(filePath))
            {
                _logger.LogInformation("No saved settings file found at {FilePath}", filePath);
                return null;
            }

            var json = await File.ReadAllTextAsync(filePath);
            var settings = JsonSerializer.Deserialize<EmailSettings>(json);
            
            if (settings != null)
            {
                _logger.LogInformation("SMTP settings loaded from {FilePath}", filePath);
            }
            
            return settings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load SMTP settings from file {FilePath}", filePath);
            return null;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Check if saved settings exist for a specific profile
    /// </summary>
    public bool HasSavedSettings(string profileName = DefaultProfileName)
    {
        return File.Exists(GetFilePath(profileName));
    }

    /// <summary>
    /// List all available profiles
    /// </summary>
    public List<string> ListProfiles()
    {
        try
        {
            return Directory.GetFiles(_settingsDirectory, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(x => !string.IsNullOrEmpty(x))
                .ToList()!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list profiles");
            return new List<string>();
        }
    }

    /// <summary>
    /// Deletes a specific profile file.
    /// </summary>
    public void DeleteProfileFile(string profileName)
    {
        var filePath = GetFilePath(profileName);
        _fileLock.Wait();
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation("Deleted profile file: {FilePath}", filePath);
            }
            else
            {
                _logger.LogWarning("Cannot find profile file to delete: {FilePath}", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while deleting profile file: {FilePath}", filePath);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Save the name of the currently active profile to a file named active_profile.txt within the _settingsDirectory.
    /// </summary>
    public async Task SaveActiveProfileNameAsync(string profileName)
    {
        var filePath = Path.Combine(_settingsDirectory, "active_profile.txt");
        await _fileLock.WaitAsync();
        try
        {
            await File.WriteAllTextAsync(filePath, profileName);
            _logger.LogInformation("Saved active profile name '{ProfileName}' to {FilePath}", profileName, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save active profile name to {FilePath}", filePath);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    ///  Read that file and return its content, defaulting to 'smtp-settings' if the file doesn't exist.
    /// </summary>
    public async Task<string> GetSavedActiveProfileNameAsync()
    {
        var filePath = Path.Combine(_settingsDirectory, "active_profile.txt");
        await _fileLock.WaitAsync();
        try
        {
            if (File.Exists(filePath))
            {
                var content = await File.ReadAllTextAsync(filePath);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    return content.Trim();
                }
            }
            return DefaultProfileName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read active profile name from {FilePath}", filePath);
            return DefaultProfileName;
        }
        finally
        {
            _fileLock.Release();
        }
    }
}
