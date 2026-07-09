// Feature DTO types for Unit 4 (Analytics Hub). These mirror the records in
// server/DanceManager.Api/Controllers/AnalyticsController.cs and are kept
// local to the feature (the shared types/index.ts is off-limits).

export interface MonthlyAttendancePoint {
  month: string
  present: number
  total: number
  attendanceRate: number
}

export interface AnalyticsResponse {
  studioId: number
  payType: 'Hourly' | 'PerHeadcount'
  payRate: number
  hoursPerSession: number
  totalSessions: number
  totalPresent: number
  avgAttendanceRate: number
  estimatedPay: number
  trend: MonthlyAttendancePoint[]
}
