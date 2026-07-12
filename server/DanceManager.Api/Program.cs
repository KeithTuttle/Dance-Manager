using DanceManager.Api.Auth;
using DanceManager.Api.Data;
using DanceManager.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

// QuestPDF Community license (required, set once at startup).
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Load user-secrets (the local Supabase connection string) explicitly by id.
// NOT gated on the environment: VS Code's `dotnet run` can default to the
// Production environment (when it doesn't apply launchSettings), which would
// otherwise skip user-secrets and silently fall back to the localhost
// placeholder. On a real deployment the secrets file doesn't exist, so this is
// a harmless no-op there and the platform's ConnectionStrings__Default env var
// is used instead.
builder.Configuration.AddUserSecrets("f98fb171-b667-400e-968f-b9058a318e6e");

// Belt-and-braces: user-secrets live under %APPDATA%, and at least one launcher
// (VS Code's task runner) spawns `dotnet run` with an environment where that
// path doesn't resolve, so the secret silently fails to load. This gitignored
// file sits next to the app, so it loads no matter who spawned the process.
// It intentionally comes AFTER AddUserSecrets so it wins when both exist.
builder.Configuration.AddJsonFile("appsettings.Development.local.json", optional: true, reloadOnChange: false);

const string CorsPolicy = "spa";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// Per-request tenant, resolved by TenantResolutionMiddleware, read by AppDbContext.
builder.Services.AddScoped<ICurrentTenant, CurrentTenant>();

// PDF generation for the Sub Handoff feature (Unit 6).
builder.Services.AddScoped<SubHandoffPdfService>();
// PDF generation for the printable costume sheet.
builder.Services.AddScoped<CostumePdfService>();

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        // Serialize enums as strings in API payloads to match the DB storage.
        o.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHostedService<DevSeeder>();

// --- Authentication (Clerk, via JWT bearer) ---
// Enabled only when Clerk:Authority is configured. Until then the API boots
// unauthenticated (dev convenience) — a startup warning is logged.
var clerkAuthority = builder.Configuration["Clerk:Authority"];
var authEnabled = !string.IsNullOrWhiteSpace(clerkAuthority);

if (authEnabled)
{
    var authorizedParty = builder.Configuration["Clerk:AuthorizedParty"];

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = clerkAuthority;
            // Keep the raw Clerk claim names (notably `sub`) instead of remapping.
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = clerkAuthority,
                // Clerk session tokens have no fixed audience; we validate the
                // authorized party (azp) below instead.
                ValidateAudience = false,
                ValidateLifetime = true,
                NameClaimType = "sub",
            };

            if (!string.IsNullOrWhiteSpace(authorizedParty))
            {
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = ctx =>
                    {
                        var azp = ctx.Principal?.FindFirst("azp")?.Value;
                        if (!string.IsNullOrEmpty(azp) && azp != authorizedParty)
                            ctx.Fail("Unauthorized party (azp mismatch).");
                        return Task.CompletedTask;
                    },
                };
            }
        });

    // Every endpoint requires an authenticated user unless it opts out
    // with [AllowAnonymous] (e.g. the health check).
    builder.Services.AddAuthorization(options =>
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
    });
}
else
{
    builder.Services.AddAuthorization();
}

builder.Services.AddCors(options =>
    options.AddPolicy(CorsPolicy, policy => policy
        .WithOrigins("http://localhost:5199")
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();

// Log which database we actually resolved (no secrets) so a stale build silently
// falling back to the localhost placeholder is obvious in the terminal instead of
// a wall of "connection refused" errors.
var dbHost = "unknown";
try
{
    dbHost = new Npgsql.NpgsqlConnectionStringBuilder(
        builder.Configuration.GetConnectionString("Default")).Host ?? "unknown";
}
catch { /* unparseable / missing */ }

if (dbHost is "localhost" or "127.0.0.1")
{
    app.Logger.LogWarning(
        "Database host is '{DbHost}' — this is the appsettings placeholder, meaning the Supabase " +
        "connection string from user-secrets did NOT load. You are likely running a STALE build: stop " +
        "any leftover 'DanceManager.Api' / 'dotnet run' processes and rebuild (dotnet build). ", dbHost);
}
else
{
    app.Logger.LogInformation(
        "Environment: {Env}; Database host: {DbHost}", app.Environment.EnvironmentName, dbHost);
}

if (!authEnabled)
{
    app.Logger.LogWarning(
        "Clerk:Authority is not configured — the API is running UNAUTHENTICATED and tenant " +
        "isolation is INACTIVE. Set Clerk:Authority (and the client's VITE_CLERK_PUBLISHABLE_KEY) to enable auth.");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(CorsPolicy);

if (authEnabled)
    app.UseAuthentication();

app.UseAuthorization();

// Resolve the caller's tenant (and auto-provision on first login) after auth.
app.UseMiddleware<TenantResolutionMiddleware>();

app.MapControllers();

app.Run();
