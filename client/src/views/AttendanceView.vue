<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import {
  DialogRoot,
  DialogPortal,
  DialogOverlay,
  DialogContent,
  DialogTitle,
  DialogDescription,
  DialogClose,
} from 'reka-ui'
import { CalendarCheck, NotebookPen, X, AlertTriangle, Save, Loader2 } from 'lucide-vue-next'
import { api } from '@/lib/api'
import { toast } from '@/lib/toast'
import { useStudioStore } from '@/stores/studio'
import type { DanceClass, Student, AttendanceRecord, AttendanceStatus, ClassSession } from '@/types'
import type { AttendanceSummary, AttendanceUpsert } from './attendance/attendance-types'
import { renderMarkdown } from './attendance/markdown'

const studioStore = useStudioStore()

const STATUSES: AttendanceStatus[] = ['Present', 'Absent', 'Excused']
const LOW_ATTENDANCE_THRESHOLD = 0.75

function todayIso(): string {
  const d = new Date()
  const m = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${d.getFullYear()}-${m}-${day}`
}

const classes = ref<DanceClass[]>([])
const students = ref<Student[]>([])
const selectedClassId = ref<number | null>(null)
const selectedDate = ref<string>(todayIso())

// Local editable attendance state: studentId -> status.
const marks = ref<Record<number, AttendanceStatus>>({})
// Summary rate per student (0..1); missing = no data.
const rates = ref<Record<number, number>>({})

const saving = ref(false)
const dirty = ref(false)

// --- Class Notes drawer state ---
const notesOpen = ref(false)
const notesText = ref('')
const savingNotes = ref(false)
const notesPreview = computed(() => renderMarkdown(notesText.value))
const sessionHistory = ref<ClassSession[]>([])

const selectedClass = computed(
  () => classes.value.find((c) => c.id === selectedClassId.value) ?? null,
)

const hasRoster = computed(() => students.value.length > 0)

function studentName(s: Student): string {
  return `${s.firstName} ${s.lastName}`.trim() || `Student #${s.id}`
}

function isLowAttendance(studentId: number): boolean {
  const rate = rates.value[studentId]
  return rate !== undefined && rate < LOW_ATTENDANCE_THRESHOLD
}

function ratePercent(studentId: number): number {
  return Math.round((rates.value[studentId] ?? 0) * 100)
}

async function fetchClasses() {
  if (studioStore.selectedStudioId === null) {
    classes.value = []
    return
  }
  try {
    const { data } = await api.get<DanceClass[]>('/classes', {
      params: { studioId: studioStore.selectedStudioId },
    })
    classes.value = data
    if (selectedClassId.value === null && data.length > 0) {
      selectedClassId.value = data[0].id
    }
  } catch {
    classes.value = []
  }
}

async function fetchStudents() {
  if (studioStore.selectedStudioId === null) {
    students.value = []
    return
  }
  try {
    const { data } = await api.get<Student[]>('/students', {
      params: { studioId: studioStore.selectedStudioId },
    })
    students.value = data
  } catch {
    students.value = []
  }
}

async function fetchAttendance() {
  marks.value = {}
  if (selectedClassId.value === null || !selectedDate.value) return
  try {
    const { data } = await api.get<AttendanceRecord[]>('/attendance', {
      params: { classId: selectedClassId.value, date: selectedDate.value },
    })
    const next: Record<number, AttendanceStatus> = {}
    for (const r of data) next[r.studentId] = r.status
    marks.value = next
  } catch {
    marks.value = {}
  }
}

async function fetchSummary() {
  rates.value = {}
  if (selectedClassId.value === null) return
  try {
    const { data } = await api.get<AttendanceSummary[]>('/attendance/summary', {
      params: { classId: selectedClassId.value },
    })
    const next: Record<number, number> = {}
    for (const s of data) next[s.studentId] = s.rate
    rates.value = next
  } catch {
    rates.value = {}
  }
}

async function reloadForSelection() {
  dirty.value = false
  await Promise.all([fetchAttendance(), fetchSummary()])
}

