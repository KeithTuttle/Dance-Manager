using DanceManager.Api.Data;
using DanceManager.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DanceManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProgressionController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProgressionController(AppDbContext db) => _db = db;

    public record ProgressionCell(int StudentId, int MilestoneId, ProgressStatus Status);

    public record ProgressionMatrix(
        List<Milestone> Milestones,
        List<Student> Students,
        List<ProgressionCell> Statuses);

    // GET /api/progression?classId=
    // Returns the matrix: milestones (columns), students (rows) and their statuses (cells).
    [HttpGet]
    public async Task<ActionResult<ProgressionMatrix>> Get([FromQuery] int classId)
    {
        var cls = await _db.FindScopedAsync<DanceClass>(classId);
        if (cls is null) return NotFound();

        // Milestones for this class: studio-wide plus class-scoped.
        var milestones = await _db.Milestones
            .Where(m => m.StudioId == cls.StudioId && (m.ClassId == null || m.ClassId == classId))
            .OrderBy(m => m.Name)
            .ToListAsync();

        // Students enrolled in this class (the class roster).
        var students = await _db.Students
            .Where(s => _db.Enrollments.Any(e => e.StudentId == s.Id && e.ClassId == classId))
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .ToListAsync();

        var milestoneIds = milestones.Select(m => m.Id).ToList();
        var studentIds = students.Select(s => s.Id).ToList();

        var statuses = await _db.StudentMilestoneStatuses
            .Where(x => milestoneIds.Contains(x.MilestoneId) && studentIds.Contains(x.StudentId))
            .Select(x => new ProgressionCell(x.StudentId, x.MilestoneId, x.Status))
            .ToListAsync();

        return new ProgressionMatrix(milestones, students, statuses);
    }

    public record UpsertCell(int StudentId, int MilestoneId, ProgressStatus Status);

    // PUT /api/progression — upsert a single matrix cell.
    [HttpPut]
    public async Task<IActionResult> Upsert(UpsertCell input)
    {
        var existing = await _db.StudentMilestoneStatuses
            .FirstOrDefaultAsync(x => x.StudentId == input.StudentId && x.MilestoneId == input.MilestoneId);

        if (existing is null)
        {
            _db.StudentMilestoneStatuses.Add(new StudentMilestoneStatus
            {
                StudentId = input.StudentId,
                MilestoneId = input.MilestoneId,
                Status = input.Status,
            });
        }
        else
        {
            existing.Status = input.Status;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }
}
