// Shared types mirroring the .NET backend models (server/DanceManager.Api/Models).
// Enums are serialized as strings by the API.

export type PayType = 'Hourly' | 'PerHeadcount'
export type AttendanceStatus = 'Present' | 'Absent' | 'Excused'
export type OrderStatus = 'NotOrdered' | 'Ordered' | 'Shipped' | 'Delivered'
export type Gender = 'Boys' | 'Girls'
export type AuditionDecision = 'Undecided' | 'Yes' | 'No'

export interface Studio {
  id: number
  name: string
  address?: string | null
  payType: PayType
  payRate: number
}

export interface DanceClass {
  id: number
  studioId: number
  name: string
}

export interface Student {
  id: number
  studioId: number
  firstName: string
  lastName: string
  dateOfBirth?: string | null
  parentName?: string | null
  parentEmail?: string | null
  parentPhone?: string | null
  medicalNotes?: string | null
  injuryAlert: boolean
  movementModifications?: string | null
  gender?: Gender | null
}

export interface StudentNote {
  id: number
  studentId: number
  classId?: number | null
  note: string
  createdAt: string
}

export interface AttendanceRecord {
  id: number
  studentId: number
  classId: number
  date: string
  status: AttendanceStatus
  note?: string | null
}

export interface ClassSession {
  id: number
  classId: number
  date: string
  notes?: string | null
}

export interface LessonPlanEntry {
  id: number
  classId: number
  weekOf: string
  coveredThisWeek?: string | null
  plannedNextWeek?: string | null
  notes?: string | null
}

export interface Routine {
  id: number
  classId: number
  songTitle: string
  artist?: string | null
  videoUrl?: string | null
  choreographyNotes?: string | null
}

export interface Formation {
  id: number
  routineId: number
  formationName: string
  orderIndex: number
  /** JSON string: { [studentId: number]: { x: number; y: number } } percentages */
  studentCoordinates: string
}

export interface RecitalParticipation {
  studentId: number
  classId: number
  isParticipating: boolean
}

export interface Enrollment {
  studentId: number
  classId: number
}

export interface ShowSection {
  id: number
  studioId: number
  name: string
  orderIndex: number
}

export interface ShowProgram {
  id: number
  routineId: number | null
  sectionId?: number | null
  studioId?: number | null
  title?: string | null
  /** JSON string: number[] of studentIds attached to a standalone/quick-add number. */
  studentIds?: string | null
  orderPosition: number
}

export interface CostumeRecord {
  id: number
  studentId: number
  costumeSize?: string | null
  feeAmount: number
  isPaid: boolean
  orderStatus: OrderStatus
  alterationNotes?: string | null
}

export interface SongChoice {
  id: number
  routineId: number
  songTitle: string
  artist?: string | null
  musicCutNotes?: string | null
}

export interface CostumeOption {
  id: number
  routineId: number
  gender: Gender
  description?: string | null
  accessories?: string | null
  shoes?: string | null
  photoLink?: string | null
  optionLink?: string | null
}

export interface Audition {
  id: number
  title: string
  date: string
  spotsAvailable: number
  /** JSON string: string[] of skill column names */
  skillColumns: string
}

export interface AuditionCandidate {
  id: number
  auditionId: number
  name: string
  /** JSON string: { [skillName: string]: number } 1-5 scores */
  scores: string
  decision: AuditionDecision
  notes?: string | null
}
