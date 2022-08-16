using BacklightService;
using Microsoft.Extensions.Logging.EventLog;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(options => { options.AddFilter<EventLogLoggerProvider>(level => level >= LogLevel.Information); })
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
        services.Configure<EventLogSettings>(config =>
        {
            config.LogName = "BacklightService";
            config.SourceName = "BacklightServiceSource";
        });
    })
    .UseWindowsService()
    .Build();

await host.RunAsync();