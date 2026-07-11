using DanceManager.Api.Data;
using DanceManager.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DanceManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StudentsController : ControllerBase
{
    private readonly AppDbContext _db;

    public StudentsController(AppDbContext db) => _db = db;

    // GET /api/students?studioId=  -> all students in a studio
    // GET /api/students?classId=   -> only students enrolled in that class (the class roster)
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Student>>> GetAll(
        [FromQuery] int? studioId, [FromQuery] int? classId)
    {
        var query = _db.Students.AsQueryable();
        if (studioId is not null)
            query = query.Where(s => s.StudioId == studioId);
        if (classId is not null)
            query = query.Where(s => _db.Enrollments.Any(e => e.StudentId == s.Id && e.ClassId == classId));
        return await query.OrderBy(s => s.LastName).ThenBy(s => s.FirstName).ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Student>> Get(int id)
    {
        var student = await _db.FindScopedAsync<Student>(id);
        return student is null ? NotFound() : student;
    }

    [HttpPost]
    public async Task<ActionResult<Student>> Create(Student student)
    {
        _db.Students.Add(student);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = student.Id }, student);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Student input)
    {
        if (id != input.Id) return BadRequest();
        return await _db.UpdateScopedAsync(id, input) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var student = await _db.FindScopedAsync<Student>(id);
        if (student is null) return NotFound();
        _db.Students.Remove(student);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
