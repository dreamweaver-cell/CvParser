using CvParser.Infrastructure.Config;
using CvParser.Infrastructure.Services;
using CvParser.Web.Components.Account;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;



namespace CvParser.Infrastructure.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
        {
            // --- Configuration binding ---
            //services.Configure<OpenAiSettings>(config.GetSection("OpenAi"));
            services.Configure<OpenAiSettings>(options => 
                        config.GetSection("OpenAi").Bind(options));

            // --- HttpClient för OpenAI ---
            services.AddHttpClient<IAiService, OpenAiService>((sp, httpClient) =>
            {
                var settings = sp.GetRequiredService<IOptions<OpenAiSettings>>().Value;

                if (string.IsNullOrEmpty(settings.ApiKey))
                    throw new InvalidOperationException("OpenAi:ApiKey must be set in configuration.");

                httpClient.BaseAddress = new Uri(settings.BaseUrl);
                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", settings.ApiKey);
                httpClient.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));
            });

            // --- Applikationstjänster ---
            //services.AddScoped<IPdfService, PdfService>();
            services.AddScoped<IDocxService, DocxService>();
            services.AddScoped<IImageService, ImageService>();
            services.AddScoped<ICvService, CvService>();
            services.AddScoped<ISessionStorageService, SessionStorageService>();

            // --- Identity & Authentication ---
            services.AddCascadingAuthenticationState();
            services.AddAuthorizationCore();
            services.AddScoped<IdentityUserAccessor>();
            services.AddScoped<IdentityRedirectManager>();
            services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

            services.AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            }).AddIdentityCookies();

            var connectionString = config.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

            services.AddDbContext<CvParserDbContext>(options =>
                options.UseSqlServer(connectionString));

            services.AddDatabaseDeveloperPageExceptionFilter();

            services.AddIdentityCore<User>(options => options.SignIn.RequireConfirmedAccount = true)
                .AddEntityFrameworkStores<CvParserDbContext>()
                .AddSignInManager()
                .AddDefaultTokenProviders();

            services.AddSingleton<IEmailSender<User>, IdentityNoOpEmailSender>();

            // --- Logging ---
            services.AddLogging(logging =>
            {
                logging.AddConsole();
                logging.AddDebug();
            });

            // --- Session ---
            services.AddDistributedMemoryCache();
            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(20);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
            });
            services.AddHttpContextAccessor();

            // --- Antiforgery ---
            services.AddAntiforgery(options =>
            {
                options.HeaderName = "X-CSRF-TOKEN";
            });

            return services;
        }
    }
}
