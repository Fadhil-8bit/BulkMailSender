using System.Text.Json;
using BulkMailSender.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Mail;
using System.Net;

namespace BulkMailSender.Pages;

public class SettingsModel : PageModel
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SettingsModel> _logger;

    public SettingsModel(IConfiguration configuration, ILogger<SettingsModel> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    [BindProperty]
    public SmtpSettings CurrentSettings { get; set; } = new();

    public string? TestResult { get; set; }
    public bool TestSuccess { get; set; }

    public void OnGet()
    {
        // Try to load from session first
        var settingsJson = HttpContext.Session.GetString("SmtpSettings");
        if (!string.IsNullOrEmpty(settingsJson))
        {
            try
            {
                CurrentSettings = JsonSerializer.Deserialize<SmtpSettings>(settingsJson) ?? new SmtpSettings();
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load SMTP settings from session");
            }
        }

        // Load default from appsettings.json
        LoadDefaultSettings();
    }

    public IActionResult OnPostUseDefault()
    {
        ModelState.Clear(); // Clear ModelState so form shows new values
        LoadDefaultSettings();
        SaveToSession();
        TestResult = "Default Gmail settings loaded. Click 'Test Connection' to verify.";
        TestSuccess = false;
        return Page();
    }

    public IActionResult OnPostUsePaperCut()
    {
        ModelState.Clear(); // Clear ModelState so form shows new values
        LoadPaperCutSettings();
        SaveToSession();
        TestResult = "PaperCut (debugging) settings loaded. Make sure PaperCut SMTP is running on localhost:25.";
        TestSuccess = false;
        return Page();
    }

    public IActionResult OnPostSave()
    {
        SaveToSession();
        TestResult = "Settings saved to session.";
        TestSuccess = true;
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
            SaveToSession();
            _logger.LogInformation("SMTP test successful: {Host}:{Port}", CurrentSettings.Host, CurrentSettings.Port);
        }
        catch (Exception ex)
        {
            TestResult = $"? FAILED: {ex.Message}";
            TestSuccess = false;
            _logger.LogError(ex, "SMTP test failed");
        }

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

    private void SaveToSession()
    {
        HttpContext.Session.SetString("SmtpSettings", JsonSerializer.Serialize(CurrentSettings));
    }
}
