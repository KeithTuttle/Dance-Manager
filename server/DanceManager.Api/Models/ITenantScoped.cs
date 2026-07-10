namespace DanceManager.Api.Models;

/// <summary>
/// Marker for entities that belong to a single tenant. Every implementing entity
/// carries a <see cref="TenantId"/>; the DbContext applies a global query filter
/// on it (reads) and stamps it on insert (writes), so cross-tenant access is
/// impossible even if an individual query forgets to filter.
/// </summary>
public interface ITenantScoped
{
    int TenantId { get; set; }
}
