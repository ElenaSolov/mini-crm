namespace MiniCrm.Api.Services;

// Background loop: every 5 seconds, send the events that are due.
public class TrackerWorker(IServiceScopeFactory scopeFactory, ILogger<TrackerWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        do
        {
            try
            {
                // A new scope each time, so each run gets a fresh DbContext.
                using var scope = scopeFactory.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<TrackerEventSender>();
                await sender.SendDueEvents(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // Keep the worker alive (e.g. the database is down for a moment); try again next time.
                logger.LogError(ex, "Sending tracker events failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
