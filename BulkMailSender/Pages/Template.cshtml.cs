using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using BulkMailSender.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BulkMailSender.Pages;

public enum TemplateType
{
    SoaInv,
    Overdue
}

public class TemplateModel : PageModel
{
    private readonly ILogger<TemplateModel> _logger;

    public TemplateModel(ILogger<TemplateModel> logger)
    {
        _logger = logger;
    }

    public bool HasUploadData { get; set; }

    [BindProperty]
    public TemplateType? TemplateType { get; set; }

    [BindProperty]
    public string Period { get; set; } = string.Empty;

    [BindProperty]
    public string DebtorCode { get; set; } = string.Empty;

    // Preview-only fields (separate from saved template)
    public TemplateType? PreviewTemplateType { get; set; }
    public string? PreviewPeriod { get; set; }

    // Available debtor codes from recipients
    public List<DebtorCodeOption> AvailableDebtorCodes { get; set; } = new();

    // Selected debtor info for preview
    public string? SelectedOrganizationName { get; set; }
    public string? SelectedNotes { get; set; }

    public string? SubjectPreview { get; set; }
    public string? BodyPreview { get; set; }

    public void OnGet()
    {
        HasUploadData = !string.IsNullOrEmpty(HttpContext.Session.GetString("ExtractionPath"));
        LoadAvailableDebtorCodes();
        
        // Check if there's a saved template and pre-populate fields
        var templateJson = HttpContext.Session.GetString("EmailTemplate");
        if (!string.IsNullOrEmpty(templateJson))
        {
            try
            {
                var savedTemplate = JsonSerializer.Deserialize<SavedTemplate>(templateJson);
                if (savedTemplate != null)
                {
                    TemplateType = savedTemplate.TemplateType;
                    Period = savedTemplate.Period;
                    _logger.LogInformation("Loaded existing template from session. Type: {Type}, Period: {Period}", TemplateType, Period);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load saved template from session");
            }
        }
    }

    public IActionResult OnPostPreview()
    {
        // CRITICAL: Clear ModelState to prevent validation errors from [BindProperty] fields
        // that are not part of the preview form (TemplateType, Period)
        ModelState.Clear();
        
        HasUploadData = !string.IsNullOrEmpty(HttpContext.Session.GetString("ExtractionPath"));
        LoadAvailableDebtorCodes();
        
        // Load saved template to restore Email Settings fields (so they don't appear blank)
        var templateJson = HttpContext.Session.GetString("EmailTemplate");
        if (!string.IsNullOrEmpty(templateJson))
        {
            try
            {
                var savedTemplate = JsonSerializer.Deserialize<SavedTemplate>(templateJson);
                if (savedTemplate != null)
                {
                    TemplateType = savedTemplate.TemplateType;
                    Period = savedTemplate.Period;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load saved template");
            }
        }
        
        // Get preview values from form
        var previewTypeStr = Request.Form["PreviewTemplateType"].ToString();
        var previewPeriodStr = Request.Form["PreviewPeriod"].ToString();
        
        // Parse preview template type
        if (!string.IsNullOrEmpty(previewTypeStr) && Enum.TryParse<TemplateType>(previewTypeStr, out var parsedType))
        {
            PreviewTemplateType = parsedType;
        }
        
        PreviewPeriod = previewPeriodStr;
        
        LoadSelectedDebtorInfo();

        // Validate preview fields only (not the main Email Settings fields)
        if (!PreviewTemplateType.HasValue)
        {
            TempData["ErrorMessage"] = "Please select a Preview Email Type.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(PreviewPeriod))
        {
            TempData["ErrorMessage"] = "Please enter a Preview Period.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(DebtorCode))
        {
            TempData["ErrorMessage"] = "Please select a Debtor Code for preview.";
            return Page();
        }

        var orgName = string.IsNullOrWhiteSpace(SelectedOrganizationName) ? "{organization name}" : SelectedOrganizationName;
        var notes = string.IsNullOrWhiteSpace(SelectedNotes) ? "{notes}" : SelectedNotes;

        SubjectPreview = BuildSubject(PreviewTemplateType.Value, PreviewPeriod, DebtorCode, orgName);
        BodyPreview = BuildBody(PreviewTemplateType.Value, notes);
        
        _logger.LogInformation("Preview generated for debtor: {DebtorCode}, Type: {Type}, Period: {Period}", 
            DebtorCode, PreviewTemplateType, PreviewPeriod);
        return Page();
    }

    public IActionResult OnPost()
    {
        HasUploadData = !string.IsNullOrEmpty(HttpContext.Session.GetString("ExtractionPath"));
        LoadAvailableDebtorCodes();

        // If fields are empty, try to load from saved template first
        if (!TemplateType.HasValue || string.IsNullOrWhiteSpace(Period))
        {
            var templateJson = HttpContext.Session.GetString("EmailTemplate");
            if (!string.IsNullOrEmpty(templateJson))
            {
                try
                {
                    var savedTemplate = JsonSerializer.Deserialize<SavedTemplate>(templateJson);
                    if (savedTemplate != null)
                    {
                        if (!TemplateType.HasValue)
                            TemplateType = savedTemplate.TemplateType;
                        if (string.IsNullOrWhiteSpace(Period))
                            Period = savedTemplate.Period;
                        
                        _logger.LogInformation("Loaded template from session for Go to Review. Type: {Type}, Period: {Period}", TemplateType, Period);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load saved template for Go to Review");
                }
            }
        }

        // Validate Email Type and Period before going to Review
        if (!TemplateType.HasValue)
        {
            ModelState.AddModelError(nameof(TemplateType), "Email type is required. Please select SOA & Invoice or Overdue.");
            TempData["ErrorMessage"] = "Please complete Email Type and Period before proceeding to review.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Period))
        {
            ModelState.AddModelError(nameof(Period), "Period is required. Please enter a period (e.g., OCT 2025).");
            TempData["ErrorMessage"] = "Please complete Email Type and Period before proceeding to review.";
            return Page();
        }

        // Create and save template to session
        var template = new SavedTemplate
        {
            TemplateType = TemplateType.Value,
            Period = Period?.Trim() ?? string.Empty,
            DebtorCode = "{debtor code}",
            OrganizationName = "{organization name}",
            Notes = "{notes}",
            Subject = BuildSubject(TemplateType.Value, Period, "{debtor code}", "{organization name}"),
            Body = BuildBody(TemplateType.Value, "{notes}")
        };
        
        try
        {
            HttpContext.Session.SetString("EmailTemplate", JsonSerializer.Serialize(template));
            _logger.LogInformation("Email template saved and redirecting to Review page. Type: {Type}, Period: {Period}", TemplateType.Value, Period);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save template before redirecting to preview");
            TempData["ErrorMessage"] = "Failed to save template. Please try again.";
            return Page();
        }
        
        return RedirectToPage("/Preview");
    }

    public IActionResult OnPostSave()
    {
        HasUploadData = !string.IsNullOrEmpty(HttpContext.Session.GetString("ExtractionPath"));
        LoadAvailableDebtorCodes();

        if (!TemplateType.HasValue)
        {
            ModelState.AddModelError(nameof(TemplateType), "Email type is required. Please select SOA & Invoice or Overdue.");
            TempData["ErrorMessage"] = "Email type is required.";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Period))
        {
            ModelState.AddModelError(nameof(Period), "Period is required. Please enter a period (e.g., OCT 2025).");
            TempData["ErrorMessage"] = "Period is required.";
            return Page();
        }

        var template = new SavedTemplate
        {
            TemplateType = TemplateType.Value,
            Period = Period?.Trim() ?? string.Empty,
            DebtorCode = "{debtor code}",
            OrganizationName = "{organization name}",
            Notes = "{notes}",
            Subject = BuildSubject(TemplateType.Value, Period, "{debtor code}", "{organization name}"),
            Body = BuildBody(TemplateType.Value, "{notes}")
        };
        
        try
        {
            HttpContext.Session.SetString("EmailTemplate", JsonSerializer.Serialize(template));
            _logger.LogInformation("Email template saved to session. Type: {Type}, Period: {Period}", TemplateType.Value, Period);
            TempData["SuccessMessage"] = $"Email template saved successfully! Type: {TemplateType.Value}, Period: {Period}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save template to session");
            TempData["ErrorMessage"] = "Failed to save template. Please try again.";
            return Page();
        }
        
        // Redirect to same page to properly reload saved template
        return RedirectToPage("/Template");
    }

    private void LoadAvailableDebtorCodes()
    {
        var recipientsJson = HttpContext.Session.GetString("Recipients");
        if (string.IsNullOrEmpty(recipientsJson)) return;

        try
        {
            var recipients = JsonSerializer.Deserialize<List<DebtorRecipient>>(recipientsJson) ?? new List<DebtorRecipient>();
            AvailableDebtorCodes = recipients
                .GroupBy(r => r.DebtorCode)
                .Select(g => new DebtorCodeOption
                {
                    DebtorCode = g.Key,
                    OrganizationName = g.First().OrganizationName ?? string.Empty,
                    Notes = g.First().Notes ?? string.Empty,
                    EmailCount = g.Count()
                })
                .OrderBy(d => d.DebtorCode)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load available debtor codes");
        }
    }

    private void LoadSelectedDebtorInfo()
    {
        if (string.IsNullOrWhiteSpace(DebtorCode)) return;

        var selected = AvailableDebtorCodes.FirstOrDefault(d => d.DebtorCode.Equals(DebtorCode, StringComparison.OrdinalIgnoreCase));
        if (selected != null)
        {
            SelectedOrganizationName = selected.OrganizationName;
            SelectedNotes = selected.Notes;
        }
    }

    private static string BuildSubject(Pages.TemplateType type, string? period, string debtorCode, string organization)
    {
        period = string.IsNullOrWhiteSpace(period) ? "<SET PERIOD>" : period.Trim();
        debtorCode = string.IsNullOrWhiteSpace(debtorCode) ? "{debtor code}" : debtorCode.Trim();
        organization = string.IsNullOrWhiteSpace(organization) ? "{organization name}" : organization.Trim();
        if (type == Pages.TemplateType.SoaInv)
        {
            return $"ATP INVOICE AND SOA {period} - {debtorCode} - {organization}";
        }
        else
        {
            return $"Reminder overdue account -{organization} - {debtorCode}";
        }
    }

    private static string BuildBody(Pages.TemplateType type, string? notes)
    {
        if (type == Pages.TemplateType.SoaInv)
        {
            return "Good day to you\nThe attached statement reflects your account balance.\n\nPlease check the statement provided.\n\nIf you have any questions regarding this statement or any clarification needs, please contact the ATP Careline 018-7864855\n\nAny overdue payment may lead to service interruption.\n\nThe below are the bank details for your payment purpose:-\nCompany Name: ATP SALES & SERVICES SDN BHD\nBank Name: Affin Bank Bhd\nBank Account Number: 10675 0000 898\nEmail (Banking Slip): <atgroupoperation02@gmail.com>\n\n***************************************************************************\n\nThis is an auto-generated email, please DO NOT REPLY. Any replies to this\nemail will be disregarded.\n\n***************************************************************************";
        }
        else
        {
            var notesText = string.IsNullOrWhiteSpace(notes) ? "{notes}" : notes.Trim();
            // notesText should be the term e.g. 60 DAYS/90Days; we keep as provided placeholder
            return $"Good day to you\nKindly find the attached statement of account and invoice.\n\nAccording to our payment term with your company, your are requested to make the payment within {notesText} after you receive the monthly statement of account. Please clear and remit, if any. If you have made the payment, please let me know and I will update accordingly\n\nBearing in mind, as company policy\n\nATP shall be entitled, at its absolute discretion, to suspend Customer's account and hold service call until overdue outstanding has been fully paid.\n\nThank you\n\nPIC name: Ms. Ika\nEmail: atgroupoperation02@gmail.com\nDirect Line: 018-7864855";
        }
    }
}

public class SavedTemplate
{
    public TemplateType TemplateType { get; set; }
    public string Period { get; set; } = string.Empty;
    public string DebtorCode { get; set; } = string.Empty;
    public string OrganizationName { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

public class DebtorCodeOption
{
    public string DebtorCode { get; set; } = string.Empty;
    public string OrganizationName { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public int EmailCount { get; set; }
}
