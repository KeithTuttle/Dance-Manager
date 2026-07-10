using DanceManager.Api.Data;
using DanceManager.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DanceManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StudentNotesController : ControllerBase
{
    private readonly AppDbContext _db;

    public StudentNotesController(AppDbContext db) => _db = db;

    // GET /api/studentnotes?studentId=  (newest first)
    [HttpGet]
    public async Task<ActionResult<IEnumerable<StudentNote>>> GetAll([FromQuery] int? studentId)
    {
        var q = _db.StudentNotes.AsQueryable();
        if (studentId is not null) q = q.Where(n => n.StudentId == studentId);
        return await q.OrderByDescending(n => n.CreatedAt).ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<StudentNote>> Get(int id)
    {
        var note = await _db.FindScopedAsync<StudentNote>(id);
        return note is null ? NotFound() : note;
    }

    [HttpPost]
    public async Task<ActionResult<StudentNote>> Create(StudentNote note)
    {
        note.CreatedAt = DateTimeOffset.UtcNow;
        _db.StudentNotes.Add(note);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = note.Id }, note);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var note = await _db.FindScopedAsync<StudentNote>(id);
        if (note is null) return NotFound();
        _db.StudentNotes.Remove(note);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
