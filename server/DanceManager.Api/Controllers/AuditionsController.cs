using DanceManager.Api.Data;
using DanceManager.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DanceManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuditionsController : ControllerBase
{
    private readonly AppDbContext _db;

    public AuditionsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Audition>>> GetAll() =>
        await _db.Auditions.OrderByDescending(a => a.Date).ThenBy(a => a.Title).ToListAsync();

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Audition>> Get(int id)
    {
        var audition = await _db.FindScopedAsync<Audition>(id);
        return audition is null ? NotFound() : audition;
    }

    [HttpPost]
    public async Task<ActionResult<Audition>> Create(Audition audition)
    {
        _db.Auditions.Add(audition);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = audition.Id }, audition);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Audition input)
    {
        if (id != input.Id) return BadRequest();
        return await _db.UpdateScopedAsync(id, input) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var audition = await _db.FindScopedAsync<Audition>(id);
        if (audition is null) return NotFound();
        _db.Auditions.Remove(audition);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
