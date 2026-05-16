using System.Net;
using System.Net.Mail;
using System.Text;

namespace ZombieKid;

public sealed class EmailNotifier
{
    private readonly EmailSettings _settings;

    public EmailNotifier(EmailSettings settings)
    {
        _settings = settings;
    }

    public bool IsConfigured => _settings.Enabled
        && _settings.Recipients.Count > 0
        && !string.IsNullOrWhiteSpace(_settings.SmtpHost)
        && !string.IsNullOrWhiteSpace(_settings.From);

    public async Task SendSummaryAsync(DailyActivity activity, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return;
        }

        await SendAsync($"ZombieKid activity summary for {activity.Date}", BuildSummary(activity), cancellationToken);
    }

    public async Task SendThresholdAsync(DailyActivity activity, IReadOnlyCollection<string> closedProcesses, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return;
        }

        var body = BuildSummary(activity) + Environment.NewLine + Environment.NewLine + "Closed processes: " + string.Join(", ", closedProcesses);
        await SendAsync($"ZombieKid limit reached for {activity.Date}", body, cancellationToken);
    }

    private async Task SendAsync(string subject, string body, CancellationToken cancellationToken)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(_settings.From),
            Subject = subject,
            Body = body
        };

        foreach (var recipient in _settings.Recipients.Where(static r => !string.IsNullOrWhiteSpace(r)))
        {
            message.To.Add(recipient.Trim());
        }

        using var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
        {
            EnableSsl = _settings.EnableSsl
        };

        if (!string.IsNullOrWhiteSpace(_settings.Username))
        {
            client.Credentials = new NetworkCredential(_settings.Username, _settings.Password);
        }

        await client.SendMailAsync(message, cancellationToken);
    }

    private static string BuildSummary(DailyActivity activity)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Date: {activity.Date}");
        builder.AppendLine($"Total: {activity.TotalTime}");
        builder.AppendLine($"Limit: {activity.Limit}");
        builder.AppendLine($"Remaining: {activity.Remaining}");
        builder.AppendLine();
        builder.AppendLine("Per-process:");

        foreach (var item in activity.Processes.OrderBy(static p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"- {item.Key}: {item.Value.Time}");
        }

        return builder.ToString();
    }
}
