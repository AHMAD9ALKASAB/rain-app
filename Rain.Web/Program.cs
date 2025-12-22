using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Rain.Infrastructure.Identity;
using Rain.Infrastructure.Persistence;
using Rain.Infrastructure.Files;
using Rain.Web.Services;
using Rain.Infrastructure.Payments;
using Microsoft.AspNetCore.Identity.UI.Services;
using Rain.Infrastructure.Seed;
using Npgsql;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services
    .AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization(options =>
    {
        options.DataAnnotationLocalizerProvider = (type, factory) => factory.Create(typeof(Rain.Web.SharedResource));
    });
builder.Services.AddRazorPages();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("default", o =>
    {
        o.PermitLimit = 100;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueLimit = 0;
    });
});

// Files
builder.Services.AddScoped<IFileStorage, LocalFileStorage>();

// Email sender (SMTP via Gmail user-secrets)
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

// Notifications
builder.Services.AddScoped<INotificationService, NotificationService>();

// Chatbot
builder.Services.AddScoped<IChatbotService, ChatbotService>();

// reCAPTCHA
builder.Services.AddSingleton<Rain.Web.Services.IRecaptchaVerifier, Rain.Web.Services.RecaptchaVerifier>();

// Payments (select provider from config)
builder.Services.AddScoped<IPaymentProvider>(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var provider = cfg["Payment:Provider"] ?? "Mock";
    if (string.Equals(provider, "Stripe", StringComparison.OrdinalIgnoreCase))
    {
        return new StripePaymentProvider(cfg);
    }
    return new MockPaymentProvider();
});

// ============ EF Core + Identity ============
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=(localdb)\\MSSQLLocalDB;Database=RainDb;Trusted_Connection=True;TrustServerCertificate=True";

// **تحويل PostgreSQL URL من Render إلى صيغة قابلة للاستخدام - الإصلاح النهائي**
if (connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
{
    try
    {
        // استخدام Regex لتحليل الرابط يدوياً لأن Uri لا يتعامل مع الروابط بدون منفذ
        var match = Regex.Match(connectionString, 
            @"postgresql://([^:]+):([^@]+)@([^/]+)/([^?]+)");
        
        if (match.Success)
        {
            var username = match.Groups[1].Value;
            var password = match.Groups[2].Value;
            var host = match.Groups[3].Value;
            var database = match.Groups[4].Value;
            
            // أضف النطاق الكامل إذا كان من Render
            if (host.Contains("dpg-", StringComparison.OrdinalIgnoreCase) && !host.Contains(".", StringComparison.OrdinalIgnoreCase))
            {
                host = host + ".oregon-postgres.render.com";
            }
            
            connectionString = new NpgsqlConnectionStringBuilder
            {
                Host = host,
                Port = 5432, // المنفذ الافتراضي لـ PostgreSQL
                Database = database,
                Username = username,
                Password = password,
                SslMode = SslMode.Require,
                TrustServerCertificate = false // هذه القيمة لم تعد ضرورية
            }.ToString();
            
            Console.WriteLine($"✅ PostgreSQL connection string parsed successfully for {host}");
        }
        else
        {
            Console.WriteLine($"❌ Failed to parse PostgreSQL URL with Regex");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error parsing PostgreSQL URL: {ex.Message}");
        Console.WriteLine($"❌ Original URL: {connectionString}");
        
        // محاولة بديلة: استخدم الصيغة المباشرة
        try
        {
            connectionString = connectionString
                .Replace("postgresql://", "", StringComparison.OrdinalIgnoreCase)
                .Replace("@", ";Username=", StringComparison.OrdinalIgnoreCase)
                .Replace(":", ";Password=", StringComparison.OrdinalIgnoreCase) // تم إصلاح الخطأ هنا
                .Replace("/", ";Database=", StringComparison.OrdinalIgnoreCase) + ";Port=5432;SSL Mode=Require";
            
            // إضافة النطاق الكامل
            if (connectionString.Contains("dpg-", StringComparison.OrdinalIgnoreCase) && !connectionString.Contains("oregon-postgres.render.com", StringComparison.OrdinalIgnoreCase))
            {
                connectionString = connectionString
                    .Replace("dpg-", "dpg-", StringComparison.OrdinalIgnoreCase)
                    .Replace(";Host=", ";Host=", StringComparison.OrdinalIgnoreCase) + ".oregon-postgres.render.com";
            }
        }
        catch (Exception ex2)
        {
            Console.WriteLine($"❌ Alternative parsing also failed: {ex2.Message}");
        }
    }
}

// تحديد نوع قاعدة البيانات
var isPostgresConnection = connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase) || 
                          connectionString.Contains("postgres", StringComparison.OrdinalIgnoreCase) ||
                          connectionString.Contains("dpg-", StringComparison.OrdinalIgnoreCase);

Console.WriteLine($"📊 Is PostgreSQL: {isPostgresConnection}");
Console.WriteLine($"📊 Connection String length: {connectionString?.Length ?? 0}");

// تخزين القيم لاستخدامها لاحقاً
var isPostgres = isPostgresConnection;

builder.Services.AddDbContext<ApplicationDbContext>((provider, options) =>
{
    if (isPostgres)
    {
        // استخدام PostgreSQL
        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay = TimeSpan.FromSeconds(30),
                errorCodesToAdd = null);
        });
        Console.WriteLine("✅ Configured for PostgreSQL");
    }
    else
    {
        // استخدام SQL Server
        options.UseSqlServer(connectionString);
        Console.WriteLine("✅ Configured for SQL Server");
    }
});

