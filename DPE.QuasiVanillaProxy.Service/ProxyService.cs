using DPE.QuasiVanillaProxy.Core;

namespace DPE.QuasiVanillaProxy.Service
{
    public class ProxyService : BackgroundService
    {
        private readonly ILogger<IProxy> _logger;
        private IProxy? _gateway;

        public ProxyService(ILogger<IProxy> logger, IProxy gateway)
        {
            _logger = logger;
            _gateway = gateway;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.Register(async () =>
            {
                _logger.LogDebug("Cancellation requested");
                if (_gateway != null)
                    await _gateway.StopAsync(stoppingToken);
            });

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogDebug("Starting background process");
                await _gateway.StartAsync(stoppingToken);
            }
        }
    }
}
