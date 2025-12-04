using API.Data;
using API.Modeles;
using API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly IDatabaseSelector _databaseSelector;
        private readonly INotificationService _notificationService;
        private readonly StoreContext _primaryContext;

        public OrdersController(
            IDatabaseSelector databaseSelector,
            INotificationService notificationService,
            StoreContext primaryContext)
        {
            _databaseSelector = databaseSelector;
            _notificationService = notificationService;
            _primaryContext = primaryContext;
        }

        [HttpPost]
        public async Task<ActionResult<Order>> PostOrder(Order order)
        {
            // Calculate totals (server-side validation) - use primary context for reference data
            decimal total = 0;
            foreach (var item in order.Items)
            {
                var product = await _primaryContext.Products.FindAsync(item.ProductId);
                if (product != null)
                {
                    item.UnitPrice = product.Price;
                    total += item.Quantity * item.UnitPrice;
                }
            }

            // Get shipping cost from primary context
            var rate = await _primaryContext.ShippingRates.FirstOrDefaultAsync(r => r.BaladiyaId == order.BaladiyaId);
            
            if (rate != null)
            {
                order.ShippingCost = order.DeliveryType == "Desk" ? rate.DeskPrice : rate.HomePrice;
            }
            else
            {
                order.ShippingCost = 0;
            }

            order.TotalAmount = total + order.ShippingCost;
            order.OrderDate = DateTime.UtcNow;

            // Get current database context and check if rotation is needed
            await _databaseSelector.CheckAndRotateIfNeededAsync();
            var currentContext = _databaseSelector.GetCurrentContext();

            // Add order to current database
            currentContext.Orders.Add(order);
            await currentContext.SaveChangesAsync();

            // Send Notification (Telegram)
            var dbName = _databaseSelector.GetCurrentDatabaseName();
            var message = $"📦 طلب جديد #{order.Id}\n👤 الاسم: {order.CustomerName}\n💰 المجموع: {order.TotalAmount} دج\n🗄️ قاعدة البيانات: {dbName}";
            await _notificationService.SendMessageAsync(message);

            return CreatedAtAction("GetOrder", new { id = order.Id }, order);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Order>> GetOrder(int id)
        {
            // Search across all databases
            foreach (var context in _databaseSelector.GetAllContexts())
            {
                var order = await context.Orders
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (order != null)
                {
                    // Load product details from primary context
                    foreach (var item in order.Items)
                    {
                        item.Product = await _primaryContext.Products.FindAsync(item.ProductId);
                    }
                    return order;
                }
            }

            return NotFound();
        }
    }
}
