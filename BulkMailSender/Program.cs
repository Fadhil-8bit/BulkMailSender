using BulkMailSender.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace BulkMailSender
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Configure Kestrel for large file uploads
            builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = 524288000; // 500 MB
                options.ValueLengthLimit = 524288000;
                options.MultipartHeadersLengthLimit = 524288000;
                options.BufferBody = false; // Don't buffer entire body in memory
                options.MemoryBufferThreshold = 65536; // 64KB threshold
                options.BufferBodyLengthLimit = 524288000;
            });

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.Limits.MaxRequestBodySize = 524288000; // 500 MB
                options.Limits.MinRequestBodyDataRate = null; // Disable minimum data rate for large files
                options.Limits.MinResponseDataRate = null;
                options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(10); // Increase keep-alive
                options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(10); // Increase header timeout
                
                // Disable HTTP/2 to fix ERR_HTTP2_PROTOCOL_ERROR
                options.ConfigureEndpointDefaults(listenOptions =>
                {
                    listenOptions.Protocols = HttpProtocols.Http1;
                });
                
                // Additional timeout settings
                options.Limits.MaxRequestLineSize = 16384; // 16 KB
                options.Limits.MaxRequestHeadersTotalSize = 65536; // 64 KB
            });

            // Add session support for large data storage (instead of cookie-based TempData)
            builder.Services.AddDistributedMemoryCache();
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            // Add services to the container.
            builder.Services.AddRazorPages();
            
            // Register custom services
            builder.Services.AddScoped<IZipExtractionService, ZipExtractionService>();
            
            // Register background job services
            builder.Services.AddSingleton<EmailSendQueueService>();
            builder.Services.AddHostedService<BackgroundEmailSendService>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
                app.UseHttpsRedirection();
            }
            else
            {
                // In development, make HTTPS optional
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            // Enable session middleware (MUST be before UseAuthorization)
            app.UseSession();

            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapRazorPages()
               .WithStaticAssets();

            app.Run();
        }
    }
}
