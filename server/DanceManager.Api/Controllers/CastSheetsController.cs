using DanceManager.Api.Data;
using DanceManager.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DanceManager.Api.Controllers;

/// <summary>Printable cast sheets for a musical production (cast list / dancer schedules).</summary>
[ApiController]
[Route("api/[controller]")]
public class CastSheetsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly CastSheetPdfService _pdf;

    public CastSheetsController(AppDbContext db, CastSheetPdfService pdf)
    {
        _db = db;
        _pdf = pdf;
    }

    // GET /api/castsheets?studioId=&mode=number|dancer
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int studioId, [FromQuery] string mode = "number")
    {
        var studioName = await _db.Studios
            .Where(s => s.Id == studioId)
            .Select(s => s.Name)
            .FirstOrDefaultAsync() ?? string.Empty;

        var classes = await _db.Classes.Where(c => c.StudioId == studioId).ToListAsync();
        var classIds = classes.Select(c => c.Id).ToList();

        var routines = await _db.Routines.Where(r => classIds.Contains(r.ClassId)).ToListAsync();
        var casts = await _db.RoutineCasts
            .Where(c => c.Routine != null && c.Routine.Class != null && c.Routine.Class.StudioId == studioId)
            .ToListAsync();
        var participations = await _db.RecitalParticipations
            .Where(p => classIds.Contains(p.ClassId))
            .ToListAsync();
        var enrollments = await _db.Enrollments
            .Where(e => classIds.Contains(e.ClassId))
            .ToListAsync();
        var students = await _db.Students.Where(s => s.StudioId == studioId).ToListAsync();
        var program = await _db.ShowPrograms
            .Where(p => p.StudioId == studioId)
            .OrderBy(p => p.OrderPosition)
            .ToListAsync();
        var sections = await _db.ShowSections
            .Where(s => s.StudioId == studioId)
            .OrderBy(s => s.OrderIndex)
            .ToListAsync();

        var data = new CastSheetData
        {
            StudioName = studioName,
            Sections = sections,
            Program = program,
            Routines = routines,
            Classes = classes,
            RoutineCasts = casts,
            Participations = participations,
            Enrollments = enrollments,
            Students = students,
        };

        var byDancer = string.Equals(mode, "dancer", StringComparison.OrdinalIgnoreCase);
        var bytes = byDancer ? _pdf.BuildByDancer(data) : _pdf.BuildByNumber(data);
        var fileName = byDancer ? "dancer-schedules.pdf" : "cast-list.pdf";
        return File(bytes, "application/pdf", fileName);
    }
}
