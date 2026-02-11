using System.Text.Json.Serialization;

namespace BulkMailSender.Models;

public class EmailPresets
{
    public EmailSettings Debug { get; set; } = new();
    public EmailSettings ProductionDefault { get; set; } = new();
}
