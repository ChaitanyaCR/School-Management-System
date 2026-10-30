using System.ComponentModel.DataAnnotations;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using SchoolMs.Infrastructure.Configuration;

namespace SchoolMs.Infrastructure.Email;

/// <summary>A rendered transactional email (NTF-01).</summary>
public sealed record EmailMessage(string ToAddress, string? ToName, string Subject, string HtmlBody, string TextBody);

/// <summary>Delivers email. Called from background jobs only, so slow or failing mail servers never block requests.</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

public enum SmtpSecurity
{
    /// <summary>Plain connection; only for a local catcher such as Mailpit.</summary>
    None,

    /// <summary>Upgrade with STARTTLS (usually port 587). The default.</summary>
    StartTls,

    /// <summary>TLS from the start (usually port 465).</summary>
    SslOnConnect,
}

/// <summary>Any SMTP provider works (NFR-OPS-06), e.g. Amazon SES, Brevo, Zoho or a self-hosted server.</summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    [Required]
    public string Host { get; init; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; init; } = 587;

    public SmtpSecurity Security { get; init; } = SmtpSecurity.StartTls;

    public string? Username { get; init; }

    public string? Password { get; init; }

    [Required]
    [EmailAddress]
    public string FromAddress { get; init; } = string.Empty;

    /// <summary>Defaults to the product name.</summary>
    public string? FromName { get; init; }
}

internal sealed class SmtpEmailSender(IOptions<SmtpOptions> smtpOptions, IOptions<AppOptions> appOptions) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var options = smtpOptions.Value;

        using var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(options.FromName ?? appOptions.Value.ProductName, options.FromAddress));
        mime.To.Add(new MailboxAddress(message.ToName ?? string.Empty, message.ToAddress));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { HtmlBody = message.HtmlBody, TextBody = message.TextBody }.ToMessageBody();

        using var client = new SmtpClient();
        var security = options.Security switch
        {
            SmtpSecurity.None => SecureSocketOptions.None,
            SmtpSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
            _ => SecureSocketOptions.StartTls,
        };

        await client.ConnectAsync(options.Host, options.Port, security, cancellationToken);
        if (!string.IsNullOrEmpty(options.Username))
        {
            await client.AuthenticateAsync(options.Username, options.Password ?? string.Empty, cancellationToken);
        }

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}
