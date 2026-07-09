using DanceManager.Api.Data;
using DanceManager.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DanceManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly AppDbContext _db;

    public AnalyticsController(AppDbContext db) => _db = db;

    /// <summary>
    /// Number of trailing months (including the current month) reported in the
    /// attendance trend series.
    /// </summary>
    private const int TrendMonths = 12;

    /// <summary>
    /// Assumed teaching hours per class session, used to estimate pay for
    /// studios billed <see cref="PayType.Hourly"/>. Sessions do not record a
    /// duration in the current schema, so a fixed, reasonable value is used and
    /// echoed back to the client as <see cref="AnalyticsResponse.HoursPerSession"/>.
    /// </summary>
    private const decimal AssumedHoursPerSession = 1.5m;

    /// <summary>
    /// Returns the monthly attendance trend and an estimated-pay summary for a
    /// single studio, filtered to that studio's classes.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<AnalyticsResponse>> Get([FromQuery] int studioId)
    {
        var studio = await _db.Studios.FindAsync(studioId);
        if (studio is null) return NotFound();

        // Class ids belonging to this studio; everything else is filtered by these.
        var classIds = await _db.Classes
            .Where(c => c.StudioId == studioId)
            .Select(c => c.Id)
            .ToListAsync();

        // Attendance for this studio's classes over the reporting window.
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var windowStart = new DateOnly(today.Year, today.Month, 1).AddMonths(-(TrendMonths - 1));

        var records = await _db.AttendanceRecords
            .Where(r => classIds.Contains(r.ClassId) && r.Date >= windowStart)
            .Select(r => new { r.Date, r.Status })
            .ToListAsync();

        // Bucket records by calendar month and compute the attendance rate
        // (present / total marked) plus the present-count for each month in the
        // window, producing a dense series even for months with no data.
        var byMonth = records
            .GroupBy(r => new { r.Date.Year, r.Date.Month })
            .ToDictionary(
                g => (g.Key.Year, g.Key.Month),
                g => new
                {
                    Total = g.Count(),
                    Present = g.Count(r => r.Status == AttendanceStatus.Present),
                });

        var trend = new List<MonthlyAttendancePoint>(TrendMonths);
        for (var i = 0; i < TrendMonths; i++)
        {
            var month = windowStart.AddMonths(i);
            var label = new DateTime(month.Year, month.Month, 1).ToString("MMM yyyy");
            byMonth.TryGetValue((month.Year, month.Month), out var stats);

            var total = stats?.Total ?? 0;
            var present = stats?.Present ?? 0;
            var rate = total > 0 ? Math.Round((decimal)present / total * 100m, 1) : 0m;

            trend.Add(new MonthlyAttendancePoint(label, present, total, rate));
        }

        // Total sessions scheduled for this studio's classes.
        var totalSessions = await _db.ClassSessions
            .CountAsync(s => classIds.Contains(s.ClassId));

        // Total present headcount across the whole history for this studio.
        var totalPresent = await _db.AttendanceRecords
            .CountAsync(r => classIds.Contains(r.ClassId) && r.Status == AttendanceStatus.Present);

        // Estimated pay from the studio's pay rules:
        //   Hourly       => sessions * assumed hours-per-session * rate.
        //   PerHeadcount => total present headcounts * rate.
        var estimatedPay = studio.PayType == PayType.Hourly
            ? totalSessions * AssumedHoursPerSession * studio.PayRate
            : totalPresent * studio.PayRate;

        // Average monthly attendance rate across months that actually had data.
        var monthsWithData = trend.Where(p => p.Total > 0).ToList();
        var avgAttendanceRate = monthsWithData.Count > 0
            ? Math.Round(monthsWithData.Average(p => p.AttendanceRate), 1)
            : 0m;

        return new AnalyticsResponse(
            StudioId: studio.Id,
            PayType: studio.PayType.ToString(),
            PayRate: studio.PayRate,
            HoursPerSession: AssumedHoursPerSession,
            TotalSessions: totalSessions,
            TotalPresent: totalPresent,
            AvgAttendanceRate: avgAttendanceRate,
            EstimatedPay: Math.Round(estimatedPay, 2),
            Trend: trend);
    }
}

/// <summary>A single month in the attendance-trend series.</summary>
public record MonthlyAttendancePoint(
    string Month,
    int Present,
    int Total,
    decimal AttendanceRate);

/// <summary>Analytics payload for one studio.</summary>
public record AnalyticsResponse(
    int StudioId,
    string PayType,
    decimal PayRate,
    decimal HoursPerSession,
    int TotalSessions,
    int TotalPresent,
    decimal AvgAttendanceRate,
    decimal EstimatedPay,
    IReadOnlyList<MonthlyAttendancePoint> Trend);
