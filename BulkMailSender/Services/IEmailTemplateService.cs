using BulkMailSender.Models;

namespace BulkMailSender.Services;

public interface IEmailTemplateService
{
    string ReplacePlaceholders(string template, Dictionary<string, string> values);
    string BuildSubject(TemplateType type, string? period, string debtorCode, string organization);
    string BuildBody(TemplateType type, string? notes);
    Task SaveUserTemplateAsync(TemplateType type, string subject, string body);
    Task ResetUserTemplateAsync();
    string GetRawSubject(TemplateType type);
    string GetRawBody(TemplateType type);
}
