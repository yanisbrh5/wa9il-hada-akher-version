using API.Data;
using API.Modeles;
using API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

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
            // Calculate totals (server-side validation)
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

            // Get shipping cost
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

            // Database rotation logic
            await _databaseSelector.CheckAndRotateIfNeededAsync();
            var currentContext = _databaseSelector.GetCurrentContext();

            currentContext.Orders.Add(order);
            await currentContext.SaveChangesAsync();

            // Send Telegram Notification
            var dbName = _databaseSelector.GetCurrentDatabaseName();
            
            var messageBuilder = new StringBuilder();
            messageBuilder.AppendLine("🎉 ═══════════════════");
            messageBuilder.AppendLine("📦 طلب جديد!");
            messageBuilder.AppendLine("═══════════════════");
            messageBuilder.AppendLine();
            messageBuilder.AppendLine($"🆔 رقم الطلب: #{order.Id}");
            messageBuilder.AppendLine($"👤 الاسم: {order.CustomerName}");
            messageBuilder.AppendLine($"📱 الهاتف: {order.CustomerPhone}");
            messageBuilder.AppendLine();
            
            // Get location names
            var wilaya = await _primaryContext.Wilayas.FindAsync(order.WilayaId);
            var baladiya = await _primaryContext.Baladiyas.FindAsync(order.BaladiyaId);
            
            messageBuilder.AppendLine($"📍 الولاية: {wilaya?.Name ?? order.WilayaId.ToString()}");
            messageBuilder.AppendLine($"📍 البلدية: {baladiya?.Name ?? order.BaladiyaId.ToString()}");
            messageBuilder.AppendLine($"📍 العنوان: {order.Address}");
            messageBuilder.AppendLine($"🚚 التوصيل: {(order.DeliveryType == "Home" ? "🏠 منزل" : "🏢 مكتب")}");
            messageBuilder.AppendLine();
            messageBuilder.AppendLine("🛍️ المنتجات:");
            
            foreach (var item in order.Items)
            {
                var product = await _primaryContext.Products.FindAsync(item.ProductId);
                var productName = product?.Name ?? "غير معروف";
                messageBuilder.AppendLine($"   • {productName} (x{item.Quantity})");
                if (!string.IsNullOrEmpty(item.SelectedColor))
                {
                    messageBuilder.AppendLine($"     🎨 لون: {item.SelectedColor}");
                }
            }
            
            messageBuilder.AppendLine();
            messageBuilder.AppendLine($"💰 المجموع: {order.TotalAmount} دج");
            messageBuilder.AppendLine($"🗄️ DB: {dbName}");
            messageBuilder.AppendLine("═══════════════════");

            await _notificationService.SendMessageAsync(messageBuilder.ToString());

            return CreatedAtAction("GetOrder", new { id = order.Id }, order);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Order>> GetOrder(int id)
        {
            foreach (var context in _databaseSelector.GetAllContexts())
            {
                var order = await context.Orders
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (order != null)
                {
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
