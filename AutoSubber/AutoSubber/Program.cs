using AutoSubber.Components;
using AutoSubber.Components.Account;
using AutoSubber.Data;
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
            
            // Configure Entity Framework with support for both SQL Server and PostgreSQL
            var databaseProvider = builder.Configuration.GetValue<string>("DatabaseProvider") ?? "SqlServer";
            
            if (databaseProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
            {
                var postgresConnectionString = builder.Configuration.GetConnectionString("PostgreSQLConnection") ?? throw new InvalidOperationException("Connection string 'PostgreSQLConnection' not found.");
                builder.Services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseNpgsql(postgresConnectionString));
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
    }
}
