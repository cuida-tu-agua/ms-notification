using SyWater.Notifications.Application.Ports.In;

namespace SyWater.Notifications.Api.Background;

/// <summary>
/// HU-027: every 10 s, sends the mails of the outbox that are due. A mail that fails is retried later with a
/// growing delay (see EmailRetryPolicy); the worker itself never stops because of one bad mail.
/// </summary>
public sealed class EmailDeliveryWorker(IServiceScopeFactory scopes, ILogger<EmailDeliveryWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Every = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Every);
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var sent = await scope.ServiceProvider.GetRequiredService<ISendDueEmailsUseCase>().ExecuteAsync(stoppingToken);
                if (sent > 0) logger.LogInformation("{Count} e-mail(s) sent", sent);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "E-mail sweep failed; retrying in {Seconds}s", Every.TotalSeconds);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}
