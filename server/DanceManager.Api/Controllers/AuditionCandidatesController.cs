using DanceManager.Api.Data;
using DanceManager.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DanceManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuditionCandidatesController : ControllerBase
{
    private readonly AppDbContext _db;

    public AuditionCandidatesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AuditionCandidate>>> GetAll([FromQuery] int? auditionId)
    {
        var query = _db.AuditionCandidates.AsQueryable();
        if (auditionId is not null)
            query = query.Where(c => c.AuditionId == auditionId);
        return await query.OrderBy(c => c.Id).ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AuditionCandidate>> Get(int id)
    {
        var candidate = await _db.FindScopedAsync<AuditionCandidate>(id);
        return candidate is null ? NotFound() : candidate;
    }

    [HttpPost]
    public async Task<ActionResult<AuditionCandidate>> Create(AuditionCandidate candidate)
    {
        _db.AuditionCandidates.Add(candidate);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = candidate.Id }, candidate);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, AuditionCandidate input)
    {
        if (id != input.Id) return BadRequest();
        return await _db.UpdateScopedAsync(id, input) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var candidate = await _db.FindScopedAsync<AuditionCandidate>(id);
        if (candidate is null) return NotFound();
        _db.AuditionCandidates.Remove(candidate);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
