<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { api } from '@/lib/api'
import { useStudioStore } from '@/stores/studio'
import type { DanceClass, Student, StudentNote, RecitalParticipation } from '@/types'
import {
  DialogRoot,
  DialogPortal,
  DialogOverlay,
  DialogContent,
  DialogTitle,
  DialogClose,
  PopoverRoot,
  PopoverTrigger,
  PopoverPortal,
  PopoverContent,
} from 'reka-ui'
import { AlertTriangle, X, ChevronDown, Check } from 'lucide-vue-next'

type ProgressStatus = 'NotStarted' | 'InProgress' | 'Mastered'

interface Milestone {
  id: number
  studioId: number
  classId?: number | null
  name: string
}

interface ProgressionCell {
  studentId: number
  milestoneId: number
  status: ProgressStatus
}

interface ProgressionMatrix {
  milestones: Milestone[]
  students: Student[]
  statuses: ProgressionCell[]
}

const studioStore = useStudioStore()

const classes = ref<DanceClass[]>([])
const selectedClassId = ref<number | null>(null)
const milestones = ref<Milestone[]>([])
const students = ref<Student[]>([])
const statuses = ref<ProgressionCell[]>([])
const participation = ref<RecitalParticipation[]>([])
const loading = ref(false)

// Which cell's status dropdown is open (`${studentId}:${milestoneId}` or null).
const openCell = ref<string | null>(null)

const STATUS_OPTIONS: { value: ProgressStatus; label: string }[] = [
  { value: 'NotStarted', label: 'Not Started' },
  { value: 'InProgress', label: 'In Progress' },
  { value: 'Mastered', label: 'Mastered' },
]

const STATUS_STYLES: Record<ProgressStatus, string> = {
  NotStarted: 'bg-muted text-muted-foreground',
  InProgress: 'bg-amber-100 text-amber-800',
  Mastered: 'bg-emerald-100 text-emerald-800',
}

const STATUS_LABELS: Record<ProgressStatus, string> = {
  NotStarted: 'Not Started',
  InProgress: 'In Progress',
  Mastered: 'Mastered',
}

// Fast lookup: `${studentId}:${milestoneId}` -> status.
const statusMap = computed(() => {
  const m = new Map<string, ProgressStatus>()
  for (const c of statuses.value) m.set(`${c.studentId}:${c.milestoneId}`, c.status)
  return m
})

function cellStatus(studentId: number, milestoneId: number): ProgressStatus {
  return statusMap.value.get(`${studentId}:${milestoneId}`) ?? 'NotStarted'
}

// Participation tally: students not present in the participation list count as No.
const participationTally = computed(() => {
  const yesIds = new Set(
    participation.value.filter((p) => p.isParticipating).map((p) => p.studentId),
  )
  let yes = 0
  for (const s of students.value) if (yesIds.has(s.id)) yes++
  return { yes, no: students.value.length - yes }
})

async function loadClasses() {
  const studioId = studioStore.selectedStudioId
  if (!studioId) {
    classes.value = []
    selectedClassId.value = null
    return
  }
  try {
    const { data } = await api.get<DanceClass[]>('/classes', { params: { studioId } })
    classes.value = data
    if (data.length > 0 && !data.some((c) => c.id === selectedClassId.value)) {
      selectedClassId.value = data[0].id
    } else if (data.length === 0) {
      selectedClassId.value = null
    }
  } catch {
    classes.value = []
    selectedClassId.value = null
  }
}

async function loadMatrix() {
  const classId = selectedClassId.value
  if (!classId) {
    milestones.value = []
    students.value = []
    statuses.value = []
    participation.value = []
    return
  }
  loading.value = true
  try {
    const { data } = await api.get<ProgressionMatrix>('/progression', { params: { classId } })
    milestones.value = data.milestones ?? []
    students.value = data.students ?? []
    statuses.value = data.statuses ?? []
  } catch {
    milestones.value = []
    students.value = []
    statuses.value = []
  }
  try {
    const { data } = await api.get<RecitalParticipation[]>('/recitalparticipation', {
      params: { classId },
    })
    participation.value = data ?? []
  } catch {
    participation.value = []
  }
  loading.value = false
}

