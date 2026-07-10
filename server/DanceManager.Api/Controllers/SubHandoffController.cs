using DanceManager.Api.Data;
using DanceManager.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DanceManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubHandoffController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly SubHandoffPdfService _pdf;

    public SubHandoffController(AppDbContext db, SubHandoffPdfService pdf)
    {
        _db = db;
        _pdf = pdf;
    }

    /// <summary>
    /// Generates a Sub Handoff PDF for a class (roster, latest class notes,
    /// routine music links, and current formation snapshots).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int? classId, [FromQuery] int? routineId)
    {
        // Resolve the class from either classId or a routineId.
        int? resolvedClassId = classId;
        if (resolvedClassId is null && routineId is not null)
        {
            resolvedClassId = await _db.Routines
                .Where(r => r.Id == routineId)
                .Select(r => (int?)r.ClassId)
                .FirstOrDefaultAsync();
        }

        if (resolvedClassId is null)
            return BadRequest("classId or routineId is required.");

        var danceClass = await _db.Classes.FirstOrDefaultAsync(c => c.Id == resolvedClassId);
        if (danceClass is null) return NotFound("Class not found.");

        var studioName = await _db.Studios
            .Where(s => s.Id == danceClass.StudioId)
            .Select(s => s.Name)
            .FirstOrDefaultAsync() ?? string.Empty;

        // Roster = participating students for this class.
        var participatingIds = await _db.RecitalParticipations
            .Where(p => p.ClassId == resolvedClassId && p.IsParticipating)
            .Select(p => p.StudentId)
            .ToListAsync();

        var roster = await _db.Students
            .Where(s => participatingIds.Contains(s.Id))
            .OrderBy(s => s.FirstName).ThenBy(s => s.LastName)
            .ToListAsync();

        var latestSession = await _db.ClassSessions
            .Where(cs => cs.ClassId == resolvedClassId)
            .OrderByDescending(cs => cs.Date)
            .FirstOrDefaultAsync();

        var routines = await _db.Routines
            .Where(r => r.ClassId == resolvedClassId)
            .OrderBy(r => r.SongTitle)
            .ToListAsync();

        var routineIds = routines.Select(r => r.Id).ToList();
        var formations = await _db.Formations
            .Where(f => routineIds.Contains(f.RoutineId))
            .OrderBy(f => f.OrderIndex)
            .ToListAsync();

        var data = new SubHandoffData
        {
            StudioName = studioName,
            ClassName = danceClass.Name,
            Roster = roster,
            LatestSession = latestSession,
            Routines = routines,
            FormationsByRoutine = formations
                .GroupBy(f => f.RoutineId)
                .ToDictionary(g => g.Key, g => g.OrderBy(f => f.OrderIndex).ToList()),
        };

        var bytes = _pdf.Build(data);
        return File(bytes, "application/pdf", "sub-handoff.pdf");
    }
}
