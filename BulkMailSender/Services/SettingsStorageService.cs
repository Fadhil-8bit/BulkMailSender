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
        // Store settings in App_Data/Settings folder
        var appDataPath = Path.Combine(env.ContentRootPath, "App_Data");
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
    /// Delete saved settings file for a specific profile
    /// </summary>
    public async Task<bool> DeleteSettingsAsync(string profileName = DefaultProfileName)
    {
        var filePath = GetFilePath(profileName);
        await _fileLock.WaitAsync();
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation("Saved settings deleted from {FilePath}", filePath);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete saved settings {FilePath}", filePath);
            return false;
        }
        finally
        {
            _fileLock.Release();
        }
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
}
