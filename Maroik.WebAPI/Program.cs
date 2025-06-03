using Maroik.Common.DataAccess.Contracts;
using Maroik.Common.DataAccess.Data;
using Maroik.Common.DataAccess.Services;
using Maroik.Common.Miscellaneous.Utilities;
using Maroik.WebAPI.Contracts;
using Maroik.WebAPI.Infrastructure;
using Maroik.WebAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Authentication;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Runtime.InteropServices;

X509Certificate2 currentCert;
string previousCertHash;
string previousCertKeyHash;
object certLock = new();
var isReloading = false;
// ReSharper disable once TooWideLocalVariableScope
System.Timers.Timer certTimer;

var builder = WebApplication.CreateBuilder(args);

#region ServerSettings
ServerSetting.MaxLoginAttempt = Convert.ToByte(builder.Configuration.GetSection("ServerSetting")["MaxLoginAttempt"]);
ServerSetting.DockerCertPath = builder.Configuration.GetSection("ServerSetting")["DockerCertPath"];
ServerSetting.DockerKeyPath = builder.Configuration.GetSection("ServerSetting")["DockerKeyPath"];
#endregion

// Add services to the container.

builder.Services.AddControllers();

JwtTokenConfig jwtTokenConfig = builder.Configuration.GetSection("jwtTokenConfig").Get<JwtTokenConfig>();
builder.Services.AddSingleton(jwtTokenConfig);
builder.Services.AddAuthentication(x =>
{
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(x =>
{
    //x.RequireHttpsMetadata = true;
    x.SaveToken = true;
    x.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtTokenConfig.Issuer,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtTokenConfig.Secret)),
        ValidAudience = jwtTokenConfig.Audience,
        ValidateAudience = true,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1)
    };
});
builder.Services.AddSingleton<IJwtAuthManager, JwtAuthManager>();
builder.Services.AddHostedService<JwtRefreshTokenCache>();

#region AddDbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions => npgsqlOptions.EnableRetryOnFailure()
    ).EnableSensitiveDataLogging()
);
#endregion

#region AddRepositories

#region DB
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<ISubCategoryRepository, SubCategoryRepository>();
builder.Services.AddScoped<IAssetRepository, AssetRepository>();
builder.Services.AddScoped<IIncomeRepository, IncomeRepository>();
builder.Services.AddScoped<IExpenditureRepository, ExpenditureRepository>();
builder.Services.AddScoped<IFixedIncomeRepository, FixedIncomeRepository>();
builder.Services.AddScoped<IFixedExpenditureRepository, FixedExpenditureRepository>();
#endregion

#region LOCAL
builder.Services.AddScoped<IUserRepository, UserRepository>();
#endregion

#endregion

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Maroik.WebAPI", Version = "v1" });

    OpenApiSecurityScheme securityScheme = new()
    {
        Name = "Maroik.WebAPI",
        Description = "Enter JWT Bearer token **_only_**",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer", // must be lower-case
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Id = JwtBearerDefaults.AuthenticationScheme,
            Type = ReferenceType.SecurityScheme
        }
    };
    c.AddSecurityDefinition(securityScheme.Reference.Id, securityScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
                    {securityScheme, []}
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policyBuilder => { _ = policyBuilder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader(); });
});

#region Hot Swap Cert
string ComputeFileHash(string filePath)
{
    using var sha256 = SHA256.Create();
    using var stream = new FileStream(
        filePath,
        FileMode.Open,
        FileAccess.Read,
        FileShare.ReadWrite,
        bufferSize: 8192,
        FileOptions.SequentialScan);

    var hashBytes = sha256.ComputeHash(stream);
    return Convert.ToHexString(hashBytes); // .NET 5+
}
X509Certificate2 LoadCertificate(string certPath, string keyPath)
{
    try
    {
        var realCertPem = "";
        var realKeyPem = "";
        
        // Read certificate and key as string (pem format)
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var linkCertPem = File.ReadAllText(certPath);
            var linkKeyPem = File.ReadAllText(keyPath);
            
            var realCertName = Path.GetFileName(linkCertPem);
            var realKeyName = Path.GetFileName(linkKeyPem);

            var realCertPath = certPath.Replace("/live/", "/archive/").Replace("cert.pem", realCertName);
            var realKeyPath = keyPath.Replace("/live/", "/archive/").Replace("privkey.pem", realKeyName);

            realCertPem = File.ReadAllText(realCertPath);
            realKeyPem = File.ReadAllText(realKeyPath);
            
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            realCertPem = File.ReadAllText(certPath);
            realKeyPem = File.ReadAllText(keyPath);
        }
        
        // Create the certificate using X509Certificate2.CreateFromPem
        var cert = X509Certificate2.CreateFromPem(realCertPem);

        // Load private key from PEM manually
        using var ecdsaPrivateKey = ECDsa.Create();
        ecdsaPrivateKey.ImportFromPem(realKeyPem);

        // Combine certificate plus private key
        var certWithKey = cert.CopyWithPrivateKey(ecdsaPrivateKey);

        return certWithKey;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to load certificate from {certPath} and {keyPath}: {ex.Message}");
        throw;
    }
}