async function setCellStatus(studentId: number, milestoneId: number, status: ProgressStatus) {
  openCell.value = null
  const idx = statuses.value.findIndex(
    (c) => c.studentId === studentId && c.milestoneId === milestoneId,
  )
  const prev = idx >= 0 ? statuses.value[idx] : null
  // Optimistic update.
  if (idx >= 0) statuses.value[idx] = { studentId, milestoneId, status }
  else statuses.value.push({ studentId, milestoneId, status })

  try {
    await api.put('/progression', { studentId, milestoneId, status })
  } catch {
    // Roll back on failure (e.g. DB unavailable).
    if (prev) statuses.value[idx] = prev
    else
      statuses.value = statuses.value.filter(
        (c) => !(c.studentId === studentId && c.milestoneId === milestoneId),
      )
  }
}

// --- Student detail slide-over ---
const detailStudent = ref<Student | null>(null)
const detailOpen = ref(false)
const notes = ref<StudentNote[]>([])
const notesLoading = ref(false)
const newNote = ref('')
const savingNote = ref(false)

async function openStudent(student: Student) {
  detailStudent.value = student
  detailOpen.value = true
  newNote.value = ''
  notes.value = []
  notesLoading.value = true
  try {
    const { data } = await api.get<StudentNote[]>('/studentnotes', {
      params: { studentId: student.id },
    })
    notes.value = data ?? []
  } catch {
    notes.value = []
  }
  notesLoading.value = false
}

async function addNote() {
  const student = detailStudent.value
  const text = newNote.value.trim()
  if (!student || !text) return
  savingNote.value = true
  try {
    const { data } = await api.post<StudentNote>('/studentnotes', {
      studentId: student.id,
      classId: selectedClassId.value,
      note: text,
    })
    notes.value = [data, ...notes.value]
    newNote.value = ''
  } catch {
    // Leave the textarea intact so the user can retry.
  }
  savingNote.value = false
}

