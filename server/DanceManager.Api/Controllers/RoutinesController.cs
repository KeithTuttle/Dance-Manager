using DanceManager.Api.Data;
using DanceManager.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DanceManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RoutinesController : ControllerBase
{
    private readonly AppDbContext _db;

    public RoutinesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Routine>>> GetAll([FromQuery] int? classId)
    {
        var query = _db.Routines.AsQueryable();
        if (classId is not null)
            query = query.Where(r => r.ClassId == classId);
        return await query.OrderBy(r => r.SongTitle).ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Routine>> Get(int id)
    {
        var routine = await _db.Routines.FindAsync(id);
        return routine is null ? NotFound() : routine;
    }

    [HttpPost]
    public async Task<ActionResult<Routine>> Create(Routine routine)
    {
        _db.Routines.Add(routine);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = routine.Id }, routine);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Routine input)
    {
        if (id != input.Id) return BadRequest();
        _db.Entry(input).State = EntityState.Modified;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var routine = await _db.Routines.FindAsync(id);
        if (routine is null) return NotFound();
        _db.Routines.Remove(routine);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
