using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using SyWater.Notifications.Application.Ports.Out;

namespace SyWater.Notifications.Infrastructure.Email;

/// <summary>Section "Smtp" of appsettings. The password goes in user-secrets, never in git.</summary>
public sealed class SmtpOptions
{
    public const string Section = "Smtp";

    /// <summary>Empty = the e-mail channel is off (nothing is queued).</summary>
    public string Host { get; init; } = "";
    public int Port { get; init; } = 587;

    /// <summary>None (Mailpit in development) · StartTls (port 587) · Ssl (port 465).</summary>
    public string Security { get; init; } = "StartTls";
    public string Username { get; init; } = "";
    public string Password { get; init; } = "";
    public string FromAddress { get; init; } = "no-reply@cuidatuagua.local";
    public string FromName { get; init; } = "Cuida Tu Agua";
    public int TimeoutSeconds { get; init; } = 15;
}

/// <summary>Real SMTP with MailKit: Mailpit in development (localhost:1025, no login), the real server in production.</summary>
public sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    private readonly SmtpOptions _options = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        mime.To.Add(new MailboxAddress(message.ToName ?? message.ToAddress, message.ToAddress));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { TextBody = message.TextBody, HtmlBody = message.HtmlBody }.ToMessageBody();

        try
        {
            using var client = new SmtpClient { Timeout = _options.TimeoutSeconds * 1000 };
            await client.ConnectAsync(_options.Host, _options.Port, SecurityFor(_options.Security), ct);
            if (!string.IsNullOrWhiteSpace(_options.Username))
                await client.AuthenticateAsync(_options.Username, _options.Password, ct);
            await client.SendAsync(mime, ct);
            await client.DisconnectAsync(quit: true, ct);
        }
        catch (Exception ex) when (ex is ProtocolException or CommandException or AuthenticationException
                                       or IOException or System.Net.Sockets.SocketException or TimeoutException)
        {
            throw new EmailSendException($"SMTP {_options.Host}:{_options.Port} failed: {ex.Message}", ex);
        }
    }

    private static SecureSocketOptions SecurityFor(string value) => value.Trim().ToLowerInvariant() switch
    {
        "none" => SecureSocketOptions.None,
        "ssl" => SecureSocketOptions.SslOnConnect,
        _ => SecureSocketOptions.StartTls,
    };
}
