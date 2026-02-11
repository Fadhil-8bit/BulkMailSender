using System.Text.Json;
using System.Collections.Concurrent;
using BulkMailSender.Models;

namespace BulkMailSender.Services;

public class EmailTemplateService : IEmailTemplateService
{
    private readonly ConcurrentDictionary<string, string> _templates;
    private readonly string _templatesFilePath;
    private readonly ILogger<EmailTemplateService> _logger;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public EmailTemplateService(IWebHostEnvironment env, ILogger<EmailTemplateService> logger)
    {
        _logger = logger;
        var appDataPath = Path.Combine(env.ContentRootPath, "App_Data");
        var templatesDir = Path.Combine(appDataPath, "Templates");
        Directory.CreateDirectory(templatesDir);
        _templatesFilePath = Path.Combine(templatesDir, "user_templates.json");

        _templates = new ConcurrentDictionary<string, string>();
        
        // Default templates
        _templates["SoaInv_Subject"] = "ATP INVOICE AND SOA {period} - {debtor_code} - {organization_name}";
        _templates["SoaInv_Body"] = "Good day to you\nThe attached statement reflects your account balance.\n\nPlease check the statement provided.\n\nIf you have any questions regarding this statement or any clarification needs, please contact the ATP Careline 018-7864855\n\nAny overdue payment may lead to service interruption.\n\nThe below are the bank details for your payment purpose:-\nCompany Name: ATP SALES & SERVICES SDN BHD\nBank Name: Affin Bank Bhd\nBank Account Number: 10675 0000 898\nEmail (Banking Slip): <atgroupoperation02@gmail.com>\n\n***************************************************************************\n\nThis is an auto-generated email, please DO NOT REPLY. Any replies to this\nemail will be disregarded.\n\n***************************************************************************";
        _templates["Overdue_Subject"] = "Reminder overdue account -{organization_name} - {debtor_code}";
        _templates["Overdue_Body"] = "Good day to you\nKindly find the attached statement of account and invoice.\n\nAccording to our payment term with your company, your are requested to make the payment within {notes} after you receive the monthly statement of account. Please clear and remit, if any. If you have made the payment, please let me know and I will update accordingly\n\nBearing in mind, as company policy\n\nATP shall be entitled, at its absolute discretion, to suspend Customer's account and hold service call until overdue outstanding has been fully paid.\n\nThank you\n\nPIC name: Ms. Ika\nEmail: atgroupoperation02@gmail.com\nDirect Line: 018-7864855";

        LoadUserTemplates();
    }
    
    private void LoadUserTemplates()
    {
        if (!File.Exists(_templatesFilePath)) return;

        try
        {
            var json = File.ReadAllText(_templatesFilePath);
            var userTemplates = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            
            if (userTemplates != null)
            {
                foreach (var kvp in userTemplates)
                {
                    _templates[kvp.Key] = kvp.Value;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load user templates");
        }
    }

    public async Task SaveCustomTemplateAsync(TemplateType type, string subject, string body)
    {
        string subjectKey = type == TemplateType.SoaInv ? "SoaInv_Subject" : "Overdue_Subject";
        string bodyKey = type == TemplateType.SoaInv ? "SoaInv_Body" : "Overdue_Body";

        // Update in-memory (always overwrites)
        _templates[subjectKey] = subject;
        _templates[bodyKey] = body;

        await SaveTemplatesToFileAsync();
    }

    public async Task ResetUserTemplateAsync()
    {
        // Re-set default templates in memory
        _templates["SoaInv_Subject"] = "ATP INVOICE AND SOA {period} - {debtor_code} - {organization_name}";
        _templates["SoaInv_Body"] = "Good day to you\nThe attached statement reflects your account balance.\n\nPlease check the statement provided.\n\nIf you have any questions regarding this statement or any clarification needs, please contact the ATP Careline 018-7864855\n\nAny overdue payment may lead to service interruption.\n\nThe below are the bank details for your payment purpose:-\nCompany Name: ATP SALES & SERVICES SDN BHD\nBank Name: Affin Bank Bhd\nBank Account Number: 10675 0000 898\nEmail (Banking Slip): <atgroupoperation02@gmail.com>\n\n***************************************************************************\n\nThis is an auto-generated email, please DO NOT REPLY. Any replies to this\nemail will be disregarded.\n\n***************************************************************************";
        _templates["Overdue_Subject"] = "Reminder overdue account -{organization_name} - {debtor_code}";
        _templates["Overdue_Body"] = "Good day to you\nKindly find the attached statement of account and invoice.\n\nAccording to our payment term with your company, your are requested to make the payment within {notes} after you receive the monthly statement of account. Please clear and remit, if any. If you have made the payment, please let me know and I will update accordingly\n\nBearing in mind, as company policy\n\nATP shall be entitled, at its absolute discretion, to suspend Customer's account and hold service call until overdue outstanding has been fully paid.\n\nThank you\n\nPIC name: Ms. Ika\nEmail: atgroupoperation02@gmail.com\nDirect Line: 018-7864855";

        // Delete the user templates file
        await _fileLock.WaitAsync();
        try
        {
            if (File.Exists(_templatesFilePath))
            {
                File.Delete(_templatesFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete user templates file");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private async Task SaveTemplatesToFileAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            // We only want to save what differs from default? Or just save all?
            // User requested to "delete custom JSON file" on reset, implying we only save modifications or we save the whole state.
            // Simplest is to save the current state of _templates to the file.
            // But wait, if I save defaults to the file, then loading it back will load defaults.
            // The requirement says "reverts to system defaults" and "deletes the custom JSON file".
            // So if I modify something, I save it.
            
            var json = JsonSerializer.Serialize(_templates, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_templatesFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save user templates");
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<EmailTemplateContent> GetTemplateAsync(TemplateType type)
    {
        // Simple synchronous fetch from memory
        string subjectKey = type == TemplateType.SoaInv ? "SoaInv_Subject" : "Overdue_Subject";
        string bodyKey = type == TemplateType.SoaInv ? "SoaInv_Body" : "Overdue_Body";

        var subject = _templates.TryGetValue(subjectKey, out var s) ? s : string.Empty;
        var body = _templates.TryGetValue(bodyKey, out var b) ? b : string.Empty;

        return await Task.FromResult(new EmailTemplateContent { Subject = subject, Body = body });
    }

    public string RenderTemplate(string template, Dictionary<string, string> placeholders)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;
        
        var result = template;
        foreach (var kvp in placeholders)
        {
            result = result.Replace($"{{{kvp.Key}}}", kvp.Value);
        }
        return result;
    }


}
