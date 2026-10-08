using SyWater.Notifications.Application.Ports.In;
using SyWater.Notifications.Application.Ports.Out;
using SyWater.Notifications.Domain.Emails;

namespace SyWater.Notifications.Application.UseCases;

/// <summary>
/// Sends the mails of the outbox whose time came. A failure never stops the others: it is recorded on its own
/// mail and retried later, until the attempts run out. Returns how many mails left.
/// </summary>
public sealed class SendDueEmailsUseCase(
    IEmailOutboxRepository outbox, IUserContactDirectory contacts, IEmailSender sender, EmailRetryPolicy policy, TimeProvider clock)
    : ISendDueEmailsUseCase
{
    public const int BatchSize = 20;

    public async Task<int> ExecuteAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var sent = 0;
        foreach (var item in await outbox.GetDueAsync(now, BatchSize, ct))
        {
            try
            {
                var contact = await contacts.FindAsync(item.UserId, ct);
                if (contact is null || string.IsNullOrWhiteSpace(contact.Email))
                {
                    item.MarkUndeliverable("The user has no e-mail address.");
                }
                else
                {
                    await sender.SendAsync(new EmailMessage(contact.Email, contact.FullName, item.Subject, item.TextBody, item.HtmlBody), ct);
                    item.MarkSent(now);
                    sent++;
                }
            }
            catch (Exception ex) when (ex is EmailSendException or ExternalServiceUnavailableException)
            {
                item.RegisterFailure(ex.Message, now, policy);
            }

            await outbox.SaveOutcomeAsync(item, ct);
        }
        return sent;
    }
}
