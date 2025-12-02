using System.Text.Json;
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
    public TemplateType TemplateType { get; set; } = TemplateType.SoaInv;

    [BindProperty]
    public string Period { get; set; } = string.Empty;

    [BindProperty]
    public string DebtorCode { get; set; } = string.Empty;

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
    }

    public IActionResult OnPostPreview()
    {
        HasUploadData = !string.IsNullOrEmpty(HttpContext.Session.GetString("ExtractionPath"));
        LoadAvailableDebtorCodes();
        LoadSelectedDebtorInfo();

        var orgName = string.IsNullOrWhiteSpace(SelectedOrganizationName) ? "{organization name}" : SelectedOrganizationName;
        var notes = string.IsNullOrWhiteSpace(SelectedNotes) ? "{notes}" : SelectedNotes;

        SubjectPreview = BuildSubject(TemplateType, Period, DebtorCode, orgName);
        BodyPreview = BuildBody(TemplateType, notes);
        return Page();
    }

    public IActionResult OnPost()
    {
        HasUploadData = !string.IsNullOrEmpty(HttpContext.Session.GetString("ExtractionPath"));
        LoadAvailableDebtorCodes();
        LoadSelectedDebtorInfo();

        var template = new SavedTemplate
        {
            TemplateType = TemplateType,
            Period = Period?.Trim() ?? string.Empty,
            DebtorCode = "{debtor code}",
            OrganizationName = "{organization name}",
            Notes = "{notes}",
            Subject = BuildSubject(TemplateType, Period, "{debtor code}", "{organization name}"),
            Body = BuildBody(TemplateType, "{notes}")
        };
        HttpContext.Session.SetString("EmailTemplate", JsonSerializer.Serialize(template));
        
        // Show preview with placeholders
        SubjectPreview = template.Subject;
        BodyPreview = template.Body;
        _logger.LogInformation("Email template saved to session with placeholders.");
        return Page();
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

    private static string BuildSubject(TemplateType type, string? period, string debtorCode, string organization)
    {
        period = string.IsNullOrWhiteSpace(period) ? "<SET PERIOD>" : period.Trim();
        debtorCode = string.IsNullOrWhiteSpace(debtorCode) ? "{debtor code}" : debtorCode.Trim();
        organization = string.IsNullOrWhiteSpace(organization) ? "{organization name}" : organization.Trim();
        if (type == TemplateType.SoaInv)
        {
            return $"ATP INVOICE AND SOA {period} - {debtorCode} - {organization}";
        }
        else
        {
            return $"Reminder overdue account -{organization} - {debtorCode}";
        }
    }

    private static string BuildBody(TemplateType type, string? notes)
    {
        if (type == TemplateType.SoaInv)
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
