using CvParser.Infrastructure.Extensions;
using CvParser.Infrastructure.Services;
using CvParser.Infrastructure.Services.Debug;
using System.Net.Http.Headers;
using CvParser.Web.Components;
using Microsoft.AspNetCore.Http.Features;



namespace CvParser.Web
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // --- Ladda OpenAI-inställningar ---
            var openAiBaseUrl = builder.Configuration["OpenAI:BaseUrl"];
            var openAiApiKey = builder.Configuration["OpenAI:ApiKey"];

            if (string.IsNullOrEmpty(openAiBaseUrl))
                throw new ArgumentException("OpenAI:BaseUrl saknas i appsettings.json");

            if (string.IsNullOrEmpty(openAiApiKey))
                throw new ArgumentException("OpenAI:ApiKey saknas i appsettings.json");

            // --- Razor & MVC ---
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();
            builder.Services.AddRazorPages();
            
            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNamingPolicy = null;
                });

            // --- Infrastruktur & tjänster ---
            builder.Services.AddInfrastructure(builder.Configuration);
            //builder.Services.AddScoped<JSRunner>();
            var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"];
            if (string.IsNullOrEmpty(apiBaseUrl))
            {
                throw new InvalidOperationException("ApiSettings:BaseUrl saknas i appsettings.json.");
            }
            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });

            // --- HttpClient för OpenAI ---
            builder.Services.AddHttpClient<OpenAiService>(client =>
            {
                client.BaseAddress = new Uri(openAiBaseUrl);
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", openAiApiKey);
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));
            });

            // --- Debug-wrapper ---
            builder.Services.AddScoped<IAiService>(sp =>
            {
                var inner = sp.GetRequiredService<OpenAiService>();
                var logger = sp.GetRequiredService<ILogger<AiServiceDebug>>();
                return new AiServiceDebug(inner, logger);
            });

            builder.Services.AddScoped<ICvParserService, CvParserService>();
            builder.Services.AddScoped<ICvService, CvService>();
            builder.Services.AddScoped<ICvParserService, CvParserService>();
            builder.Services.AddScoped<ICvDocumentGenerator, CvDocumentGenerator>();
            builder.Services.AddScoped<IDocxService, DocxService>();
            //builder.Services.AddScoped<IPdfService, PdfService>();
            builder.Services.AddScoped<IImageService, ImageService>();
            builder.Services.AddSingleton<IImageService, ImageService>();

            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30); // hur länge sessionen lever
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Strict;
            });

            builder.Services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10 MB
            });

            var app = builder.Build();

            // --- Middleware ---
            if (app.Environment.IsDevelopment())
            {
                app.UseMigrationsEndPoint();
                app.UseDeveloperExceptionPage(); // Lämpligare för utveckling
            }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();
            app.MapBlazorHub();
            app.MapFallbackToPage("/_Host");

            app.UseAntiforgery();
            
            // --- Endpoints ---
            // Använd endast dessa. Ordningen är viktig.
            app.UseSession();
            app.MapRazorPages();
            app.MapControllers();
            app.Run();
            app.MapRazorComponents<App>()
               .AddInteractiveServerRenderMode();
        }
    }
}
