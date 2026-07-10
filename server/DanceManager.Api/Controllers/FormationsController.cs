using DanceManager.Api.Data;
using DanceManager.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DanceManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FormationsController : ControllerBase
{
    private readonly AppDbContext _db;

    public FormationsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Formation>>> GetAll([FromQuery] int? routineId)
    {
        var query = _db.Formations.AsQueryable();
        if (routineId is not null)
            query = query.Where(f => f.RoutineId == routineId);
        return await query.OrderBy(f => f.OrderIndex).ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Formation>> Get(int id)
    {
        var formation = await _db.Formations.FindAsync(id);
        return formation is null ? NotFound() : formation;
    }

    [HttpPost]
    public async Task<ActionResult<Formation>> Create(Formation formation)
    {
        _db.Formations.Add(formation);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = formation.Id }, formation);
    }

    /// <summary>
    /// Saves a formation's name, order, and StudentCoordinates JSON (node positions).
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Formation input)
    {
        if (id != input.Id) return BadRequest();
        _db.Entry(input).State = EntityState.Modified;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var formation = await _db.Formations.FindAsync(id);
        if (formation is null) return NotFound();
        _db.Formations.Remove(formation);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
