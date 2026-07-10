using DanceManager.Api.Auth;
using DanceManager.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DanceManager.Api.Controllers;

/// <summary>Account-level settings for the current tenant (the studio-group itself).</summary>
public record SettingsResponse(int TenantId, string Name);
public record RenameTenantRequest(string Name);

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenant _tenant;

    public SettingsController(AppDbContext db, ICurrentTenant tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    // GET /api/settings — the current tenant's name.
    [HttpGet]
    public async Task<ActionResult<SettingsResponse>> Get()
    {
        var tenantId = _tenant.TenantId;
        if (tenantId is null) return NotFound();

        // Tenants is a global (non-tenant-scoped) table; filter explicitly by id.
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
        if (tenant is null) return NotFound();

        return new SettingsResponse(tenant.Id, tenant.Name);
    }

    // PUT /api/settings — rename the current tenant (studio-group).
    [HttpPut]
    public async Task<ActionResult<SettingsResponse>> Rename(RenameTenantRequest input)
    {
        var tenantId = _tenant.TenantId;
        if (tenantId is null) return NotFound();

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
        if (tenant is null) return NotFound();

        var name = input.Name?.Trim();
        if (string.IsNullOrEmpty(name)) return BadRequest("Name cannot be empty.");

        tenant.Name = name;
        await _db.SaveChangesAsync();
        return new SettingsResponse(tenant.Id, tenant.Name);
    }
}
