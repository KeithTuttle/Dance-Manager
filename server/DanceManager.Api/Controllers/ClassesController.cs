using DanceManager.Api.Data;
using DanceManager.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DanceManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClassesController : ControllerBase
{
    private readonly AppDbContext _db;

    public ClassesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<DanceClass>>> GetAll([FromQuery] int? studioId)
    {
        var query = _db.Classes.AsQueryable();
        if (studioId is not null)
            query = query.Where(c => c.StudioId == studioId);
        return await query.OrderBy(c => c.Name).ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DanceClass>> Get(int id)
    {
        var danceClass = await _db.FindScopedAsync<DanceClass>(id);
        return danceClass is null ? NotFound() : danceClass;
    }

    [HttpPost]
    public async Task<ActionResult<DanceClass>> Create(DanceClass danceClass)
    {
        _db.Classes.Add(danceClass);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = danceClass.Id }, danceClass);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, DanceClass input)
    {
        if (id != input.Id) return BadRequest();
        return await _db.UpdateScopedAsync(id, input) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var danceClass = await _db.FindScopedAsync<DanceClass>(id);
        if (danceClass is null) return NotFound();
        _db.Classes.Remove(danceClass);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