function setStatus(studentId: number, status: AttendanceStatus) {
  marks.value = { ...marks.value, [studentId]: status }
  dirty.value = true
}

async function save() {
  if (selectedClassId.value === null || !selectedDate.value) return
  const payload: AttendanceUpsert[] = Object.entries(marks.value).map(
    ([studentId, status]) => ({
      studentId: Number(studentId),
      classId: selectedClassId.value as number,
      date: selectedDate.value,
      status,
    }),
  )
  if (payload.length === 0) return
  saving.value = true
  try {
    await api.put('/attendance', payload)
    dirty.value = false
    toast.success('Attendance saved')
    // Refresh the rolling summary since today's marks may change it.
    await fetchSummary()
  } catch {
    // DB unavailable — keep local state so work is not lost.
  } finally {
    saving.value = false
  }
}

// --- Class Notes ---
async function openNotes() {
  notesOpen.value = true
  notesText.value = ''
  sessionHistory.value = []
  if (selectedClassId.value === null || !selectedDate.value) return
  try {
    const res = await api.get('/classsessions', {
      params: { classId: selectedClassId.value, date: selectedDate.value },
    })
    // 204 => empty body (no session yet).
    notesText.value = res.data?.notes ?? ''
  } catch {
    notesText.value = ''
  }
  try {
    const { data } = await api.get<ClassSession[]>('/classsessions/history', {
      params: { classId: selectedClassId.value },
    })
    sessionHistory.value = data ?? []
  } catch {
    sessionHistory.value = []
  }
}

function loadPastSession(session: ClassSession) {
  selectedDate.value = session.date
  notesText.value = session.notes ?? ''
}

function formatSessionDate(iso: string): string {
  const d = new Date(`${iso}T00:00:00`)
  if (isNaN(d.getTime())) return iso
  return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' })
}

async function saveNotes() {
  if (selectedClassId.value === null || !selectedDate.value) return
  savingNotes.value = true
  try {
    await api.put('/classsessions', {
      classId: selectedClassId.value,
      date: selectedDate.value,
      notes: notesText.value,
    })
    notesOpen.value = false
    toast.success('Class notes saved')
  } catch {
    // DB unavailable — leave drawer open so notes are not lost.
  } finally {
    savingNotes.value = false
  }
}

onMounted(async () => {
  if (studioStore.studios.length === 0) {
    await studioStore.fetchStudios().catch(() => {})
  }
  await Promise.all([fetchClasses(), fetchStudents()])
  await reloadForSelection()
})

watch(
  () => studioStore.selectedStudioId,
  async () => {
    selectedClassId.value = null
    await Promise.all([fetchClasses(), fetchStudents()])
    await reloadForSelection()
  },
)

watch(selectedClassId, reloadForSelection)
watch(selectedDate, async () => {
  dirty.value = false
  await fetchAttendance()
})
</script>

