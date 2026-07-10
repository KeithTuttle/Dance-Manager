using DanceManager.Api.Data;
using DanceManager.Api.Services;
using Microsoft.EntityFrameworkCore;

// QuestPDF Community license (required, set once at startup).
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

const string CorsPolicy = "spa";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// PDF generation for the Sub Handoff feature (Unit 6).
builder.Services.AddScoped<SubHandoffPdfService>();

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        // Serialize enums as strings in API payloads to match the DB storage.
        o.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHostedService<DanceManager.Api.Data.DevSeeder>();

builder.Services.AddCors(options =>
    options.AddPolicy(CorsPolicy, policy => policy
        .WithOrigins("http://localhost:5199")
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(CorsPolicy);
app.UseAuthorization();
app.MapControllers();

app.Run();
