using DanceManager.Api.Data;
using DanceManager.Api.Models;
using DanceManager.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DanceManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CostumeOptionsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly CostumePdfService _pdf;
    private readonly ImageFetchService _images;

    public CostumeOptionsController(AppDbContext db, CostumePdfService pdf, ImageFetchService images)
    {
        _db = db;
        _pdf = pdf;
        _images = images;
    }

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
        var option = await _db.FindScopedAsync<CostumeOption>(id);
        return option is null ? NotFound() : option;
    }

    // GET /api/costumeoptions/pdf?routineId= — printable costume sheet for a routine.
    [HttpGet("pdf")]
    public async Task<IActionResult> Pdf([FromQuery] int routineId)
    {
        var routine = await _db.Routines.FirstOrDefaultAsync(r => r.Id == routineId);
        if (routine is null) return NotFound("Routine not found.");

        var danceClass = await _db.Classes.FirstOrDefaultAsync(c => c.Id == routine.ClassId);
        var studioName = danceClass is null ? string.Empty : await _db.Studios
            .Where(s => s.Id == danceClass.StudioId)
            .Select(s => s.Name)
            .FirstOrDefaultAsync() ?? string.Empty;

        var options = await _db.CostumeOptions
            .Where(o => o.RoutineId == routineId)
            .OrderBy(o => o.Id)
            .ToListAsync();

        var title = string.IsNullOrWhiteSpace(routine.Artist)
            ? routine.SongTitle
            : $"{routine.SongTitle} — {routine.Artist}";

        // Fetch each option's photo (guarded) concurrently; skip failures — the
        // service falls back to printing the link for options without bytes.
        var withPhotos = options.Where(o => !string.IsNullOrWhiteSpace(o.PhotoLink)).ToList();
        var fetched = await Task.WhenAll(withPhotos.Select(async o =>
            (o.Id, Bytes: await _images.TryFetchAsync(o.PhotoLink, HttpContext.RequestAborted))));
        var photoBytes = fetched
            .Where(f => f.Bytes is not null)
            .ToDictionary(f => f.Id, f => f.Bytes!);

        var bytes = _pdf.Build(new CostumeSheetData
        {
            StudioName = studioName,
            ClassName = danceClass?.Name ?? string.Empty,
            RoutineTitle = title,
            BoysOptions = options.Where(o => o.Gender == Gender.Boys).ToList(),
            GirlsOptions = options.Where(o => o.Gender == Gender.Girls).ToList(),
            PhotoBytesByOptionId = photoBytes,
        });

        return File(bytes, "application/pdf", "costume-sheet.pdf");
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
        return await _db.UpdateScopedAsync(id, input) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var option = await _db.FindScopedAsync<CostumeOption>(id);
        if (option is null) return NotFound();
        _db.CostumeOptions.Remove(option);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
