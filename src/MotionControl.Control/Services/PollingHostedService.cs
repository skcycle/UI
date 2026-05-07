using Microsoft.Extensions.Hosting;
using MotionControl.Control.Interfaces;
using MotionControl.Device.Zmc.Config;

namespace MotionControl.Control.Services;

public sealed class PollingHostedService(
    ControllerPollingService controllerPollingService,
    IUiRefreshNotifier uiRefreshNotifier,
    ZmcControllerOptions zmcOptions) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMilliseconds(Math.Max(30, zmcOptions.PollingIntervalMs <= 0 ? 200 : zmcOptions.PollingIntervalMs));
        var refreshInterval = TimeSpan.FromMilliseconds(Math.Max(50, zmcOptions.PollingIntervalMs));
        var lastRefreshUtc = DateTime.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            await controllerPollingService.PollOnceAsync(stoppingToken);

            var now = DateTime.UtcNow;
            if (now - lastRefreshUtc >= refreshInterval)
            {
                uiRefreshNotifier.RequestRefresh();
                lastRefreshUtc = now;
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
