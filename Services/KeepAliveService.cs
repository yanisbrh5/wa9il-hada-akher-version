namespace API.Services
{
    public class KeepAliveService : BackgroundService
    {
        private readonly ILogger<KeepAliveService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        // Use the public URL of the API
        private const string PingUrl = "https://yarbi-hada-sah-akher-version.onrender.com/api/products";

        public KeepAliveService(ILogger<KeepAliveService> logger, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("KeepAlive Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Pinging server to keep it alive...");
                    
                    using var client = _httpClientFactory.CreateClient();
                    // Set a short timeout so we don't hang if it's slow
                    client.Timeout = TimeSpan.FromSeconds(10);
                    
                    var response = await client.GetAsync(PingUrl, stoppingToken);
                    
                    _logger.LogInformation($"Ping response: {response.StatusCode}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Ping failed: {ex.Message}");
                }

                // Wait for 1 minute before next ping
                // Render free tier sleeps after 15 mins of inactivity, so 1-5 mins is safe.
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}
