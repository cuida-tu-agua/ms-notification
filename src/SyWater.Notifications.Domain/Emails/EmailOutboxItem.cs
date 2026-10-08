namespace SyWater.Notifications.Domain.Emails;

public enum EmailStatus { Pending, Sent, Failed }

/// <summary>How hard we try to send a mail before giving up.</summary>
public sealed record EmailRetryPolicy(int MaxAttempts, IReadOnlyList<TimeSpan> Delays)
{
    /// <summary>5 attempts: after a failure wait 1 min, 5 min, 15 min, 1 h; the 5th failure is final.</summary>
    public static EmailRetryPolicy Default { get; } = new(5,
        [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromHours(1)]);

    public TimeSpan DelayAfter(int attempts) => Delays[Math.Clamp(attempts - 1, 0, Delays.Count - 1)];
}

/// <summary>
/// HU-027: one e-mail waiting to leave. It is written in the same flow that creates the notification, so a
/// mail server that is down never loses a critical alert: the worker retries until it leaves or the attempts
/// run out. The address is not stored here; it is asked to ms-iam at the moment of sending.
/// </summary>
public sealed class EmailOutboxItem
{
    public const int MaxErrorLength = 500;

    public Guid Id { get; }
    public Guid UserId { get; }
    public string? SourceEventId { get; }
    public string Subject { get; }
    public string TextBody { get; }
    public string HtmlBody { get; }
    public EmailStatus Status { get; private set; }
    public int Attempts { get; private set; }
    public DateTime NextAttemptAt { get; private set; }
    public string? LastError { get; private set; }
    public DateTime CreatedAt { get; }
    public DateTime? SentAt { get; private set; }

    private EmailOutboxItem(Guid id, Guid userId, string? sourceEventId, string subject, string textBody, string htmlBody,
        EmailStatus status, int attempts, DateTime nextAttemptAt, string? lastError, DateTime createdAt, DateTime? sentAt)
    {
        Id = id;
        UserId = userId;
        SourceEventId = sourceEventId;
        Subject = subject;
        TextBody = textBody;
        HtmlBody = htmlBody;
        Status = status;
        Attempts = attempts;
        NextAttemptAt = nextAttemptAt;
        LastError = lastError;
        CreatedAt = createdAt;
        SentAt = sentAt;
    }

    public static EmailOutboxItem Queue(Guid userId, string? sourceEventId, string subject, string textBody, string htmlBody, DateTime now) =>
        new(Guid.NewGuid(), userId, string.IsNullOrWhiteSpace(sourceEventId) ? null : sourceEventId.Trim(),
            subject, textBody, htmlBody, EmailStatus.Pending, 0, now, null, now, null);

    public static EmailOutboxItem Restore(Guid id, Guid userId, string? sourceEventId, string subject, string textBody, string htmlBody,
        EmailStatus status, int attempts, DateTime nextAttemptAt, string? lastError, DateTime createdAt, DateTime? sentAt) =>
        new(id, userId, sourceEventId, subject, textBody, htmlBody, status, attempts, nextAttemptAt, lastError, createdAt, sentAt);

    public bool IsDue(DateTime now) => Status == EmailStatus.Pending && now >= NextAttemptAt;

    public void MarkSent(DateTime now)
    {
        if (Status != EmailStatus.Pending) return;
        Status = EmailStatus.Sent;
        SentAt = now;
        Attempts++;
        LastError = null;
    }

    /// <summary>The mail server (or ms-iam) failed: try again later, or give up after the last attempt.</summary>
    public void RegisterFailure(string error, DateTime now, EmailRetryPolicy policy)
    {
        if (Status != EmailStatus.Pending) return;
        Attempts++;
        LastError = Trim(error);
        if (Attempts >= policy.MaxAttempts) Status = EmailStatus.Failed;
        else NextAttemptAt = now + policy.DelayAfter(Attempts);
    }

    /// <summary>Retrying will not help (the user has no address, was deleted…): final.</summary>
    public void MarkUndeliverable(string reason)
    {
        if (Status != EmailStatus.Pending) return;
        Status = EmailStatus.Failed;
        LastError = Trim(reason);
    }

    private static string Trim(string s) => s.Length <= MaxErrorLength ? s : s[..MaxErrorLength];
}
