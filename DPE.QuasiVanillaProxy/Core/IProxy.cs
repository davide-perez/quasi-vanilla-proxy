namespace DPE.QuasiVanillaProxy.Core
{
    public interface IProxy
    {
        public Uri? TargetUrl { get; set; }

        public Task StartAsync(CancellationToken cancellationToken);
        public Task StopAsync(CancellationToken cancellationToken);
        public Task<HttpResponseMessage?> ForwardAsync(byte[] payload, CancellationToken cancellationToken);
    }
}
