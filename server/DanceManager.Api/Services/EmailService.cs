using System.Net;
using System.Net.Mail;

namespace DanceManager.Api.Services;

/// <summary>
/// Sends team-invite emails via Gmail SMTP (an App Password, not the account
/// password — see appsettings.json's Email block for setup). Uses the built-in
/// SmtpClient rather than adding a mail library dependency; fine at this volume
/// (Gmail caps free accounts around ~500 sends/day).
/// </summary>
public class EmailService
{
    private readonly string? _user;
    private readonly string? _appPassword;
    private readonly string _appUrl;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _user = config["Email:GmailUser"];
        _appPassword = config["Email:GmailAppPassword"];
        _appUrl = (config["Email:AppUrl"] ?? "http://localhost:5199").TrimEnd('/');
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_user) && !string.IsNullOrWhiteSpace(_appPassword);

    /// <summary>Sends the invite email; returns false (never throws) on any failure so a
    /// mail outage never blocks creating the invite itself.</summary>
    public async Task<bool> SendInviteEmailAsync(string toEmail, string tenantName, string code, CancellationToken ct = default)
    {
        if (!IsConfigured) return false;

        var subject = $"You’re invited to join {tenantName} on DanceManager";
        var body = $"""
            Hi there,

            You've been invited to join "{tenantName}" on DanceManager as a co-teacher.

            To join:
              1. Go to {_appUrl} and sign in (or create an account with this email).
              2. Open Account settings.
              3. Under "Join a team", enter this invite code:

                     {code}

            That's it — you'll see the same studios, classes, and shows as the rest of the team.

            — DanceManager
            """;

        using var message = new MailMessage
        {
            From = new MailAddress(_user!, "DanceManager"),
            Subject = subject,
            Body = body,
            IsBodyHtml = false,
        };
        message.To.Add(toEmail);

        using var client = new SmtpClient("smtp.gmail.com", 587)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(_user, _appPassword),
        };

        try
        {
            await client.SendMailAsync(message, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send invite email to {Email}", toEmail);
            return false;
        }
    }
}
