using DanceManager.Api.Data;
using DanceManager.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DanceManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SongChoicesController : ControllerBase
{
    private readonly AppDbContext _db;

    public SongChoicesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SongChoice>>> GetAll([FromQuery] int? routineId)
    {
        var query = _db.SongChoices.AsQueryable();
        if (routineId is not null)
            query = query.Where(s => s.RoutineId == routineId);
        return await query.ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SongChoice>> Get(int id)
    {
        var choice = await _db.SongChoices.FindAsync(id);
        return choice is null ? NotFound() : choice;
    }

    [HttpPost]
    public async Task<ActionResult<SongChoice>> Create(SongChoice choice)
    {
        _db.SongChoices.Add(choice);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = choice.Id }, choice);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, SongChoice input)
    {
        if (id != input.Id) return BadRequest();
        _db.Entry(input).State = EntityState.Modified;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var choice = await _db.SongChoices.FindAsync(id);
        if (choice is null) return NotFound();
        _db.SongChoices.Remove(choice);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
