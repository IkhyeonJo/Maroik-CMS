// Maroik.Website - Program.cs
// Entry point for the Maroik web application.
// Configures DI services (repositories, services, clients, session, antiforgery, CORS, localization),
// sets up hot-swap TLS certificate polling for Docker deployments, and builds the ASP.NET Core pipeline.

using System.Globalization;
using Maroik.Core.Client.Extensions;
using Maroik.Core.Contract.Misc.Enums;
using Maroik.Core.Contract.Misc.Settings;
using Maroik.Core.Domain.Localization;
using Maroik.Core.Repository.Extensions;
using Maroik.Core.Service.Extensions;
using Maroik.Website.Constants;
using Maroik.Website.Extensions;
using Maroik.Website.Filters;
using Maroik.Website.Middlewares;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.AspNetCore.Session;
using Serilog;
using Serilog.Events;
using StackExchange.Redis;

// Bootstrap logger captures startup errors before the host is built.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: LogTemplates.Console)
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    WebApplication? app = null; // assigned after Build() below; captured by the Kestrel cert selector closure

    builder.Host.UseSerilog((_, _, configuration) => configuration
        .MinimumLevel.Information()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
        .MinimumLevel.Override("System", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .WriteTo.Console(outputTemplate: LogTemplates.Console));

    const string cookiePrefix = "__Secure-";

    #region ServerSettings

    // Strongly-typed binding of the whole "ServerSetting" section, plus the handful of values that
    // live in sibling sections (Clamav, FileStorage) or a nested subSection (SmtpOptions) the flat
    // model can't reach directly. Replaces a handWritten Convert.ToX(section["..."]) wall in which
    // a missing numeric silently became 0 (e.g. SessionExpireMinutes = 0 -> sessions expire at once).
    static void BindServerSetting(IConfiguration configuration, ServerSetting target)
    {
        configuration.GetSection("ServerSetting").Bind(target);
        configuration.GetSection("ServerSetting:SmtpOptions").Bind(target);
        target.ClamavHost = configuration["Clamav:Host"];
        target.ClamavPort = configuration.GetValue<int>("Clamav:Port");
        target.FileStorageBaseUrl = configuration["FileStorage:BaseUrl"];
        target.RsaAlgorithm =
            Enum.TryParse<RsaType>(configuration["ServerSetting:RsaAlgorithm"], ignoreCase: true, out var parsed)
                ? parsed
                : RsaType.Rsa2;
    }

    // A local copy for the values needed before the DI container is built (middleware / Kestrel).
    var serverSetting = new ServerSetting();
    BindServerSetting(builder.Configuration, serverSetting);

    if (serverSetting.SessionExpireMinutes <= 0)
        throw new InvalidOperationException("ServerSetting:SessionExpireMinutes must be a positive number.");
    if (serverSetting.MaxLoginAttempt == 0)
        throw new InvalidOperationException("ServerSetting:MaxLoginAttempt must be greater than zero.");

    var cfgDomainName = serverSetting.DomainName ?? "";
    var cfgSessionExpireMinutes = serverSetting.SessionExpireMinutes;
    var cfgDockerCertPath = serverSetting.DockerCertPath;
    var cfgDockerKeyPath = serverSetting.DockerKeyPath;

    // Register ServerSetting as IOptions<ServerSetting> for DI.
    builder.Services.Configure<ServerSetting>(cfg => BindServerSetting(builder.Configuration, cfg));

    #endregion

    #region AddDbContext

    builder.Services.AddRepositoryContext(builder.Configuration);

    #endregion

    #region AddRepositories

    builder.Services.AddRepositoryServices();

    #endregion

    #region AddServices

    builder.Services.AddApplicationServices();
    builder.Services
        .AddScoped<Maroik.Website.Contracts.IExcelExportService, Maroik.Website.Services.ExcelExportService>()
        .AddScoped<Maroik.Website.Contracts.ISessionService, Maroik.Website.Services.SessionService>()
        .AddSingleton<Maroik.Website.Contracts.ITrustedDeviceCookie, Maroik.Website.Services.TrustedDeviceCookie>();

    // Registered here (before Build()) so it goes through DI like the other services. ConfigureKestrel
    // below must also run before Build() - the service collection becomes read-only once Build() runs.
    // The selector closure captures `app`, which is only assigned after Build() (see below), but the
    // selector itself isn't invoked until the first TLS handshake, long after that.
    if (!string.IsNullOrEmpty(cfgDockerCertPath) && !string.IsNullOrEmpty(cfgDockerKeyPath))
    {
        builder.Services.AddSingleton<Maroik.Website.Contracts.ICertificateManager>(sp =>
            new Maroik.Website.Services.CertificateManager(
                cfgDockerCertPath,
                cfgDockerKeyPath,
                sp.GetRequiredService<ILogger<Maroik.Website.Services.CertificateManager>>()));

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ConfigureHttpsDefaults(https =>
            {
                // ReSharper disable once AccessToModifiedClosure - `app` is assigned exactly once
                // (right after Build(), below) before this selector can ever be invoked.
                https.ServerCertificateSelector = (_, _) =>
                    app!.Services.GetRequiredService<Maroik.Website.Contracts.ICertificateManager>()
                        .SelectCertificate();
            });
        });
    }

    #endregion

    #region AddClients

    string rabbitMqConnectionString = builder.Configuration.GetConnectionString("RabbitMQ")
        ?? "amqp://guest:guest@localhost:5672/";
    builder.Services.AddClientServices(rabbitMqConnectionString);
    builder.Services.AddHttpClient();

    #endregion

    #region Valkey / Session / DataProtection
    // Valkey is wire-protocol compatible with Redis, so the StackExchange.Redis client
    // (there is no separate "StackExchange.Valkey" package) connects to it unmodified.

    var valkeyConnectionString = builder.Configuration.GetConnectionString("Valkey");
    IConnectionMultiplexer? valkey = null;
    if (!string.IsNullOrEmpty(valkeyConnectionString))
    {
        // AbortOnConnectFail = false so a Valkey that is briefly unreachable at boot (its container
        // still starting, a transient network blip) does not take the whole web app down with it —
        // the multiplexer connects in the background and retries instead of throwing here.
        var valkeyOptions = ConfigurationOptions.Parse(valkeyConnectionString);
        valkeyOptions.AbortOnConnectFail = false;
        valkey = await ConnectionMultiplexer.ConnectAsync(valkeyOptions);
    }

    if (valkey != null)
    {
        builder.Services.AddSingleton(valkey);
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.ConnectionMultiplexerFactory = () => Task.FromResult(valkey);
            options.InstanceName = "MaroikWebsite:";
        });
        builder.Services.AddDataProtection()
            .PersistKeysToStackExchangeRedis(valkey, "MaroikWebsite:DataProtection-Keys")
            .SetApplicationName("Maroik");
    }
    else
    {
        builder.Services.AddDistributedMemoryCache();

        // No Valkey: persist the Data Protection keyring to a stable folder instead of the default
        // ephemeral per-process one, so session and antiforgery cookies issued before a restart
        // stay decryptable afterward. Still not shared across instances — Valkey is required for a
        // scaled-out deployment.
        string keyRingPath = Path.Combine(builder.Environment.ContentRootPath, "dataprotection-keys");
        _ = Directory.CreateDirectory(keyRingPath);
        builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath))
            .SetApplicationName("Maroik");
        Log.Warning(
            "No Valkey connection string configured — Data Protection keys persist to {KeyRingPath} " +
            "and the distributed cache is in-process. Multi-instance deployments require Valkey",
            keyRingPath);
    }

    string cookieDomain = cfgDomainName.Replace("https://", "").Replace("/", "");

    builder.Services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromMinutes(cfgSessionExpireMinutes);
        options.Cookie.Name = $"{cookiePrefix}{SessionDefaults.CookieName}";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.Domain = cookieDomain;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
    });

    #endregion

    #region AddAntiforgery

    builder.Services.AddAntiforgery(options =>
    {
        options.Cookie.Name = $"{cookiePrefix}{AntiforgeryOptions.DefaultCookiePrefix}";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.Domain = cookieDomain;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
    });

    #endregion

    #region AddCors

    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(corsPolicyBuilder =>
        {
            // Origin = scheme + host (+ port), no trailing slash. The old Replace("/", "") also ate
            // the "//" in "https://", producing the invalid origin "https:host" that matched nothing.
            var corsOrigin = cfgDomainName.TrimEnd('/');
            if (!string.IsNullOrEmpty(corsOrigin))
                _ = corsPolicyBuilder.WithOrigins(corsOrigin);
        });
    });

    #endregion

    builder.Services.AddHttpContextAccessor();
    // builder.Services.AddMemoryCache(); // For IMemoryCache DI - unused; IDistributedCache(Valkey) used instead for scale-out
    // No AddResponseCaching / UseResponseCaching: the middleware below stamps every response
    // Cache-Control: no-store, so nothing is ever cacheable and the response-caching middleware
    // would only ever be a no-op. [ResponseCache] on individual actions is header-only and needs
    // neither.

    builder.Services.AddHsts(options => // https://blog.elmah.io/the-asp-net-core-security-headers-guide/
    {
        options.IncludeSubDomains = true;
        // 2 years — the value the (now-removed) hand-appended Strict-Transport-Security header
        // carried; UseHsts() below is the single emitter, and it only fires over HTTPS.
        options.MaxAge = TimeSpan.FromDays(730);
        options.Preload = true;
        // Do NOT add the live domain here: ExcludedHosts is the set of hosts the header is
        // withheld for. Its defaults (localhost / 127.0.0.1 / [::1]) already keep HSTS off for
        // local dev over plain HTTP; adding the production host would suppress the header on the
        // one host that actually needs it.
    });

    #region Multi-Language

    builder.Services.AddLocalization(opt => { opt.ResourcesPath = "Resources"; });
    // AddControllersWithViews (not AddMvc): this app has no Razor Pages, so AddMvc is extra Razor
    // Pages services were dead weight. The filters/options for the same builder are added by the
    // AddControllersWithViews call further below; the two calls merge into one MVC builder.
    builder.Services.AddControllersWithViews()
        .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
        .AddDataAnnotationsLocalization();

    builder.Services.Configure<RequestLocalizationOptions>(opt =>
    {
        List<CultureInfo> supportedCultures = CulturePolicy.SupportedCultures.Select(c => new CultureInfo(c)).ToList();
        opt.DefaultRequestCulture = new RequestCulture(CulturePolicy.DefaultCulture);
        opt.SupportedCultures = supportedCultures;
        opt.SupportedUICultures = supportedCultures;
    });

    #endregion

    // Custom checks (not the AspNetCore health checks.* NuGet packages) so /health actually reflects
    // whether the app can reach its dependencies, for a post-deploy / container health probe.
    var healthChecksBuilder = builder.Services.AddHealthChecks()
        .AddCheck<Maroik.Core.Repository.HealthChecks.DatabaseHealthCheck>("database")
        .AddTypeActivatedCheck<Maroik.Core.Client.HealthChecks.RabbitMqHealthCheck>(
            "rabbitmq", args: [rabbitMqConnectionString]);

    if (valkey != null)
    {
        healthChecksBuilder.AddCheck<Maroik.Website.HealthChecks.ValkeyHealthCheck>("valkey");
    }

    builder.Services.AddControllersWithViews(options =>
    {
        _ = options.Filters.Add<AuthorizationFilter>();
        _ = options.Filters.Add<ViewBagPopulatorFilter>();
        options.Filters.Add(
            new AutoValidateAntiforgeryTokenAttribute()); // Globally enables token validation for all requests except for GET, HEAD, OPTIONS, and TRACE
    });

    app = builder.Build();

    #region Hot Swap Cert

    if (!string.IsNullOrEmpty(cfgDockerCertPath) && !string.IsNullOrEmpty(cfgDockerKeyPath))
    {
        var certManager = app.Services.GetRequiredService<Maroik.Website.Contracts.ICertificateManager>();

        var certTimer = new System.Timers.Timer(TimeSpan.FromHours(1).TotalMilliseconds)
        {
            AutoReset = true
        };
        certTimer.Elapsed += (_, _) => certManager.TryReload();
        certTimer.Start();

        // Stop and dispose the timer on graceful shutdown so it can't fire a reload into a
        // half-torn-down host.
        app.Lifetime.ApplicationStopping.Register(() =>
        {
            certTimer.Stop();
            certTimer.Dispose();
        });

        app.Logger.LogInformation("Certificate polling started");
    }

    #endregion

    app.UseCookiePolicy(new CookiePolicyOptions
    {
        HttpOnly = HttpOnlyPolicy.Always,
        Secure = CookieSecurePolicy.Always,
    });

    app.UseExceptionHandler("/Exception/Error");

    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();

    // /health bypasses HTTPS redirect and www-redirect so it is reachable on HTTP port 80
    // from the host (e.g. a post-deploy health probe). Ordinal: the probe hits the exact-case
    // "/health", matching StartsWithSegments' own default is not relied on (it is
    // OrdinalIgnoreCase).
    app.UseWhen(
        ctx => !ctx.Request.Path.StartsWithSegments("/health", StringComparison.Ordinal),
        pipeline =>
        {
            pipeline.UseHttpsRedirection();
            pipeline.UseRewriter(new RewriteOptions().AddRedirectToWww());
        });

    app.UseSerilogRequestLogging(); // Log each HTTP request (method, path, status, duration) via Serilog
    app.UseRouting(); // Routing middleware for routing requests (UseRouting)
    app.UseCors();

    #region Login

    app.UseSession(); // Session middleware (UseSession) establishes and maintains session state. If the app uses session state, call Session Middleware after Cookie Policy Middleware and before MVC Middleware.
    app.UseAuthentication(); // Authentication middleware (UseAuthentication) attempts to authenticate the user before they're allowed access to secure resources.
    app.UseAuthorization(); // Authorization middleware (UseAuthorization) authorizes a user to access secure resources.

    #endregion

    #region Default HTTP Header Setting

    #region Apply the following code to all pages. After logout, pressing back button shows logged-in page, so cache is disabled

    //<META http-equiv="Expires" content="-1">
    //<META http-equiv="Pragma" content="no-cache">
    //<META http-equiv="Cache-Control" content="No-Cache">

    //The code below is equivalent to the metadata content that goes into the HTML Header above.

    app.Use(async (context, next) =>
    {
        context.Response.GetTypedHeaders().CacheControl =
            new Microsoft.Net.Http.Headers.CacheControlHeaderValue()
            {
                //Public = true,
                //MaxAge = TimeSpan.FromSeconds(10),
                NoCache = true, // For resolve logout back button problem
                NoStore = true // For resolve logout back button problem
            };
        context.Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.Vary] =
            (string[])["Accept-Encoding"];

        #region Security Headers [https: //blog.elmah.io/the-asp-net-core-security-headers-guide/]

        // Strict-Transport-Security is emitted by app.UseHsts() alone (see AddHsts above) — a second
        // handWritten header here produced two conflicting max-age values on every HTTPS response.
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        // X-XSS-Protection deliberately set to 0: the legacy auditor it enables is itself a source
        // of vulnerabilities in the browsers that still honor it, and modern guidance (OWASP) is to
        // disable it and rely on the Content-Security-Policy below.
        context.Response.Headers.Append("X-Xss-Protection", "0");
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("Referrer-Policy", "no-referrer");
        context.Response.Headers.Append("X-Permitted-Cross-Domain-Policies", "none");
        context.Response.Headers.Append("Cross-Origin-Embedder-Policy", "require-corp");
        context.Response.Headers.Append("Cross-Origin-Opener-Policy", "same-origin");
        context.Response.Headers.Append("Permissions-Policy",
            "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()");
        // default-src 'self' closes the fallback for the directives not named explicitly
        // (font-src, media-src, worker-src, manifest-src, frame-src, …), which were previously
        // unrestricted. The deprecated report-uri (which pointed at the site root — no collector
        // exists to receive the POSTed reports) is dropped.
        context.Response.Headers.Append("Content-Security-Policy",
            "default-src 'self'; img-src 'self' blob:; connect-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; object-src 'none'; base-uri 'none'; form-action 'self'; frame-ancestors 'none'"); // [https://csper.io/blog/no-more-unsafe-inline] [https://csp-evaluator.withgoogle.com/?csp=https://www.maroik.com]

        #endregion

        await next();
    });

    #endregion

    #endregion

    #region Multi-Language

    string[] supportedCultures = [.. CulturePolicy.SupportedCultures];
    var localizationOptions = new RequestLocalizationOptions().SetDefaultCulture(CulturePolicy.DefaultCulture)
        .AddSupportedCultures(supportedCultures)
        .AddSupportedUICultures(supportedCultures);

    app.UseRequestLocalization(localizationOptions);

    #endregion

    app.MapHealthChecks("/health");
    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Dashboard}/{action=AnonymousIndex}/{id?}");
    // Must run immediately before MapStaticAssets so it can 403 role-restricted /admin and /user
    // static file paths before the static file middleware gets a chance to serve them.
    app.UseMiddleware<RoleBasedStaticFileMiddleware>();
    // Also before MapStaticAssets: lazily pull an uploaded avatar from the shared file-storage
    // service into this replica's wwwroot on first request, so the static-file middleware below can
    // then serve it normally.
    app.UseMiddleware<AvatarCacheMiddleware>();
    app.MapStaticAssets();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Maroik.Website terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
