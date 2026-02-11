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
    private readonly ISettingsManager _settingsManager;
    private readonly IEmailService _emailService;

    public SettingsModel(
        IConfiguration configuration, 
        ILogger<SettingsModel> logger,
        ISettingsManager settingsManager,
        IEmailService emailService)
    {
        _configuration = configuration;
        _logger = logger;
        _settingsManager = settingsManager;
        _emailService = emailService;
    }

    [BindProperty]
    public EmailSettings CurrentSettings { get; set; } = new();

    public string? TestResult { get; set; }
    public bool TestSuccess { get; set; }
    public bool HasSavedSettings { get; set; }

    public async Task OnGetAsync()
    {
        CurrentSettings = await _settingsManager.GetActiveSettingsAsync();
        HasSavedSettings = await _settingsManager.HasSavedSettingsAsync();

        HttpContext.Session.SetString("SmtpSettings", JsonSerializer.Serialize(CurrentSettings));
        
        if (HasSavedSettings)
        {
             _logger.LogInformation("Loaded settings from persistent storage and updated session");
        }
    }

    public async Task<IActionResult> OnPostUseDefaultAsync()
    {
        ModelState.Clear();
        CurrentSettings = _settingsManager.GetPreset("ProductionDefault");
        await SaveToStorageAndSessionAsync();
        TestResult = "? Default settings loaded and saved. Ready to use!";
        TestSuccess = true;
        HasSavedSettings = await _settingsManager.HasSavedSettingsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostUsePaperCutAsync()
    {
        ModelState.Clear();
        CurrentSettings = _settingsManager.GetPreset("Debug");
        await SaveToStorageAndSessionAsync();
        TestResult = "? PaperCut settings loaded and saved. Make sure PaperCut SMTP is running on localhost:25.";
        TestSuccess = true;
        HasSavedSettings = await _settingsManager.HasSavedSettingsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        await SaveToStorageAndSessionAsync();
        TestResult = "? Settings saved successfully! They will persist across restarts.";
        TestSuccess = true;
        HasSavedSettings = await _settingsManager.HasSavedSettingsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostTestConnectionAsync()
    {
        try
        {
            await _emailService.TestConnectionAsync(CurrentSettings);

            TestResult = $"? SUCCESS! Test email sent to {CurrentSettings.FromEmail}. Check your inbox.";
            TestSuccess = true;
            await SaveToStorageAndSessionAsync();
        }
        catch (Exception ex)
        {
            TestResult = $"? FAILED: {ex.Message}";
            TestSuccess = false;
            _logger.LogError(ex, "SMTP test failed");
        }

        HasSavedSettings = await _settingsManager.HasSavedSettingsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostClearSettingsAsync()
    {
        await _settingsManager.ClearSettingsAsync();
        HttpContext.Session.Remove("SmtpSettings");
        
        // Reload default (active)
        CurrentSettings = await _settingsManager.GetActiveSettingsAsync();
        
        TestResult = "?? Saved settings cleared. Reverted to defaults.";
        TestSuccess = false;
        HasSavedSettings = false;
        return Page();
    }

    private async Task SaveToStorageAndSessionAsync()
    {
        await _settingsManager.SaveSettingsAsync(CurrentSettings);
        HttpContext.Session.SetString("SmtpSettings", JsonSerializer.Serialize(CurrentSettings));
    }
}
