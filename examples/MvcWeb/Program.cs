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

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, options) =>
{
    options
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "Piranha.CMS")
        .Enrich.WithMachineName()
        .Enrich.WithProcessId()
        .Enrich.WithThreadId()
        .WriteTo.Logger(lc => lc
            .Filter.ByIncludingOnly("SourceContext like '%RabbitMQ%'")
            .WriteTo.File(
                path: "Logs/Events/events-.log",
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] {CorrelationId} {Message}{NewLine}{Exception}")
        )
        .WriteTo.Logger(lc => lc
            .Filter.ByIncludingOnly("SourceContext like '%Security%'")
            .WriteTo.File(
                path: "Logs/Security/security-.log",
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level}] {CorrelationId} {Message}{NewLine}{Exception}")
        )
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

builder.Services.AddSingleton<IPiranhaEventPublisher>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new PiranhaEventPublisher(
        config["RabbitMQ:HostName"] ?? "localhost",
        int.Parse(config["RabbitMQ:Port"] ?? "5672"),
        config["RabbitMQ:UserName"] ?? "user",
        config["RabbitMQ:Password"] ?? "password"
    );
});

builder.Services.AddSingleton<IHostedService>(sp => new ExternalEventListenerService(
    sp,
    builder.Configuration,
    sp.GetRequiredService<ILogger<ExternalEventListenerService>>()

));

var app = builder.Build();



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

app.Run();