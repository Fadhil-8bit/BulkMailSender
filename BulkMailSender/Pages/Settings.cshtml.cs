using System.Text.Json;
using BulkMailSender.Models;
using BulkMailSender.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Mail;
using System.Net;

namespace BulkMailSender.Pages;

public class SettingsModel : PageModel
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SettingsModel> _logger;
    private readonly SettingsStorageService _settingsStorage;

    public SettingsModel(
        IConfiguration configuration, 
        ILogger<SettingsModel> logger,
        SettingsStorageService settingsStorage)
    {
        _configuration = configuration;
        _logger = logger;
        _settingsStorage = settingsStorage;
    }

    [BindProperty]
    public SmtpSettings CurrentSettings { get; set; } = new();

    public string? TestResult { get; set; }
    public bool TestSuccess { get; set; }
    public bool HasSavedSettings { get; set; }

    public async Task OnGetAsync()
    {
        // Try to load from persistent storage first
        var savedSettings = await _settingsStorage.LoadSettingsAsync();
        if (savedSettings != null)
        {
            CurrentSettings = savedSettings;
            HasSavedSettings = true;
            
            // IMPORTANT: Also save to session so other pages can use it
            HttpContext.Session.SetString("SmtpSettings", JsonSerializer.Serialize(savedSettings));
            
            _logger.LogInformation("Loaded settings from persistent storage and updated session");
        }
        else
        {
            // Fall back to session
            var settingsJson = HttpContext.Session.GetString("SmtpSettings");
            if (!string.IsNullOrEmpty(settingsJson))
            {
                try
                {
                    CurrentSettings = JsonSerializer.Deserialize<SmtpSettings>(settingsJson) ?? new SmtpSettings();
                    _logger.LogInformation("Loaded settings from session");
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load SMTP settings from session");
                }
            }

            // Load default from appsettings.json as last resort
            LoadDefaultSettings();
        }
    }

    public async Task<IActionResult> OnPostUseDefaultAsync()
    {
        ModelState.Clear();
        LoadDefaultSettings();
        await SaveToStorageAndSessionAsync();
        TestResult = "Default settings loaded and saved. Click 'Test Connection' to verify.";
        TestSuccess = false;
        HasSavedSettings = _settingsStorage.HasSavedSettings();
        return Page();
    }

    public async Task<IActionResult> OnPostUsePaperCutAsync()
    {
        ModelState.Clear();
        LoadPaperCutSettings();
        await SaveToStorageAndSessionAsync();
        TestResult = "PaperCut settings loaded and saved. Make sure PaperCut SMTP is running on localhost:25.";
        TestSuccess = false;
        HasSavedSettings = _settingsStorage.HasSavedSettings();
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        await SaveToStorageAndSessionAsync();
        TestResult = "? Settings saved successfully! They will persist across restarts.";
        TestSuccess = true;
        HasSavedSettings = _settingsStorage.HasSavedSettings();
        return Page();
    }

    public async Task<IActionResult> OnPostTestConnectionAsync()
    {
        try
        {
            using var client = new SmtpClient(CurrentSettings.Host, CurrentSettings.Port)
            {
                Credentials = string.IsNullOrWhiteSpace(CurrentSettings.Username)
                    ? null
                    : new NetworkCredential(CurrentSettings.Username, CurrentSettings.Password),
                EnableSsl = CurrentSettings.EnableSsl,
                Timeout = CurrentSettings.TimeoutSeconds * 1000
            };

            var testMessage = new MailMessage
            {
                From = new MailAddress(CurrentSettings.FromEmail, CurrentSettings.FromName),
                Subject = "ATP Bulk Mailer - Test Connection",
                Body = "This is a test email from ATP Bulk Mail Sender.\n\nIf you receive this, your SMTP settings are correct.",
                IsBodyHtml = false
            };
            testMessage.To.Add(new MailAddress(CurrentSettings.FromEmail));

            await client.SendMailAsync(testMessage);

            TestResult = $"? SUCCESS! Test email sent to {CurrentSettings.FromEmail}. Check your inbox.";
            TestSuccess = true;
            await SaveToStorageAndSessionAsync();
            _logger.LogInformation("SMTP test successful: {Host}:{Port}", CurrentSettings.Host, CurrentSettings.Port);
        }
        catch (Exception ex)
        {
            TestResult = $"? FAILED: {ex.Message}";
            TestSuccess = false;
            _logger.LogError(ex, "SMTP test failed");
        }

        HasSavedSettings = _settingsStorage.HasSavedSettings();
        return Page();
    }

    public async Task<IActionResult> OnPostClearSettingsAsync()
    {
        await _settingsStorage.DeleteSettingsAsync();
        HttpContext.Session.Remove("SmtpSettings");
        CurrentSettings = new SmtpSettings();
        TestResult = "?? Saved settings cleared. Using defaults from appsettings.json.";
        TestSuccess = false;
        HasSavedSettings = false;
        LoadDefaultSettings();
        return Page();
    }

    private void LoadDefaultSettings()
    {
        var settings = _configuration.GetSection("SmtpDefault").Get<SmtpSettings>();
        if (settings != null)
        {
            CurrentSettings = settings;
        }
    }

    private void LoadPaperCutSettings()
    {
        var settings = _configuration.GetSection("SmtpPaperCut").Get<SmtpSettings>();
        if (settings != null)
        {
            CurrentSettings = settings;
        }
    }

    private async Task SaveToStorageAndSessionAsync()
    {
        // Save to persistent storage
        await _settingsStorage.SaveSettingsAsync(CurrentSettings);
        
        // Also save to session for immediate use
        HttpContext.Session.SetString("SmtpSettings", JsonSerializer.Serialize(CurrentSettings));
    }
}
