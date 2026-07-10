using DanceManager.Api.Data;
using DanceManager.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DanceManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CostumeOptionsController : ControllerBase
{
    private readonly AppDbContext _db;

    public CostumeOptionsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CostumeOption>>> GetAll([FromQuery] int? routineId)
    {
        var query = _db.CostumeOptions.AsQueryable();
        if (routineId is not null)
            query = query.Where(o => o.RoutineId == routineId);
        return await query.ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CostumeOption>> Get(int id)
    {
        var option = await _db.CostumeOptions.FindAsync(id);
        return option is null ? NotFound() : option;
    }

    [HttpPost]
    public async Task<ActionResult<CostumeOption>> Create(CostumeOption option)
    {
        _db.CostumeOptions.Add(option);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = option.Id }, option);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CostumeOption input)
    {
        if (id != input.Id) return BadRequest();
        _db.Entry(input).State = EntityState.Modified;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var option = await _db.CostumeOptions.FindAsync(id);
        if (option is null) return NotFound();
        _db.CostumeOptions.Remove(option);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
