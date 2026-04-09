namespace OmnilensScraping.Tracking;

public sealed class LocalLogEmailSender : IEmailSender
{
    private readonly ILogger<LocalLogEmailSender> _logger;

    public LocalLogEmailSender(ILogger<LocalLogEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken)
    {
        _logger.LogInformation("EMAIL -> {ToEmail} | {Subject}\n{Body}", toEmail, subject, body);
        return Task.CompletedTask;
    }
}

