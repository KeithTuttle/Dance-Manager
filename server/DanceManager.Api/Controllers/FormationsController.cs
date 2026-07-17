using DanceManager.Api.Data;
using DanceManager.Api.Models;
using DanceManager.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DanceManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FormationsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly FormationAiService _ai;

    public FormationsController(AppDbContext db, FormationAiService ai)
    {
        _db = db;
        _ai = ai;
    }

    public record SuggestRequest(List<FormationDancer> Dancers, string? Description);

    // POST /api/formations/suggest — AI-suggest positions for a set of dancers.
    // Stateless: returns a {studentId: {x,y}} map the client applies + saves via PUT.
    [HttpPost("suggest")]
    public async Task<IActionResult> Suggest(SuggestRequest input)
    {
        var dancers = input.Dancers ?? new List<FormationDancer>();
        var result = await _ai.SuggestAsync(dancers, input.Description, HttpContext.RequestAborted);
        return Ok(new
        {
            configured = result.Configured,
            ok = result.Ok,
            coordinates = result.Coordinates, // keyed by studentId -> {x,y}
        });
    }

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
        var formation = await _db.FindScopedAsync<Formation>(id);
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
        return await _db.UpdateScopedAsync(id, input) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var formation = await _db.FindScopedAsync<Formation>(id);
        if (formation is null) return NotFound();
        _db.Formations.Remove(formation);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