// ============ بقية التهيئة ============
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultUI()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

// Identity password & lockout policies
builder.Services.Configure<IdentityOptions>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireDigit = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.SignIn.RequireConfirmedAccount = true;
});

// Localization (ar/en)
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
var supportedCultures = new[] { "ar", "en" };
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.SetDefaultCulture("ar");
    options.AddSupportedCultures(supportedCultures);
    options.AddSupportedUICultures(supportedCultures);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

// Request localization
app.UseRequestLocalization(app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<RequestLocalizationOptions>>().Value);

// Guard: Suppliers can only access ChangePassword under Identity Manage
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;
    if (path.StartsWith("/Identity/Account/Manage", StringComparison.OrdinalIgnoreCase)
        && !path.Contains("/ChangePassword", StringComparison.OrdinalIgnoreCase))
    {
        if (context.User?.Identity?.IsAuthenticated == true && context.User.IsInRole("Supplier"))
        {
            context.Response.Redirect("/Identity/Account/Manage/ChangePassword");
            return;
        }
    }
    await next();
});

// 🔧 **التعديل الأساسي: تحديث سياسة أمان CSP للسماح بمصادر CDN**
// Basic security headers (CSP, X-Content-Type-Options, X-Frame-Options)
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
    
    // ✅ **CSP المحدثة للسماح بـ Bootstrap, jQuery, والكود المدمج**
    var csp = string.Join("; ", new[]{
        "default-src 'self'",
        "script-src 'self' https://www.google.com https://www.gstatic.com https://cdn.jsdelivr.net https://code.jquery.com 'unsafe-inline'",
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net",
        "img-src 'self' data: https:",
        "font-src 'self' data: https:",
        "frame-src 'self' https://www.google.com",
        "connect-src 'self' https://api.openai.com"
    });
    
    context.Response.Headers["Content-Security-Policy"] = csp;
    await next();
});

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

// ============ Seed database ============
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var logger = services.GetRequiredService<ILogger<Program>>();
        
        logger.LogInformation($"📊 Database provider: {(isPostgres ? "PostgreSQL" : "SQL Server")}");
        
        if (isPostgres)
        {
            // لـ PostgreSQL: استخدام EnsureCreated بدلاً من Migrate
            logger.LogInformation("🔧 Ensuring PostgreSQL database is created...");
            await context.Database.EnsureCreatedAsync();
            logger.LogInformation("✅ PostgreSQL database ensured");
        }
        else
        {
            // لـ SQL Server: استخدام الهجرات
            logger.LogInformation("🔧 Applying SQL Server migrations...");
            await context.Database.MigrateAsync();
            logger.LogInformation("✅ SQL Server migrations applied");
        }
        
        // تشغيل seeding
        logger.LogInformation("🌱 Seeding database...");
        await SeedData.SeedAsync(services);
        logger.LogInformation("✅ Database seeding completed successfully");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "❌ An error occurred during database initialization");
        // لا توقف التطبيق - استمر
    }
}

// ============ إصلاح مشكلة البورت في Render ============
var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Run($"http://0.0.0.0:{port}");
