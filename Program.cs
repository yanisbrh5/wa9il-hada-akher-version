using API.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization; // مهم جداً لهذا السطر

namespace API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Configure Port for Render/Cloud Hosting
            var port = Environment.GetEnvironmentVariable("PORT") ?? "5214";
            builder.WebHost.UseUrls($"http://*:{port}");

            // Add services to the container.
            
            // Configure Database Settings
            builder.Services.Configure<API.Models.DatabaseSettings>(
                builder.Configuration.GetSection("DatabaseSettings"));
            builder.Services.Configure<API.Models.CleanupSettings>(
                builder.Configuration.GetSection("CleanupSettings"));

            // Get connection strings
            var db1ConnectionString = Environment.GetEnvironmentVariable("DB1_CONNECTION_STRING") ?? 
                                       builder.Configuration.GetConnectionString("Database1");
            var db2ConnectionString = Environment.GetEnvironmentVariable("DB2_CONNECTION_STRING") ?? 
                                       builder.Configuration.GetConnectionString("Database2");

            // Register both database contexts
            builder.Services.AddDbContext<StoreContext>(options =>
                options.UseNpgsql(db1ConnectionString));

            builder.Services.AddDbContext<API.Data.StoreContext2>(options =>
                options.UseNpgsql(db2ConnectionString));

            // Database Selector Service
            builder.Services.AddScoped<API.Services.IDatabaseSelector, API.Services.DatabaseSelector>();

            // Notification Service (Telegram)
            builder.Services.AddScoped<API.Services.INotificationService, API.Services.TelegramNotificationService>();
            
            // Photo Service (Cloudinary)
            builder.Services.AddScoped<API.Services.IPhotoService, API.Services.PhotoService>();

            // Background Services
            builder.Services.AddHostedService<API.Services.OrderCleanupService>();

            // ✅ تصحيح مشكلة JSON Loop
            builder.Services.AddControllers().AddJsonOptions(x =>
                x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);
            
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // CORS Configuration
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowFrontend",
                    policy =>
                    {
                        // ✅ تصحيح مشكلة CORS (السماح للجميع)
                        policy.SetIsOriginAllowed(origin => true) 
                              .AllowAnyHeader()
                              .AllowAnyMethod()
                              .AllowCredentials();
                    });
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            // Use CORS
            app.UseCors("AllowFrontend");

            app.UseAuthorization();

            app.MapControllers();

            // Health check endpoints
            app.MapMethods("/", new[] { "GET", "HEAD", "POST" }, () => "API is running!");
            app.MapMethods("/health", new[] { "GET", "HEAD", "POST" }, () => Results.Ok("Healthy"));
            app.MapMethods("/api", new[] { "GET", "HEAD", "POST" }, () => "API Root");

            // Database Seeding & Migration
            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var context = services.GetRequiredService<StoreContext>();
                var context2 = services.GetRequiredService<API.Data.StoreContext2>();
                try
                {
                    context.Database.Migrate();
                    context2.Database.Migrate();
                    Console.WriteLine("✅ Database 1 & 2 connections successful!");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Database connection failed: {ex.Message}");
                }
            }

            app.Run();
        }
    }
}
