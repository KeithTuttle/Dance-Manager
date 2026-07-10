// Feature-local DTO types for the Attendance Tracker unit.
// Shared model types live in `@/types` (do not edit that file).

import type { AttendanceStatus } from '@/types'

/** Rolling 4-week attendance summary per student (from GET /api/attendance/summary). */
export interface AttendanceSummary {
  studentId: number
  present: number
  total: number
  /** Fraction 0..1 of recorded sessions the student was Present. */
  rate: number
}

/** A single mark in the bulk PUT /api/attendance payload. */
export interface AttendanceUpsert {
  studentId: number
  classId: number
  date: string
  status: AttendanceStatus
}
