using BulkMailSender.Models;

namespace BulkMailSender.Services;

public interface IEmailTemplateService
{
    Task<EmailTemplateContent> GetTemplateAsync(TemplateType type);
    Task SaveCustomTemplateAsync(TemplateType type, string subject, string body);
    string RenderTemplate(string template, Dictionary<string, string> placeholders);
    Task ResetUserTemplateAsync();
}
