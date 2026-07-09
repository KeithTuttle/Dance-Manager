using DanceManager.Api.Data;
using DanceManager.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DanceManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RecitalParticipationController : ControllerBase
{
    private readonly AppDbContext _db;

    public RecitalParticipationController(AppDbContext db) => _db = db;

    /// <summary>
    /// Returns participation rows. Filter by classId (participation for a class)
    /// or by studioId (participation for all classes in a studio). If neither is
    /// supplied, returns all rows.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<RecitalParticipation>>> GetAll(
        [FromQuery] int? classId, [FromQuery] int? studioId)
    {
        var query = _db.RecitalParticipations.AsQueryable();

        if (classId is not null)
            query = query.Where(rp => rp.ClassId == classId);

        if (studioId is not null)
            query = query.Where(rp => rp.Class != null && rp.Class.StudioId == studioId);

        return await query
            .OrderBy(rp => rp.ClassId).ThenBy(rp => rp.StudentId)
            .ToListAsync();
    }

    /// <summary>
    /// Upserts a participation row keyed on (StudentId, ClassId).
    /// </summary>
    [HttpPut]
    public async Task<ActionResult<RecitalParticipation>> Upsert(RecitalParticipation input)
    {
        var existing = await _db.RecitalParticipations
            .FindAsync(input.StudentId, input.ClassId);

        if (existing is null)
        {
            _db.RecitalParticipations.Add(input);
        }
        else
        {
            existing.IsParticipating = input.IsParticipating;
        }

        await _db.SaveChangesAsync();

        var result = existing ?? input;
        return Ok(result);
    }
}