function formatDate(iso: string): string {
  const d = new Date(iso)
  if (isNaN(d.getTime())) return iso
  return d.toLocaleString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

function studentName(s: Student): string {
  return `${s.firstName} ${s.lastName}`.trim()
}

onMounted(async () => {
  if (studioStore.studios.length === 0) {
    try {
      await studioStore.fetchStudios()
    } catch {
      /* graceful: no studios */
    }
  }
  // loadClasses assigns selectedClassId, which drives loadMatrix via the watcher below.
  await loadClasses()
})

watch(
  () => studioStore.selectedStudioId,
  async () => {
    await loadClasses()
  },
)

// Single source of truth for loading the matrix: whenever the selected class changes
// (by user, by studio switch, or on initial load), refetch. `immediate` covers the
// case where loadClasses leaves the id unchanged (e.g. no classes in either studio).
watch(selectedClassId, async () => {
  await loadMatrix()
})
</script>

<template>
  <div class="mx-auto max-w-7xl px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
    <!-- Header -->
    <header class="mb-6 border-b border-border pb-4">
      <div class="flex items-center justify-between">
        <h1 class="text-xl font-semibold tracking-tight">Roster &amp; Progression</h1>
        <span class="text-sm text-muted-foreground">
          {{ studioStore.selectedStudio?.name ?? 'No studio selected' }}
        </span>
      </div>
      <p class="mt-1 text-sm text-muted-foreground">
        Track student progression across milestones, flag injuries, and log notes.
      </p>
    </header>

    <!-- Class selector -->
    <div class="mb-6 flex items-center gap-3">
      <label class="text-sm font-medium">Class</label>
      <div class="relative">
        <select
          v-model="selectedClassId"
          class="appearance-none rounded-md border border-border bg-background py-2 pl-3 pr-9 text-sm hover:bg-accent focus:outline-none focus:ring-2 focus:ring-ring disabled:opacity-50"
          :disabled="classes.length === 0"
        >
          <option v-if="classes.length === 0" :value="null">No classes</option>
          <option v-for="c in classes" :key="c.id" :value="c.id">{{ c.name }}</option>
        </select>
        <ChevronDown
          class="pointer-events-none absolute right-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground"
        />
      </div>
    </div>

    <!-- Empty state -->
    <div
      v-if="!selectedClassId || (students.length === 0 && !loading)"
      class="rounded-lg border border-dashed border-border p-8 text-sm text-muted-foreground"
    >
      <p class="mb-1 font-medium text-foreground">Nothing to show yet</p>
      <p v-if="!selectedClassId">Select a class to view its roster and progression matrix.</p>
      <p v-else>No students found for this class.</p>
    </div>

    <template v-else>
      <!-- Progression matrix -->
      <div class="overflow-x-auto rounded-lg border border-border">
        <table class="w-full border-collapse text-sm">
          <thead>
            <tr class="border-b border-border bg-muted/40">
              <th
                class="sticky left-0 z-10 min-w-[200px] bg-muted/40 px-4 py-3 text-left font-medium"
              >
                Student
              </th>
              <th
                v-for="m in milestones"
                :key="m.id"
                class="min-w-[120px] whitespace-nowrap px-3 py-3 text-left font-medium"
              >
                {{ m.name }}
              </th>
              <th
                v-if="milestones.length === 0"
                class="px-3 py-3 text-left font-normal text-muted-foreground"
              >
                No milestones defined
              </th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="s in students"
              :key="s.id"
              class="border-b border-border last:border-0 hover:bg-accent/40"
            >
              <!-- Name + injury badge -->
              <td class="sticky left-0 z-10 bg-background px-4 py-2.5">
                <div class="flex items-center gap-2">
                  <button
                    class="truncate text-left font-medium hover:underline"
                    @click="openStudent(s)"
                  >
                    {{ studentName(s) }}
                  </button>
                  <PopoverRoot v-if="s.injuryAlert">
                    <PopoverTrigger as-child>
                      <button
                        class="inline-flex items-center gap-1 rounded-full bg-destructive px-1.5 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-destructive-foreground"
                        aria-label="Injury alert"
                      >
                        <AlertTriangle class="h-3 w-3" />
                        Injury
                      </button>
                    </PopoverTrigger>
                    <PopoverPortal>
                      <PopoverContent
                        side="right"
                        :side-offset="6"
                        class="z-50 max-w-xs rounded-md border border-border bg-popover p-3 text-xs text-popover-foreground shadow-md"
                      >
                        <p class="mb-1 font-semibold text-destructive">Movement modifications</p>
                        <p class="text-muted-foreground">
                          {{ s.movementModifications || 'No modifications recorded.' }}
                        </p>
                      </PopoverContent>
                    </PopoverPortal>
                  </PopoverRoot>
                </div>
              </td>

              <!-- Status cells -->
              <td v-for="m in milestones" :key="m.id" class="px-3 py-2.5">
                <PopoverRoot
                  :open="openCell === `${s.id}:${m.id}`"
                  @update:open="(v: boolean) => (openCell = v ? `${s.id}:${m.id}` : null)"
                >
                  <PopoverTrigger as-child>
                    <button
                      :class="[
                        'w-full rounded-md px-2 py-1 text-center text-xs font-medium transition-colors',
                        STATUS_STYLES[cellStatus(s.id, m.id)],
                      ]"
                    >
                      {{ STATUS_LABELS[cellStatus(s.id, m.id)] }}
                    </button>
                  </PopoverTrigger>
                  <PopoverPortal>
                    <PopoverContent
                      :side-offset="4"
                      class="z-50 w-40 rounded-md border border-border bg-popover p-1 shadow-md"
                    >
                      <button
                        v-for="opt in STATUS_OPTIONS"
                        :key="opt.value"
                        class="flex w-full items-center justify-between rounded-sm px-2 py-1.5 text-sm hover:bg-accent"
                        @click="setCellStatus(s.id, m.id, opt.value)"
                      >
                        <span>{{ opt.label }}</span>
                        <Check v-if="cellStatus(s.id, m.id) === opt.value" class="h-4 w-4" />
                      </button>
                    </PopoverContent>
                  </PopoverPortal>
                </PopoverRoot>
              </td>
              <td v-if="milestones.length === 0" class="px-3 py-2.5 text-muted-foreground">—</td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Participation tally -->
      <div class="mt-4 flex items-center gap-2 text-sm">
        <span class="font-medium">Recital participation:</span>
        <span class="text-emerald-700">{{ participationTally.yes }} Yes</span>
        <span class="text-muted-foreground">·</span>
        <span class="text-muted-foreground">{{ participationTally.no }} No</span>
      </div>
    </template>

    <!-- Student detail slide-over -->
    <DialogRoot v-model:open="detailOpen">
      <DialogPortal>
        <DialogOverlay class="fixed inset-0 z-50 bg-black/40" />
        <DialogContent
          class="fixed inset-y-0 right-0 z-50 flex w-full max-w-md flex-col border-l border-border bg-background shadow-xl focus:outline-none"
        >
          <div class="flex items-center justify-between border-b border-border px-5 py-4">
            <DialogTitle class="text-base font-semibold">
              {{ detailStudent ? studentName(detailStudent) : 'Student' }}
            </DialogTitle>
            <DialogClose
              class="flex h-8 w-8 items-center justify-center rounded-md text-muted-foreground hover:bg-accent"
              aria-label="Close"
            >
              <X class="h-4 w-4" />
            </DialogClose>
          </div>

          <div v-if="detailStudent" class="flex-1 space-y-6 overflow-y-auto px-5 py-4">
            <!-- Info -->
            <section class="space-y-1 text-sm">
              <div
                v-if="detailStudent.injuryAlert"
                class="mb-3 flex items-start gap-2 rounded-md border border-destructive/40 bg-destructive/10 p-3"
              >
                <AlertTriangle class="mt-0.5 h-4 w-4 shrink-0 text-destructive" />
                <div>
                  <p class="font-semibold text-destructive">Injury alert</p>
                  <p class="text-muted-foreground">
                    {{ detailStudent.movementModifications || 'No modifications recorded.' }}
                  </p>
                </div>
              </div>
              <p v-if="detailStudent.parentName">
                <span class="text-muted-foreground">Parent:</span> {{ detailStudent.parentName }}
              </p>
              <p v-if="detailStudent.parentEmail">
                <span class="text-muted-foreground">Email:</span> {{ detailStudent.parentEmail }}
              </p>
              <p v-if="detailStudent.parentPhone">
                <span class="text-muted-foreground">Phone:</span> {{ detailStudent.parentPhone }}
              </p>
              <p v-if="detailStudent.medicalNotes">
                <span class="text-muted-foreground">Medical:</span> {{ detailStudent.medicalNotes }}
              </p>
            </section>

            <!-- Notes -->
            <section>
              <h3 class="mb-2 text-sm font-semibold">Notes</h3>
              <div class="mb-3 space-y-2">
                <textarea
                  v-model="newNote"
                  rows="3"
                  placeholder="Add a note…"
                  class="w-full resize-none rounded-md border border-border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
                ></textarea>
                <button
                  class="rounded-md bg-primary px-3 py-1.5 text-sm font-medium text-primary-foreground hover:opacity-90 disabled:opacity-50"
                  :disabled="!newNote.trim() || savingNote"
                  @click="addNote"
                >
                  {{ savingNote ? 'Saving…' : 'Add note' }}
                </button>
              </div>

              <p v-if="notesLoading" class="text-sm text-muted-foreground">Loading notes…</p>
              <p v-else-if="notes.length === 0" class="text-sm text-muted-foreground">
                No notes yet.
              </p>
              <ul v-else class="space-y-3">
                <li
                  v-for="n in notes"
                  :key="n.id"
                  class="rounded-md border border-border p-3 text-sm"
                >
                  <p class="whitespace-pre-wrap">{{ n.note }}</p>
                  <p class="mt-1.5 text-xs text-muted-foreground">{{ formatDate(n.createdAt) }}</p>
                </li>
              </ul>
            </section>
          </div>
        </DialogContent>
      </DialogPortal>
    </DialogRoot>
  </div>
</template>
