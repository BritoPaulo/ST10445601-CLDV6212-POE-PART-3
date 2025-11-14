using System.Globalization;
using ABCRetailers.Data;
using ABCRetailers.Models.SQL;
using ABCRetailers.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ✅ Add MVC support
builder.Services.AddControllersWithViews();

// ✅ Configure SQL Server Connection
builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ✅ FIXED: Authentication Configuration
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login/Login";
        options.AccessDeniedPath = "/Login/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(2);
        options.SlidingExpiration = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.None; // For development
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = context =>
            {
                // Prevent redirect loops by checking if we're already going to login
                if (context.Request.Path.StartsWithSegments("/Login"))
                {
                    context.Response.StatusCode = 401;
                    return Task.CompletedTask;
                }
                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            }
        };
    });

// ✅ Add Authorization
builder.Services.AddAuthorization();

// ✅ Register services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IAzureStorageService, AzureStorageService>();

// ✅ Configure Functions API client
builder.Services.AddHttpClient<IFunctionsApi, FunctionsApiClient>(client =>
{
    client.BaseAddress = new Uri("http://localhost:7137/api/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

// ✅ CORRECT Middleware Order - CRITICAL FIX
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// ✅ MUST be in this exact order
app.UseAuthentication();
app.UseAuthorization();

// ✅ Map controllers
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Create default users
using (var scope = app.Services.CreateScope())
{
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

        await context.Database.EnsureCreatedAsync();

        // Create default admin user
        if (!await context.Users.AnyAsync(u => u.Username == "admin"))
        {
            var adminUser = new SqlUser
            {
                UserId = Guid.NewGuid(),
                Username = "admin",
                Email = "admin@abcretailers.com",
                PasswordHash = authService.HashPassword("Admin123!"),
                Role = "Admin",
                CreatedDate = DateTime.UtcNow,
                IsActive = true
            };
            context.Users.Add(adminUser);
            await context.SaveChangesAsync();
        }

        // Create default customer user
        if (!await context.Users.AnyAsync(u => u.Username == "customer"))
        {
            var customerUser = new SqlUser
            {
                UserId = Guid.NewGuid(),
                Username = "customer",
                Email = "customer@abcretailers.com",
                PasswordHash = authService.HashPassword("Customer123!"),
                Role = "Customer",
                CreatedDate = DateTime.UtcNow,
                IsActive = true
            };
            context.Users.Add(customerUser);
            await context.SaveChangesAsync();
        }
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Failed to create default users");
    }
}

app.Run();