if (!string.IsNullOrEmpty(ServerSetting.DockerCertPath) && !string.IsNullOrEmpty(ServerSetting.DockerKeyPath))
{
    // Load certificate
    currentCert = LoadCertificate(ServerSetting.DockerCertPath, ServerSetting.DockerKeyPath);
    previousCertHash = ComputeFileHash(ServerSetting.DockerCertPath);
    previousCertKeyHash = ComputeFileHash(ServerSetting.DockerKeyPath);

    // Configure Kestrel SSL settings
    builder.WebHost.ConfigureKestrel(serverOptions =>
    {
        serverOptions.ConfigureEndpointDefaults(_ =>
        {
            serverOptions.Limits.MinRequestBodyDataRate = new MinDataRate(100, TimeSpan.FromSeconds(10));
            serverOptions.Limits.MinResponseDataRate = new MinDataRate(100, TimeSpan.FromSeconds(10));
            serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
            serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(1);
            // serverOptions.ConfigureHttpsDefaults(options =>
            // {
            //     options.SslProtocols = SslProtocols.Tls12;
            //     options.ServerCertificateSelector = (_, _) =>
            //     {
            //         lock (certLock)
            //         {
            //             return currentCert!;
            //         }
            //     };
            // });
        });
    });

    certTimer = new System.Timers.Timer(TimeSpan.FromHours(1).TotalMilliseconds); // for debug [TimeSpan.FromSeconds(5)]
    certTimer.AutoReset = true;

    certTimer.Elapsed += (_, _) =>
    {
        if (isReloading) return;
        isReloading = true;

        try
        {
            var currentCertHash = ComputeFileHash(ServerSetting.DockerCertPath);
            var currentCertKeyHash = ComputeFileHash(ServerSetting.DockerKeyPath);
            // ReSharper disable once InvertIf
            if (currentCertHash != previousCertHash &&
                currentCertKeyHash != previousCertKeyHash)
            {
                Console.WriteLine("Certificate file, reloading...");

                var newCert = LoadCertificate(ServerSetting.DockerCertPath, ServerSetting.DockerKeyPath);

                lock (certLock)
                {
                    currentCert?.Dispose();
                    currentCert = newCert;
                    previousCertHash = currentCertHash;
                    previousCertKeyHash = currentCertKeyHash;
                }
                Console.WriteLine("Certificate reloaded successfully.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to reload certificate: {ex.Message}");
        }
        finally
        {
            isReloading = false;
        }
    };

    certTimer.Start();
    Console.WriteLine("Certificate polling started.");
}
else
{
    builder.WebHost.ConfigureKestrel(serverOptions =>
    {
        serverOptions.ConfigureEndpointDefaults(_ =>
        {
            serverOptions.Limits.MinRequestBodyDataRate = new MinDataRate(100, TimeSpan.FromSeconds(10));
            serverOptions.Limits.MinResponseDataRate = new MinDataRate(100, TimeSpan.FromSeconds(10));
            serverOptions.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
            serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(1);
            // serverOptions.ConfigureHttpsDefaults(options =>
            // {
            //     options.SslProtocols = SslProtocols.Tls12;
            // });
        });
    });
}
#endregion

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    _ = app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("./swagger/v1/swagger.json", "Maroik.WebAPI");
    c.DocumentTitle = "Maroik.WebAPI";
    c.RoutePrefix = string.Empty;
});

app.UseRouting();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();