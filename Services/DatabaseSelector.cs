using API.Data;
using API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace API.Services
{
    public class DatabaseSelector : IDatabaseSelector
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly INotificationService _notificationService;
        private readonly DatabaseSettings _databaseSettings;
        private readonly ILogger<DatabaseSelector> _logger;
        private static readonly SemaphoreSlim _rotationLock = new SemaphoreSlim(1, 1);

        public DatabaseSelector(
            IServiceProvider serviceProvider,
            IConfiguration configuration,
            INotificationService notificationService,
            IOptions<DatabaseSettings> databaseSettings,
            ILogger<DatabaseSelector> logger)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _notificationService = notificationService;
            _databaseSettings = databaseSettings.Value;
            _logger = logger;
        }

        public StoreContext GetCurrentContext()
        {
            var scope = _serviceProvider.CreateScope();
            // Always return StoreContext (Database1) for now
            // We'll use both contexts in GetAllContexts for searching
            return scope.ServiceProvider.GetRequiredService<StoreContext>();
        }

        public IEnumerable<StoreContext> GetAllContexts()
        {
            var scope = _serviceProvider.CreateScope();
            var contexts = new List<StoreContext>();
            
            // Add primary database context
            contexts.Add(scope.ServiceProvider.GetRequiredService<StoreContext>());
            
            return contexts;
        }

        public async Task CheckAndRotateIfNeededAsync()
        {
            if (_databaseSettings.RotationStrategy != "SizeBased")
                return;

            var currentContext = GetCurrentContext();
            var databaseSizeMB = await GetDatabaseSizeInMBAsync(currentContext);

            _logger.LogInformation($"Current database size: {databaseSizeMB}MB (Max: {_databaseSettings.MaxDatabaseSizeMB}MB)");

            if (databaseSizeMB >= _databaseSettings.MaxDatabaseSizeMB)
            {
                await RotateToNextDatabaseAsync();
            }
        }

        public async Task RotateToNextDatabaseAsync()
        {
            await _rotationLock.WaitAsync();
            try
            {
                var currentDb = _databaseSettings.CurrentDatabase;
                var nextDb = currentDb == "Database1" ? "Database2" : "Database1";

                _logger.LogWarning($"🔄 Rotating from {currentDb} to {nextDb}");

                // Update configuration
                _databaseSettings.CurrentDatabase = nextDb;
                _configuration[$"DatabaseSettings:CurrentDatabase"] = nextDb;

                // Send Telegram notification
                if (_databaseSettings.NotifyOnRotation)
                {
                    var message = $"🔄 تنبيه: تبديل قاعدة البيانات\n\n" +
                                $"راك تحط في قاعدة البيانات {(nextDb == "Database2" ? "2" : "1")}\n" +
                                $"السبب: امتلأت قاعدة البيانات {(currentDb == "Database1" ? "1" : "2")}";
                    
                    await _notificationService.SendMessageAsync(message);
                }

                _logger.LogInformation($"✅ Successfully rotated to {nextDb}");
            }
            finally
            {
                _rotationLock.Release();
            }
        }

        public string GetCurrentDatabaseName()
        {
            return _databaseSettings.CurrentDatabase;
        }

        private async Task<long> GetDatabaseSizeInMBAsync(StoreContext context)
        {
            try
            {
                // PostgreSQL query to get database size
                var connection = context.Database.GetDbConnection();
                await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = "SELECT pg_database_size(current_database())";
                
                var sizeInBytes = (long)(await command.ExecuteScalarAsync() ?? 0L);
                var sizeInMB = sizeInBytes / (1024 * 1024);

                await connection.CloseAsync();
                
                return sizeInMB;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting database size: {ex.Message}");
                return 0;
            }
        }
    }
}
