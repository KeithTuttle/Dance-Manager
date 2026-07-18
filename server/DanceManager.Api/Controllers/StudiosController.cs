using DanceManager.Api.Data;
using DanceManager.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DanceManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StudiosController : ControllerBase
{
    private readonly AppDbContext _db;

    public StudiosController(AppDbContext db) => _db = db;

    // GET /api/studios — active studios only, unless ?includeArchived=true
    // (Settings uses that to manage archived seasons; pickers stay clean).
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Studio>>> GetAll([FromQuery] bool includeArchived = false)
    {
        var query = _db.Studios.AsQueryable();
        if (!includeArchived)
            query = query.Where(s => !s.IsArchived);
        return await query.OrderBy(s => s.Name).ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Studio>> Get(int id)
    {
        var studio = await _db.FindScopedAsync<Studio>(id);
        return studio is null ? NotFound() : studio;
    }

    [HttpPost]
    public async Task<ActionResult<Studio>> Create(Studio studio)
    {
        _db.Studios.Add(studio);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = studio.Id }, studio);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Studio input)
    {
        if (id != input.Id) return BadRequest();
        return await _db.UpdateScopedAsync(id, input) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var studio = await _db.FindScopedAsync<Studio>(id);
        if (studio is null) return NotFound();
        _db.Studios.Remove(studio);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
