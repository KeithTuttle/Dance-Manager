// Feature-local DTO types for Lesson Plans (Unit 5).
// Shared model types live in @/types (do not edit that file).

/** Editable string fields on a lesson-plan entry. */
export type LessonPlanField = 'coveredThisWeek' | 'plannedNextWeek' | 'notes'

/** Payload sent when creating a new lesson-plan entry. */
export interface NewLessonPlanEntry {
  classId: number
  weekOf: string
  coveredThisWeek?: string | null
  plannedNextWeek?: string | null
  notes?: string | null
}
