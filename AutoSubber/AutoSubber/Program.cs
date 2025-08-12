using AutoSubber.Components;
using AutoSubber.Components.Account;
using AutoSubber.Data;
using AutoSubber.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AutoSubber
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Configure Serilog
            builder.Host.UseSerilog((context, configuration) =>
                configuration.ReadFrom.Configuration(context.Configuration));

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents()
                .AddInteractiveWebAssemblyComponents();

            // Add Web API controllers for webhook endpoints
            builder.Services.AddControllers();

            builder.Services.AddCascadingAuthenticationState();
            builder.Services.AddScoped<IdentityUserAccessor>();
            builder.Services.AddScoped<IdentityRedirectManager>();
            builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

            builder.Services.AddAuthentication(options =>
                {
                    options.DefaultScheme = IdentityConstants.ApplicationScheme;
                    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
                })
                .AddIdentityCookies(options =>
                {
                    // Configure secure cookie settings
                    options.ApplicationCookie.Configure(appCookieOptions =>
                    {
                        appCookieOptions.Cookie.HttpOnly = true;
                        appCookieOptions.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                        appCookieOptions.Cookie.SameSite = SameSiteMode.Strict;
                        appCookieOptions.Cookie.Name = "AutoSubber.Identity";
                        appCookieOptions.SlidingExpiration = true;
                        appCookieOptions.ExpireTimeSpan = TimeSpan.FromDays(30);
                    });
                    
                    options.ExternalCookie.Configure(extCookieOptions =>
                    {
                        extCookieOptions.Cookie.HttpOnly = true;
                        extCookieOptions.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                        extCookieOptions.Cookie.SameSite = SameSiteMode.Lax; // Lax for external providers
                        extCookieOptions.Cookie.Name = "AutoSubber.External";
                    });
                });

            // Add external authentication providers
            var authBuilder = builder.Services.AddAuthentication();
            
            // Configure Google OAuth if credentials are provided
            var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
            var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
            if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
            {
                authBuilder.AddGoogle(options =>
                {
                    options.ClientId = googleClientId;
                    options.ClientSecret = googleClientSecret;
                    options.CallbackPath = "/signin-google";
                    
                    // Request YouTube scopes for playlist management
                    options.Scope.Add("https://www.googleapis.com/auth/youtube.force-ssl");
                    options.Scope.Add("https://www.googleapis.com/auth/youtube.readonly");
                    options.Scope.Add("https://www.googleapis.com/auth/youtube");
                    
                    // Enable offline access to get refresh tokens
                    options.AccessType = "offline";
                    
                    // Save tokens to use with YouTube API
                    options.SaveTokens = true;
                });
            }
            
            // Configure Microsoft OAuth if credentials are provided
            var microsoftClientId = builder.Configuration["Authentication:Microsoft:ClientId"];
            var microsoftClientSecret = builder.Configuration["Authentication:Microsoft:ClientSecret"];
            if (!string.IsNullOrEmpty(microsoftClientId) && !string.IsNullOrEmpty(microsoftClientSecret))
            {
                authBuilder.AddMicrosoftAccount(options =>
                {
                    options.ClientId = microsoftClientId;
                    options.ClientSecret = microsoftClientSecret;
                    options.CallbackPath = "/signin-microsoft";
                });
            }

            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            
            // Configure Entity Framework with support for SQL Server, PostgreSQL, and SQLite
            var databaseProvider = builder.Configuration.GetValue<string>("DatabaseProvider") ?? "SqlServer";
            
            if (databaseProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
            {
                var postgresConnectionString = builder.Configuration.GetConnectionString("PostgreSQLConnection") ?? throw new InvalidOperationException("Connection string 'PostgreSQLConnection' not found.");
                builder.Services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseNpgsql(postgresConnectionString));
            }
            else if (databaseProvider.Equals("SQLite", StringComparison.OrdinalIgnoreCase) || 
                     !OperatingSystem.IsWindows())  // Use SQLite on non-Windows platforms
            {
                var sqliteConnectionString = builder.Configuration.GetConnectionString("SqliteConnection") ?? "Data Source=autosubber.db";
                builder.Services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseSqlite(sqliteConnectionString));
            }
            else
            {
                builder.Services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseSqlServer(connectionString));
            }
            
            builder.Services.AddDatabaseDeveloperPageExceptionFilter();

            builder.Services.AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddSignInManager()
                .AddDefaultTokenProviders();

            builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

            // Configure Data Protection
            ConfigureDataProtection(builder);

            // Register token encryption service
            builder.Services.AddScoped<ITokenEncryptionService, TokenEncryptionService>();
            
            // Register YouTube token refresh service
            builder.Services.AddScoped<IYouTubeTokenRefreshService, YouTubeTokenRefreshService>();
            
            // Register YouTube playlist service
            builder.Services.AddScoped<IYouTubePlaylistService, YouTubePlaylistService>();
            
            // Register YouTube subscription service
            builder.Services.AddScoped<IYouTubeSubscriptionService, YouTubeSubscriptionService>();

            // Register YouTube webhook service
            builder.Services.AddScoped<IYouTubeWebhookService, YouTubeWebhookService>();

            // Register PubSub subscription service
            builder.Services.AddScoped<IPubSubSubscriptionService, PubSubSubscriptionService>();
            
            // Register YouTube polling service for fallback when PubSub fails
            builder.Services.AddScoped<IYouTubePollingService, YouTubePollingService>();
            
            // Register HTTP client for PubSub service
            builder.Services.AddHttpClient<PubSubSubscriptionService>();
            
            // Register HTTP client for token refresh service
            builder.Services.AddHttpClient<YouTubeTokenRefreshService>();

            // Register background service for token refresh
            builder.Services.AddHostedService<TokenRefreshBackgroundService>();

            // Register background service for PubSub renewal
            builder.Services.AddHostedService<PubSubRenewalBackgroundService>();

            // Register background service for fallback polling
            builder.Services.AddHostedService<FallbackPollingBackgroundService>();

            var app = builder.Build();

            // Configure Serilog request logging
            app.UseSerilogRequestLogging();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseWebAssemblyDebugging();
                app.UseMigrationsEndPoint();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseAntiforgery();

            app.UseStaticFiles();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode()
                .AddInteractiveWebAssemblyRenderMode()
                .AddAdditionalAssemblies(typeof(Client._Imports).Assembly);

            // Add additional endpoints required by the Identity /Account Razor components.
            app.MapAdditionalIdentityEndpoints();

            // Map Web API controllers for webhook endpoints
            app.MapControllers();

            // Log application startup
            Log.Information("AutoSubber application starting up");

            try
            {
                app.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "AutoSubber application terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        /// <summary>
        /// Configures Data Protection for secure token encryption
        /// </summary>
        private static void ConfigureDataProtection(WebApplicationBuilder builder)
        {
            // Configure Data Protection with application name for key isolation
            builder.Services.AddDataProtection();

            // In production, log a warning about key persistence
            if (builder.Environment.IsProduction())
            {
                var keyDirectory = builder.Configuration["DataProtection:KeyDirectory"];
                if (string.IsNullOrEmpty(keyDirectory))
                {
                    Console.WriteLine("INFO: DataProtection:KeyDirectory not configured. Keys will use default persistence mechanism. For persistent keys across deployments, configure a persistent storage location.");
                }
                else
                {
                    Console.WriteLine($"INFO: DataProtection key directory configured: {keyDirectory}");
                    // Note: PersistKeysToFileSystem requires additional configuration in deployment
                    // For now, rely on default persistence mechanisms
                }
            }
        }
    }
}
