using BacklightLibrary;
using BacklightLibrary.Events;

namespace BacklightService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var args = Environment.GetCommandLineArgs();
        var targetBrightness = BacklightState.Full;
        if (args.Length == 1)
            switch (args[0])
            {
                case "0":
                    targetBrightness = BacklightState.Off;
                    break;
                case "1":
                    targetBrightness = BacklightState.Dim;
                    break;
                case "2":
                    targetBrightness = BacklightState.Full;
                    break;
                default:
                    _logger.LogCritical("Provided start parameter is incorrect. Accepted values are [0-2].");
                    break;
            }
        else _logger.LogCritical("Incorrect number of parameters.");

        var keeper = new BacklightKeeper(targetBrightness);
        keeper.OnException += OnKeeperException;
        keeper.Start();
        _logger.LogInformation("Reached main loop.");
        while (!stoppingToken.IsCancellationRequested)
            //_logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            await Task.Delay(5000, stoppingToken);
        _logger.LogInformation("Stopping the keeper.");
        keeper.Stop();
        _logger.LogInformation("Stopped. Goodbye!");
    }

    private void OnKeeperException(object sender, ExceptionEventArgs e)
    {
        _logger.LogError(e.Exception, "An exception occurred within BacklightKeeper library.");
    }
}