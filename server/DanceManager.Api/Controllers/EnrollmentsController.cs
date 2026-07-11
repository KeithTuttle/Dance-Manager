using DanceManager.Api.Data;
using DanceManager.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DanceManager.Api.Controllers;

/// <summary>Class rosters: which students are enrolled in which class.</summary>
[ApiController]
[Route("api/[controller]")]
public class EnrollmentsController : ControllerBase
{
    private readonly AppDbContext _db;

    public EnrollmentsController(AppDbContext db) => _db = db;

    public record EnrollRequest(int StudentId, int ClassId);

    // GET /api/enrollments?classId=  (or ?studioId= for all rosters in a studio)
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Enrollment>>> GetAll(
        [FromQuery] int? classId, [FromQuery] int? studioId)
    {
        var query = _db.Enrollments.AsQueryable();
        if (classId is not null)
            query = query.Where(e => e.ClassId == classId);
        if (studioId is not null)
            query = query.Where(e => e.Class != null && e.Class.StudioId == studioId);
        return await query.ToListAsync();
    }

    // POST /api/enrollments — enroll a student in a class (idempotent).
    [HttpPost]
    public async Task<ActionResult<Enrollment>> Enroll(EnrollRequest input)
    {
        var existing = await _db.Enrollments
            .FirstOrDefaultAsync(e => e.StudentId == input.StudentId && e.ClassId == input.ClassId);
        if (existing is not null) return Ok(existing);

        var enrollment = new Enrollment { StudentId = input.StudentId, ClassId = input.ClassId };
        _db.Enrollments.Add(enrollment);
        await _db.SaveChangesAsync();
        return Ok(enrollment);
    }

    // DELETE /api/enrollments?studentId=&classId= — unenroll (remove from the class roster).
    [HttpDelete]
    public async Task<IActionResult> Unenroll([FromQuery] int studentId, [FromQuery] int classId)
    {
        // Tenant-filtered lookup: an out-of-tenant row simply isn't found.
        var enrollment = await _db.Enrollments
            .FirstOrDefaultAsync(e => e.StudentId == studentId && e.ClassId == classId);
        if (enrollment is null) return NotFound();

        _db.Enrollments.Remove(enrollment);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
