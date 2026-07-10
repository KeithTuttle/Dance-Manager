using DanceManager.Api.Data;
using DanceManager.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DanceManager.Api.Controllers;

/// <summary>Payload for upserting Markdown class notes for a class on a date.</summary>
public record ClassSessionUpsertDto(int ClassId, DateOnly Date, string? Notes);

[ApiController]
[Route("api/[controller]")]
public class ClassSessionsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ClassSessionsController(AppDbContext db) => _db = db;

    // GET /api/classsessions?classId=&date=
    // Returns the session (with Markdown Notes) for a class on a date, or 204 if none.
    [HttpGet]
    public async Task<ActionResult<ClassSession>> Get(
        [FromQuery] int classId,
        [FromQuery] DateOnly date)
    {
        var session = await _db.ClassSessions
            .FirstOrDefaultAsync(s => s.ClassId == classId && s.Date == date);
        return session is null ? NoContent() : session;
    }

    // PUT /api/classsessions — upsert notes for a class+date (unique on ClassId, Date).
    [HttpPut]
    public async Task<ActionResult<ClassSession>> Upsert([FromBody] ClassSessionUpsertDto input)
    {
        var session = await _db.ClassSessions
            .FirstOrDefaultAsync(s => s.ClassId == input.ClassId && s.Date == input.Date);

        if (session is null)
        {
            session = new ClassSession
            {
                ClassId = input.ClassId,
                Date = input.Date,
                Notes = input.Notes,
            };
            _db.ClassSessions.Add(session);
        }
        else
        {
            session.Notes = input.Notes;
        }

        await _db.SaveChangesAsync();
        return session;
    }
}
