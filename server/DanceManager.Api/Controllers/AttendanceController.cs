using DanceManager.Api.Data;
using DanceManager.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DanceManager.Api.Controllers;

/// <summary>Payload for a single attendance mark in a bulk upsert.</summary>
public record AttendanceUpsertDto(int StudentId, int ClassId, DateOnly Date, AttendanceStatus Status);

/// <summary>
/// Rolling 4-week (last 28 days) attendance summary per student for a class.
/// Rate is the fraction of recorded sessions where the student was Present.
/// </summary>
public record AttendanceSummaryDto(int StudentId, int Present, int Total, double Rate);

[ApiController]
[Route("api/[controller]")]
public class AttendanceController : ControllerBase
{
    private readonly AppDbContext _db;

    public AttendanceController(AppDbContext db) => _db = db;

    // GET /api/attendance?classId=&date=
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AttendanceRecord>>> Get(
        [FromQuery] int classId,
        [FromQuery] DateOnly date)
    {
        return await _db.AttendanceRecords
            .Where(a => a.ClassId == classId && a.Date == date)
            .ToListAsync();
    }

    // PUT /api/attendance — bulk upsert respecting the unique (StudentId, ClassId, Date) index.
    [HttpPut]
    public async Task<IActionResult> BulkUpsert([FromBody] List<AttendanceUpsertDto> records)
    {
        if (records is null || records.Count == 0) return NoContent();

        // Load existing records that overlap the incoming keys so we update in place.
        var classIds = records.Select(r => r.ClassId).Distinct().ToList();
        var dates = records.Select(r => r.Date).Distinct().ToList();
        var studentIds = records.Select(r => r.StudentId).Distinct().ToList();

        var existing = await _db.AttendanceRecords
            .Where(a => classIds.Contains(a.ClassId)
                && dates.Contains(a.Date)
                && studentIds.Contains(a.StudentId))
            .ToListAsync();

        var lookup = existing.ToDictionary(a => (a.StudentId, a.ClassId, a.Date));

        foreach (var r in records)
        {
            if (lookup.TryGetValue((r.StudentId, r.ClassId, r.Date), out var found))
            {
                found.Status = r.Status;
            }
            else
            {
                _db.AttendanceRecords.Add(new AttendanceRecord
                {
                    StudentId = r.StudentId,
                    ClassId = r.ClassId,
                    Date = r.Date,
                    Status = r.Status,
                });
            }
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // GET /api/attendance/summary?classId=
    // Per-student attendance rate over the last 28 days for flagging < 75%.
    [HttpGet("summary")]
    public async Task<ActionResult<IEnumerable<AttendanceSummaryDto>>> Summary(
        [FromQuery] int classId)
    {
        var since = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-28));

        var rows = await _db.AttendanceRecords
            .Where(a => a.ClassId == classId && a.Date >= since)
            .GroupBy(a => a.StudentId)
            .Select(g => new AttendanceSummaryDto(
                g.Key,
                g.Count(a => a.Status == AttendanceStatus.Present),
                g.Count(),
                g.Count() == 0
                    ? 0
                    : (double)g.Count(a => a.Status == AttendanceStatus.Present) / g.Count()))
            .ToListAsync();

        return rows;
    }
}
