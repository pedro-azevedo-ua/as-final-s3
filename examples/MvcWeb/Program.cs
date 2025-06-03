using Microsoft.EntityFrameworkCore;
using Piranha;
using Piranha.Services;
using Piranha.AspNetCore.Identity.SQLite;
using Piranha.AttributeBuilder;
using Piranha.Data.EF.SQLite;
using Piranha.Manager.Editor;
using ContentsRUs.Eventing.Listener;

using ContentsRUs.Eventing.Publisher;
using MvcWeb.Services;
using Serilog;
using Serilog.Context;
using ContentRUs.Eventing.Listener.BackgroundServices;
using Swashbuckle.AspNetCore.SwaggerGen;
using Microsoft.OpenApi.Models;

using Prometheus;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, options) =>
{
    options
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "Piranha.CMS")
        .Enrich.WithMachineName()
        .Enrich.WithProcessId()
        .Enrich.WithThreadId()
        .ReadFrom.Configuration(context.Configuration); // Read from appsettings.json if needed
});

builder.AddPiranha(options =>
{
    /**
     * This will enable automatic reload of .cshtml
     * without restarting the application. However since
     * this adds a slight overhead it should not be
     * enabled in production.
     */
    options.AddRazorRuntimeCompilation = true;

    options.UseCms();
    options.UseManager();

    options.UseFileStorage(naming: Piranha.Local.FileStorageNaming.UniqueFolderNames);
    options.UseImageSharp();
    options.UseTinyMCE();
    options.UseMemoryCache();

    var connectionString = builder.Configuration.GetConnectionString("piranha");
    options.UseEF<SQLiteDb>(db => db.UseSqlite(connectionString));
    options.UseIdentityWithSeed<IdentitySQLiteDb>(db => db.UseSqlite(connectionString));

    /**
     * Here you can configure the different permissions
     * that you want to use for securing content in the
     * application.
    options.UseSecurity(o =>
    {
        o.UsePermission("WebUser", "Web User");
    });
     */

    /**
     * Here you can specify the login url for the front end
     * application. This does not affect the login url of
     * the manager interface.
    options.LoginUrl = "login";
     */
});

builder.Services.TryDecorate<IPageService, CustomPageService>();

builder.Services.AddSingleton<IPiranhaEventPublisher, PiranhaEventPublisher>();
builder.Services.AddHostedService<PiranhaPublisherInitializer>();


builder.Services.AddHostedService<ExternalEventListenerService>();
builder.Services.AddHostedService<DlqConsumerHostedService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });
    c.DocumentFilter<MetricsDocumentFilter>();
});



//builder.Services.AddSingleton<IHostedService>(sp => new ExternalEventListenerService(
//    sp,
//    builder.Configuration,
//    sp.GetRequiredService<ILogger<ExternalEventListenerService>>()

//));

var app = builder.Build();



// Middleware for correlation ID:
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? Guid.NewGuid().ToString();
    using (LogContext.PushProperty("CorrelationId", correlationId))
    {
        context.Response.Headers["X-Correlation-ID"] = correlationId;
        await next();
    }
});

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();

}

// Add the middleware to collect Prometheus metrics

app.UseRouting(); // <-- necessary to use endpoints after
app.UseHttpMetrics();

app.UsePiranha(options =>
{
    // Initialize Piranha
    App.Init(options.Api);

    // Build content types
    new ContentTypeBuilder(options.Api)
        .AddAssembly(typeof(Program).Assembly)
        .Build()
        .DeleteOrphans();

    // Configure Tiny MCE
    EditorConfig.FromFile("editorconfig.json");

    options.UseManager();
    options.UseTinyMCE();
    options.UseIdentity();
});

// Map the endpoint to metrics (/metrics)
app.UseEndpoints(endpoints =>
{
    endpoints.MapMetrics();
});

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
});


app.Run();