<template>
  <div class="mx-auto max-w-6xl px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
    <header class="mb-6 border-b border-border pb-4">
      <div class="flex items-center justify-between">
        <h1 class="text-xl font-semibold tracking-tight">Attendance</h1>
        <span class="text-sm text-muted-foreground">
          {{ studioStore.selectedStudio?.name ?? 'No studio selected' }}
        </span>
      </div>
      <p class="mt-1 text-sm text-muted-foreground">
        Rapid daily attendance with a 3-state toggle. Amber flags students below 75% over the last
        4 weeks.
      </p>
    </header>

    <!-- Controls -->
    <div class="mb-5 flex flex-wrap items-end gap-4">
      <label class="flex flex-col gap-1.5">
        <span class="text-xs font-medium text-muted-foreground">Class</span>
        <select
          v-model="selectedClassId"
          class="h-9 min-w-52 rounded-md border border-border bg-background px-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:opacity-50"
          :disabled="classes.length === 0"
        >
          <option v-if="classes.length === 0" :value="null">No classes</option>
          <option v-for="c in classes" :key="c.id" :value="c.id">{{ c.name }}</option>
        </select>
      </label>

      <label class="flex flex-col gap-1.5">
        <span class="text-xs font-medium text-muted-foreground">Date</span>
        <input
          v-model="selectedDate"
          type="date"
          class="h-9 rounded-md border border-border bg-background px-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring"
        />
      </label>

      <div class="ml-auto flex items-center gap-2">
        <button
          class="inline-flex h-9 items-center gap-2 rounded-md border border-border px-3 text-sm font-medium hover:bg-accent hover:text-accent-foreground disabled:opacity-50"
          :disabled="selectedClassId === null"
          @click="openNotes"
        >
          <NotebookPen class="h-4 w-4" />
          Class Notes
        </button>
        <button
          class="inline-flex h-9 items-center gap-2 rounded-md bg-primary px-3 text-sm font-medium text-primary-foreground hover:opacity-90 disabled:opacity-50"
          :disabled="!dirty || saving || !hasRoster"
          @click="save"
        >
          <Loader2 v-if="saving" class="h-4 w-4 animate-spin" />
          <Save v-else class="h-4 w-4" />
          {{ saving ? 'Saving…' : 'Save' }}
        </button>
      </div>
    </div>

    <!-- Roster -->
    <div class="rounded-lg border border-border">
      <div
        v-if="!hasRoster"
        class="flex flex-col items-center gap-2 p-12 text-center text-sm text-muted-foreground"
      >
        <CalendarCheck class="h-6 w-6" />
        <p class="font-medium text-foreground">No students to show</p>
        <p>
          {{
            studioStore.selectedStudioId === null
              ? 'Select a studio to load its roster.'
              : 'This studio has no students yet, or the database is unavailable.'
          }}
        </p>
      </div>

      <ul v-else class="divide-y divide-border">
        <li
          v-for="s in students"
          :key="s.id"
          class="flex items-center justify-between gap-4 px-4 py-3"
        >
          <div class="flex min-w-0 items-center gap-2">
            <span class="truncate text-sm font-medium">{{ studentName(s) }}</span>
            <span
              v-if="isLowAttendance(s.id)"
              class="inline-flex items-center gap-1 rounded-full border border-amber-500/40 bg-amber-500/10 px-2 py-0.5 text-xs font-medium text-amber-600 dark:text-amber-400"
              :title="`${ratePercent(s.id)}% attendance over the last 4 weeks`"
            >
              <AlertTriangle class="h-3 w-3" />
              {{ ratePercent(s.id) }}%
            </span>
          </div>

          <!-- 3-state segmented toggle -->
          <div
            class="inline-flex shrink-0 overflow-hidden rounded-md border border-border"
            role="group"
            :aria-label="`Attendance for ${studentName(s)}`"
          >
            <button
              v-for="status in STATUSES"
              :key="status"
              type="button"
              class="h-8 border-l border-border px-3 text-xs font-medium transition-colors first:border-l-0"
              :class="
                marks[s.id] === status
                  ? status === 'Present'
                    ? 'bg-emerald-500 text-white'
                    : status === 'Absent'
                      ? 'bg-red-500 text-white'
                      : 'bg-amber-500 text-white'
                  : 'text-muted-foreground hover:bg-accent hover:text-accent-foreground'
              "
              :aria-pressed="marks[s.id] === status"
              @click="setStatus(s.id, status)"
            >
              {{ status }}
            </button>
          </div>
        </li>
      </ul>
    </div>

    <!-- Class Notes slide-out Sheet (reka-ui Dialog styled as a right drawer) -->
    <DialogRoot v-model:open="notesOpen">
      <DialogPortal>
        <DialogOverlay
          class="fixed inset-0 z-50 bg-black/40 data-[state=open]:animate-in data-[state=open]:fade-in"
        />
        <DialogContent
          class="fixed inset-y-0 right-0 z-50 flex w-full max-w-xl flex-col border-l border-border bg-background shadow-xl focus:outline-none data-[state=open]:animate-in data-[state=open]:slide-in-from-right"
        >
          <div class="flex items-center justify-between border-b border-border px-5 py-4">
            <div>
              <DialogTitle class="text-sm font-semibold tracking-tight">Class Notes</DialogTitle>
              <DialogDescription class="mt-0.5 text-xs text-muted-foreground">
                {{ selectedClass?.name ?? 'Class' }} · {{ selectedDate }} · Markdown supported
              </DialogDescription>
            </div>
            <DialogClose
              class="flex h-8 w-8 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-accent-foreground"
              aria-label="Close"
            >
              <X class="h-4 w-4" />
            </DialogClose>
          </div>

          <div
            v-if="sessionHistory.length > 0"
            class="flex flex-wrap items-center gap-1.5 border-b border-border px-5 py-3"
          >
            <span class="text-xs font-medium text-muted-foreground">Past sessions:</span>
            <button
              v-for="h in sessionHistory"
              :key="h.id"
              class="rounded-md border px-2 py-1 text-xs transition-colors"
              :class="
                h.date === selectedDate
                  ? 'border-foreground bg-foreground text-background'
                  : 'border-border hover:bg-accent'
              "
              @click="loadPastSession(h)"
            >
              {{ formatSessionDate(h.date) }}
            </button>
          </div>

          <div class="grid flex-1 grid-rows-2 gap-4 overflow-hidden p-5 lg:grid-rows-none lg:grid-cols-2">
            <div class="flex min-h-0 flex-col gap-1.5">
              <span class="text-xs font-medium text-muted-foreground">Editor</span>
              <textarea
                v-model="notesText"
                placeholder="# Warm-up&#10;- Pliés&#10;**Focus:** turnout"
                class="min-h-0 flex-1 resize-none rounded-md border border-border bg-background p-3 font-mono text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring"
              ></textarea>
            </div>
            <div class="flex min-h-0 flex-col gap-1.5">
              <span class="text-xs font-medium text-muted-foreground">Preview</span>
              <div
                class="markdown-preview min-h-0 flex-1 overflow-y-auto rounded-md border border-border bg-muted/30 p-3 text-sm"
                v-html="notesPreview || '<p class=\'text-muted-foreground\'>Nothing to preview yet.</p>'"
              ></div>
            </div>
          </div>

          <div class="flex items-center justify-end gap-2 border-t border-border px-5 py-4">
            <DialogClose
              class="inline-flex h-9 items-center rounded-md border border-border px-3 text-sm font-medium hover:bg-accent hover:text-accent-foreground"
            >
              Cancel
            </DialogClose>
            <button
              class="inline-flex h-9 items-center gap-2 rounded-md bg-primary px-3 text-sm font-medium text-primary-foreground hover:opacity-90 disabled:opacity-50"
              :disabled="savingNotes"
              @click="saveNotes"
            >
              <Loader2 v-if="savingNotes" class="h-4 w-4 animate-spin" />
              <Save v-else class="h-4 w-4" />
              {{ savingNotes ? 'Saving…' : 'Save Notes' }}
            </button>
          </div>
        </DialogContent>
      </DialogPortal>
    </DialogRoot>
  </div>
</template>

<style scoped>
.markdown-preview :deep(h1) {
  font-size: 1.125rem;
  font-weight: 600;
  margin: 0.5rem 0 0.25rem;
}
.markdown-preview :deep(h2) {
  font-size: 1rem;
  font-weight: 600;
  margin: 0.5rem 0 0.25rem;
}
.markdown-preview :deep(h3) {
  font-size: 0.9375rem;
  font-weight: 600;
  margin: 0.5rem 0 0.25rem;
}
.markdown-preview :deep(p) {
  margin: 0.25rem 0;
}
.markdown-preview :deep(ul) {
  list-style: disc;
  padding-left: 1.25rem;
  margin: 0.25rem 0;
}
.markdown-preview :deep(ol) {
  list-style: decimal;
  padding-left: 1.25rem;
  margin: 0.25rem 0;
}
.markdown-preview :deep(strong) {
  font-weight: 600;
}
.markdown-preview :deep(em) {
  font-style: italic;
}
</style>
