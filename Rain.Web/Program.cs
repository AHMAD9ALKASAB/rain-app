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

// EF Core + Identity
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=(localdb)\\MSSQLLocalDB;Database=RainDb;Trusted_Connection=True;TrustServerCertificate=True";

builder.Services.AddDbContext<Rain.Infrastructure.Persistence.ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services
    .AddIdentity<Rain.Infrastructure.Identity.ApplicationUser, Microsoft.AspNetCore.Identity.IdentityRole>()
    .AddEntityFrameworkStores<Rain.Infrastructure.Persistence.ApplicationDbContext>()
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

// Basic security headers (CSP, X-Content-Type-Options, X-Frame-Options)
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
    // Allow Bootstrap CDN and Google reCAPTCHA
    var csp = string.Join("; ", new[]{
        "default-src 'self'",
        "script-src 'self' https://www.google.com https://www.gstatic.com",
        "style-src 'self' 'unsafe-inline'",
        "img-src 'self' data:",
        "font-src 'self' data:",
        "frame-src 'self' https://www.google.com"
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

// Seed roles/admin on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await Rain.Infrastructure.Seed.SeedData.SeedAsync(services);
}

app.Run();
