using System.Text.Json;
using System.Text.Json.Serialization;
using DanceManager.Api.Auth;
using DanceManager.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DanceManager.Api.Controllers;

/// <summary>
/// Full-tenant data export: one JSON file with every table the current tenant
/// owns, as a portable backup that isn't the database. No navigation includes —
/// rows reference each other by id, which keeps the file flat and cycle-free.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ExportController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenant _tenant;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }, // "Hourly", not 0
    };

    public ExportController(AppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    // GET /api/export — download everything in the current tenant as JSON.
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var tenantId = _tenant.TenantId ?? 0;
        var tenantName = await _db.Tenants
            .Where(t => t.Id == tenantId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync();

        // Every tenant-scoped DbSet is already filtered to the caller's tenant.
        var export = new
        {
            exportedAt = DateTimeOffset.UtcNow,
            tenantName,
            studios = await _db.Studios.AsNoTracking().ToListAsync(),
            classes = await _db.Classes.AsNoTracking().ToListAsync(),
            students = await _db.Students.AsNoTracking().ToListAsync(),
            studentNotes = await _db.StudentNotes.AsNoTracking().ToListAsync(),
            enrollments = await _db.Enrollments.AsNoTracking().ToListAsync(),
            attendanceRecords = await _db.AttendanceRecords.AsNoTracking().ToListAsync(),
            classSessions = await _db.ClassSessions.AsNoTracking().ToListAsync(),
            lessonPlanEntries = await _db.LessonPlanEntries.AsNoTracking().ToListAsync(),
            routines = await _db.Routines.AsNoTracking().ToListAsync(),
            formations = await _db.Formations.AsNoTracking().ToListAsync(),
            routineCasts = await _db.RoutineCasts.AsNoTracking().ToListAsync(),
            recitalParticipations = await _db.RecitalParticipations.AsNoTracking().ToListAsync(),
            showSections = await _db.ShowSections.AsNoTracking().ToListAsync(),
            showPrograms = await _db.ShowPrograms.AsNoTracking().ToListAsync(),
            costumeRecords = await _db.CostumeRecords.AsNoTracking().ToListAsync(),
            costumeOptions = await _db.CostumeOptions.AsNoTracking().ToListAsync(),
            songChoices = await _db.SongChoices.AsNoTracking().ToListAsync(),
            auditions = await _db.Auditions.AsNoTracking().ToListAsync(),
            auditionCandidates = await _db.AuditionCandidates.AsNoTracking().ToListAsync(),
            milestones = await _db.Milestones.AsNoTracking().ToListAsync(),
            studentMilestoneStatuses = await _db.StudentMilestoneStatuses.AsNoTracking().ToListAsync(),
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(export, JsonOpts);
        var fileName = $"dancemanager-export-{DateTime.UtcNow:yyyy-MM-dd}.json";
        return File(bytes, "application/json", fileName);
    }
}
