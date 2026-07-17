using DanceManager.Api.Data;
using DanceManager.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DanceManager.Api.Controllers;

/// <summary>Per-number casting: which students are cast in which routine (number).</summary>
[ApiController]
[Route("api/[controller]")]
public class RoutineCastController : ControllerBase
{
    private readonly AppDbContext _db;

    public RoutineCastController(AppDbContext db) => _db = db;

    public record CastRequest(int RoutineId, int StudentId);

    // GET /api/routinecast?routineId=  (a number's cast)
    // GET /api/routinecast?studioId=   (all casts in a production, via routine -> class -> studio)
    [HttpGet]
    public async Task<ActionResult<IEnumerable<RoutineCast>>> GetAll(
        [FromQuery] int? routineId, [FromQuery] int? studioId)
    {
        var query = _db.RoutineCasts.AsQueryable();
        if (routineId is not null)
            query = query.Where(c => c.RoutineId == routineId);
        if (studioId is not null)
            query = query.Where(c => c.Routine != null && c.Routine.Class != null
                && c.Routine.Class.StudioId == studioId);
        return await query.ToListAsync();
    }

    // POST /api/routinecast — cast a student in a routine (idempotent).
    [HttpPost]
    public async Task<ActionResult<RoutineCast>> Add(CastRequest input)
    {
        var existing = await _db.RoutineCasts
            .FirstOrDefaultAsync(c => c.RoutineId == input.RoutineId && c.StudentId == input.StudentId);
        if (existing is not null) return Ok(existing);

        var cast = new RoutineCast { RoutineId = input.RoutineId, StudentId = input.StudentId };
        _db.RoutineCasts.Add(cast);
        await _db.SaveChangesAsync();
        return Ok(cast);
    }

    // DELETE /api/routinecast?routineId=&studentId= — remove a student from a number's cast.
    [HttpDelete]
    public async Task<IActionResult> Remove([FromQuery] int routineId, [FromQuery] int studentId)
    {
        // Tenant-filtered lookup: an out-of-tenant row simply isn't found.
        var cast = await _db.RoutineCasts
            .FirstOrDefaultAsync(c => c.RoutineId == routineId && c.StudentId == studentId);
        if (cast is null) return NotFound();

        _db.RoutineCasts.Remove(cast);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
