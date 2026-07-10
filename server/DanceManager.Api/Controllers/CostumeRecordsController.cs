using DanceManager.Api.Data;
using DanceManager.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DanceManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CostumeRecordsController : ControllerBase
{
    private readonly AppDbContext _db;

    public CostumeRecordsController(AppDbContext db) => _db = db;

    /// <summary>
    /// Returns costume records. Filterable by studio (via the student's studio)
    /// or by class (students participating in that class via RecitalParticipation).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CostumeRecord>>> GetAll(
        [FromQuery] int? classId,
        [FromQuery] int? studioId)
    {
        var query = _db.CostumeRecords.Include(c => c.Student).AsQueryable();

        if (classId is not null)
        {
            var studentIds = _db.RecitalParticipations
                .Where(p => p.ClassId == classId)
                .Select(p => p.StudentId);
            query = query.Where(c => studentIds.Contains(c.StudentId));
        }

        if (studioId is not null)
            query = query.Where(c => c.Student != null && c.Student.StudioId == studioId);

        return await query.ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CostumeRecord>> Get(int id)
    {
        var record = await _db.FindScopedAsync<CostumeRecord>(id);
        return record is null ? NotFound() : record;
    }

    [HttpPost]
    public async Task<ActionResult<CostumeRecord>> Create(CostumeRecord record)
    {
        _db.CostumeRecords.Add(record);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = record.Id }, record);
    }

    /// <summary>
    /// Full or single-field update. Supports inline cell edits from the ledger:
    /// the client sends the whole record with the changed field applied.
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CostumeRecord input)
    {
        if (id != input.Id) return BadRequest();
        return await _db.UpdateScopedAsync(id, input) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var record = await _db.FindScopedAsync<CostumeRecord>(id);
        if (record is null) return NotFound();
        _db.CostumeRecords.Remove(record);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
