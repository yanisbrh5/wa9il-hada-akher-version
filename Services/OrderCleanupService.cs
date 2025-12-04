using API.Data;
using API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace API.Services
{
    public class OrderCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OrderCleanupService> _logger;
        private readonly CleanupSettings _cleanupSettings;
        private Timer? _timer;

        public OrderCleanupService(
            IServiceProvider serviceProvider,
            ILogger<OrderCleanupService> logger,
            IOptions<CleanupSettings> cleanupSettings)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _cleanupSettings = cleanupSettings.Value;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_cleanupSettings.Enabled)
            {
                _logger.LogInformation("🔕 Order cleanup service is disabled");
                return Task.CompletedTask;
            }

            _logger.LogInformation($"🧹 Order cleanup service started. Running every {_cleanupSettings.IntervalHours} hours");
            _logger.LogInformation($"📅 Retention period: {_cleanupSettings.RetentionDays} days");

            var interval = TimeSpan.FromHours(_cleanupSettings.IntervalHours);
            _timer = new Timer(DoCleanup, null, TimeSpan.Zero, interval);

            return Task.CompletedTask;
        }

        private async void DoCleanup(object? state)
        {
            try
            {
                _logger.LogInformation("🧹 Starting order cleanup...");

                using var scope = _serviceProvider.CreateScope();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                
                var cutoffDate = DateTime.UtcNow.AddDays(-_cleanupSettings.RetentionDays);
                var totalDeleted = 0;

                // Clean from Database 1
                var context1 = scope.ServiceProvider.GetRequiredService<StoreContext>();
                var deletedFromDb1 = await CleanupFromContext(context1, cutoffDate);
                totalDeleted += deletedFromDb1;

                // Clean from Database 2 if available
                try
                {
                    var context2 = scope.ServiceProvider.GetRequiredService<StoreContext2>();
                    var deletedFromDb2 = await CleanupFromContext(context2, cutoffDate);
                    totalDeleted += deletedFromDb2;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Database2 cleanup skipped: {ex.Message}");
                }

                _logger.LogInformation($"✅ Cleanup completed. Deleted {totalDeleted} orders older than {_cleanupSettings.RetentionDays} days");

                // Send Telegram notification
                if (_cleanupSettings.NotifyOnCleanup && totalDeleted > 0)
                {
                    var message = $"🧹 تنظيف تلقائي للطلبات\n\n" +
                                $"✅ تم حذف {totalDeleted} طلب\n" +
                                $"📅 الطلبات الأقدم من {_cleanupSettings.RetentionDays} أيام\n" +
                                $"⏰ {DateTime.Now:yyyy-MM-dd HH:mm}";

                    await notificationService.SendMessageAsync(message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error during cleanup: {ex.Message}");
            }
        }

        private async Task<int> CleanupFromContext(DbContext context, DateTime cutoffDate)
        {
            try
            {
                var ordersProperty = context.GetType().GetProperty("Orders");
                if (ordersProperty == null)
                    return 0;

                var ordersSet = ordersProperty.GetValue(context) as DbSet<API.Modeles.Order>;
                if (ordersSet == null)
                    return 0;

                var oldOrders = await ordersSet
                    .Where(o => o.OrderDate < cutoffDate)
                    .Include(o => o.Items)
                    .ToListAsync();

                if (oldOrders.Any())
                {
                    ordersSet.RemoveRange(oldOrders);
                    await context.SaveChangesAsync();
                    
                    _logger.LogInformation($"🗑️ Deleted {oldOrders.Count} orders from {context.GetType().Name}");
                }

                return oldOrders.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error cleaning {context.GetType().Name}: {ex.Message}");
                return 0;
            }
        }

        public override void Dispose()
        {
            _timer?.Dispose();
            base.Dispose();
        }
    }
}
