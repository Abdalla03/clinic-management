using System.Security.Claims;
using System.Text;
using ClinicManagement.Api.Authorization;
using ClinicManagement.Api.Infrastructure;
using ClinicManagement.Api.Controllers;
using ClinicManagement.Application.Auth;
using ClinicManagement.Domain.Users;
using ClinicManagement.Infrastructure.DependencyInjection;
using ClinicManagement.Infrastructure.Identity;
using ClinicManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
    .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "ClinicManagement.Api")
    .ReadFrom.Configuration(context.Configuration)
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(context.Configuration["LOG_PATH"] ?? "logs", "clinic-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 90,
        shared: true));

builder.Services.AddClinicPersistence(builder.Configuration);
builder.Services.AddClinicIdentity();
builder.Services.AddClinicValidators();
builder.Services.AddClinicApplicationServices();
builder.Services.AddClinicSecurity(builder.Configuration, builder.Environment);
builder.Services.AddClinicRateLimiter(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddProblemDetails();
builder.Services.AddControllers();

// In production the API sits behind a TLS-terminating reverse proxy (Caddy). Trust the
// X-Forwarded-* headers it injects so Request.IsHttps is true and Hsts/UseHttpsRedirection
// do not loop. The proxy is the only reachable peer on the internal compose network, so
// trusting all forwarders here is safe; the API port is never published in prod.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey ?? string.Empty)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(jwtOptions.ClockSkewSeconds),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],

            // The issuer writes ClaimTypes.* verbatim (MapInboundClaims is disabled), so the identity
            // must be told to read the same types or role checks silently never match.
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };
    });

builder.Services.AddSingleton<IAuthorizationHandler, MfaSatisfiedHandler>();

// AddIdentity registers cookie defaults; this API authenticates with bearer tokens only.
builder.Services.Configure<AuthenticationOptions>(options =>
{
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.AuthenticatedUser, builder => PolicyFactory.ApplyMfaPolicy(builder));
    options.AddPolicy(Policies.DoctorOnly, builder => PolicyFactory.ApplyMfaPolicy(builder, RoleNames.Doctor));
    options.AddPolicy(Policies.SecretaryOnly, builder => PolicyFactory.ApplyMfaPolicy(builder, RoleNames.Secretary));
    options.AddPolicy(Policies.PatientOnly, builder => PolicyFactory.ApplyMfaPolicy(builder, RoleNames.Patient));
    options.AddPolicy(Policies.ClinicalStaff, builder => PolicyFactory.ApplyMfaPolicy(builder, RoleNames.Doctor, RoleNames.Secretary));

    options.FallbackPolicy = PolicyFactory.MfaSatisfied();
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Length > 0)
        {
            policy.WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .WithExposedHeaders("WWW-Authenticate");
        }
    });
});

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressMapClientErrors = false;
});

if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Clinic Management API",
            Version = "v1",
            Description = "Clinic management system for doctors, secretaries and patients."
        });

        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Paste the access token only."
        });

        options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });

        options.OperationFilter<SwaggerRequestExamplesOperationFilter>();
        options.OperationFilter<SwaggerProblemDetailsOperationFilter>();
    });
}

var app = builder.Build();

app.UseForwardedHeaders();

app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("CorrelationId", httpContext.TraceIdentifier);
    };
});

app.UseExceptionHandler();
app.UseClinicSecurityHeaders();

if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors();
app.UseRateLimiter();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/", () => Results.Ok(new { service = "clinic-management-api", status = "up" }))
    .AllowAnonymous()
    .ExcludeFromDescription();

var seedDemoAccounts = app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing");
var seedDemoData = app.Environment.IsDevelopment();

// Development and Testing always bring the database up to date on boot so a fresh clone can sign in
// immediately without remembering to pass --migrate. Other environments only migrate when the
// operator explicitly asks for it, so a production process never performs a surprise schema change.
var runDatabaseInitializer =
    args.Contains("--migrate", StringComparer.OrdinalIgnoreCase) ||
    app.Environment.IsDevelopment() ||
    app.Environment.IsEnvironment("Testing");

if (runDatabaseInitializer)
{
    try
    {
        await DatabaseInitializer.InitializeAsync(app.Services, seedDemoAccounts, seedDemoData);
    }
    catch (Exception exception)
    {
        var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
        startupLogger.LogError(
            exception,
            "Database initialization failed while applying migrations/seeding. In Development this " +
            "usually means PostgreSQL is not reachable or the connection string is wrong; the API " +
            "will still start but sign-in and data endpoints will fail until it is fixed.");
    }
}

await app.RunAsync();

public partial class Program;
