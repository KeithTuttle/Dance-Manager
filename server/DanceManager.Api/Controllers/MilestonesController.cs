using DanceManager.Api.Data;
using DanceManager.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DanceManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MilestonesController : ControllerBase
{
    private readonly AppDbContext _db;

    public MilestonesController(AppDbContext db) => _db = db;

    // GET /api/milestones?studioId=&classId=
    // Returns studio-wide milestones plus (when classId given) class-scoped ones.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Milestone>>> GetAll(
        [FromQuery] int? studioId, [FromQuery] int? classId)
    {
        var q = _db.Milestones.AsQueryable();
        if (studioId is not null) q = q.Where(m => m.StudioId == studioId);
        if (classId is not null) q = q.Where(m => m.ClassId == null || m.ClassId == classId);
        return await q.OrderBy(m => m.Name).ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Milestone>> Get(int id)
    {
        var milestone = await _db.FindScopedAsync<Milestone>(id);
        return milestone is null ? NotFound() : milestone;
    }

    [HttpPost]
    public async Task<ActionResult<Milestone>> Create(Milestone milestone)
    {
        _db.Milestones.Add(milestone);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = milestone.Id }, milestone);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Milestone input)
    {
        if (id != input.Id) return BadRequest();
        return await _db.UpdateScopedAsync(id, input) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var milestone = await _db.FindScopedAsync<Milestone>(id);
        if (milestone is null) return NotFound();
        _db.Milestones.Remove(milestone);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
