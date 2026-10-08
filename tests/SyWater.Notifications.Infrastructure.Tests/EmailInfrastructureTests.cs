using System.Net;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SyWater.Notifications.Application.Ports.Out;
using SyWater.Notifications.Domain.Emails;
using SyWater.Notifications.Infrastructure.Http;
using SyWater.Notifications.Infrastructure.Persistence;

namespace SyWater.Notifications.Infrastructure.Tests;

public sealed class EfEmailOutboxRepositoryTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
    private readonly SqliteConnection _sqlite = new("DataSource=:memory:");
    private readonly DbContextOptions<NotificationDbContext> _options;
    private readonly Guid _user = Guid.NewGuid();

    public EfEmailOutboxRepositoryTests()
    {
        _sqlite.Open();
        _options = new DbContextOptionsBuilder<NotificationDbContext>().UseSqlite(_sqlite).Options;
        using var db = new NotificationDbContext(_options);
        db.Database.EnsureCreated();
    }

    private EfEmailOutboxRepository Repo() => new(new NotificationDbContext(_options));

    [Fact]
    public async Task The_same_source_event_queues_one_mail_per_user()
    {
        Assert.True(await Repo().AddAsync(EmailOutboxItem.Queue(_user, "evt-1", "S", "t", "h", Now), default));
        Assert.False(await Repo().AddAsync(EmailOutboxItem.Queue(_user, "evt-1", "S", "t", "h", Now), default));
        Assert.True(await Repo().AddAsync(EmailOutboxItem.Queue(Guid.NewGuid(), "evt-1", "S", "t", "h", Now), default));
    }

    [Fact]
    public async Task Only_pending_due_mails_are_returned_and_an_outcome_is_saved_once()
    {
        await Repo().AddAsync(EmailOutboxItem.Queue(_user, "evt-1", "S", "t", "h", Now), default);
        await Repo().AddAsync(EmailOutboxItem.Queue(_user, "evt-2", "S", "t", "h", Now.AddMinutes(10)), default);   // not due yet

        var due = Assert.Single(await Repo().GetDueAsync(Now.AddMinutes(1), 10, default));
        Assert.Equal(DateTimeKind.Utc, due.NextAttemptAt.Kind);

        due.MarkSent(Now.AddMinutes(1));
        Assert.True(await Repo().SaveOutcomeAsync(due, default));
        Assert.False(await Repo().SaveOutcomeAsync(due, default));   // already finished: a second worker changes nothing
        Assert.Empty(await Repo().GetDueAsync(Now.AddMinutes(1), 10, default));
    }

    [Fact]
    public async Task A_failure_keeps_the_mail_pending_with_the_next_attempt()
    {
        await Repo().AddAsync(EmailOutboxItem.Queue(_user, "evt-1", "S", "t", "h", Now), default);
        var item = Assert.Single(await Repo().GetDueAsync(Now, 10, default));

        item.RegisterFailure("SMTP down", Now, EmailRetryPolicy.Default);
        await Repo().SaveOutcomeAsync(item, default);

        Assert.Empty(await Repo().GetDueAsync(Now, 10, default));
        var later = Assert.Single(await Repo().GetDueAsync(Now.AddMinutes(2), 10, default));
        Assert.Equal(1, later.Attempts);
        Assert.Equal("SMTP down", later.LastError);
    }

    public void Dispose() => _sqlite.Dispose();
}

public class HttpUserContactDirectoryTests
{
    private sealed class Stub(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public HttpRequestMessage? Last { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Last = request;
            return Task.FromResult(answer(request));
        }
    }

    private static HttpUserContactDirectory Directory(Stub stub) =>
        new(new HttpClient(stub) { BaseAddress = new Uri("http://iam/") }, "internal-key-0123456789abcd");

    private static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    [Fact]
    public async Task It_asks_ms_iam_with_the_internal_key_and_reads_the_contact()
    {
        var id = Guid.NewGuid();
        var stub = new Stub(_ => Json(HttpStatusCode.OK, $$"""{"id":"{{id}}","email":"juan@example.com","fullName":"Juan Ome"}"""));

        var contact = await Directory(stub).FindAsync(id, default);

        Assert.Equal("juan@example.com", contact!.Email);
        Assert.Equal("Juan Ome", contact.FullName);
        Assert.Equal($"/internal/users/{id}/contact", stub.Last!.RequestUri!.AbsolutePath);
        Assert.Equal("internal-key-0123456789abcd", stub.Last.Headers.GetValues("X-Internal-Key").Single());
    }

    [Fact]
    public async Task A_user_that_does_not_exist_or_has_no_email_is_null_and_ms_iam_errors_are_unavailable()
    {
        Assert.Null(await Directory(new Stub(_ => new HttpResponseMessage(HttpStatusCode.NotFound))).FindAsync(Guid.NewGuid(), default));
        Assert.Null(await Directory(new Stub(_ => Json(HttpStatusCode.OK, """{"email":""}"""))).FindAsync(Guid.NewGuid(), default));
        await Assert.ThrowsAsync<ExternalServiceUnavailableException>(() =>
            Directory(new Stub(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError))).FindAsync(Guid.NewGuid(), default));
        await Assert.ThrowsAsync<ExternalServiceUnavailableException>(() =>
            Directory(new Stub(_ => new HttpResponseMessage(HttpStatusCode.Forbidden))).FindAsync(Guid.NewGuid(), default));
    }
}
