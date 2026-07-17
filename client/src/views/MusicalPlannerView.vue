<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { RouterLink } from 'vue-router'
import { api } from '@/lib/api'
import { useStudioStore } from '@/stores/studio'
import type {
  DanceClass,
  Routine,
  Student,
  RoutineCast,
  ShowProgram,
  ShowSection,
  Gender,
} from '@/types'
import { toast } from '@/lib/toast'
import { confirm } from '@/lib/confirm'
import {
  Theater,
  Plus,
  Trash2,
  Users,
  Copy,
  AlertTriangle,
  LayoutGrid,
  ListMusic,
  ExternalLink,
  Settings,
  UserX,
  X,
  FileDown,
  Search,
  ChevronDown,
} from 'lucide-vue-next'
import FormationEditor from '@/components/FormationEditor.vue'

// Musical Planner: model a production as the selected Studio, its cast groups as
// Classes, and each number as a Routine whose "home group" is its Class. A
// number's real cast is an explicit RoutineCast set that can span groups.
const studioStore = useStudioStore()

type PlannerView = 'plan' | 'overview'
const view = ref<PlannerView>('plan')

const classes = ref<DanceClass[]>([]) // cast groups
const routines = ref<Routine[]>([]) // numbers
const students = ref<Student[]>([]) // studio roster
const allCasts = ref<RoutineCast[]>([]) // every cast in the production
const groupRosters = ref<Record<number, Student[]>>({}) // classId -> enrolled students
const showProgram = ref<ShowProgram[]>([]) // running order (for quick-change flags)
const showSections = ref<ShowSection[]>([])
const selectedRoutineId = ref<number | null>(null)

const byName = (a: Student, b: Student) =>
  `${a.firstName} ${a.lastName}`.localeCompare(`${b.firstName} ${b.lastName}`)
const byPos = (a: ShowProgram, b: ShowProgram) => a.orderPosition - b.orderPosition

const selectedRoutine = computed(
  () => routines.value.find((r) => r.id === selectedRoutineId.value) ?? null,
)
const classMap = computed(() => new Map(classes.value.map((c) => [c.id, c])))
const studentMap = computed(() => new Map(students.value.map((s) => [s.id, s])))
const sortedClasses = computed(() =>
  [...classes.value].sort((a, b) => a.name.localeCompare(b.name)),
)

// Numbers grouped by home group (class), groups in name order.
const numbersByGroup = computed(() =>
  sortedClasses.value.map((g) => ({
    group: g,
    numbers: routines.value
      .filter((r) => r.classId === g.id)
      .sort((a, b) => (a.songTitle || '').localeCompare(b.songTitle || '')),
  })),
)

// --- Casts, derived from the production-wide set ---
const castByRoutine = computed(() => {
  const map = new Map<number, Set<number>>()
  for (const c of allCasts.value) {
    if (!map.has(c.routineId)) map.set(c.routineId, new Set())
    map.get(c.routineId)!.add(c.studentId)
  }
  return map
})
function castOf(routineId: number | null): Set<number> {
  return (routineId != null && castByRoutine.value.get(routineId)) || new Set<number>()
}
const selectedCast = computed(() => castOf(selectedRoutineId.value))
const castStudents = computed(() =>
  students.value.filter((s) => selectedCast.value.has(s.id)),
)

// numbers-per-dancer count, for the overview + load flags.
const countByStudent = computed(() => {
  const m = new Map<number, number>()
  for (const c of allCasts.value) m.set(c.studentId, (m.get(c.studentId) ?? 0) + 1)
  return m
})
function countOf(id: number): number {
  return countByStudent.value.get(id) ?? 0
}
// "Busy" = at or above 1.5x the average load (min 3) — a soft, show-relative flag.
const busyThreshold = computed(() => {
  const counts = [...countByStudent.value.values()].filter((n) => n > 0)
  if (counts.length === 0) return Infinity
  const avg = counts.reduce((a, b) => a + b, 0) / counts.length
  return Math.max(3, Math.ceil(avg * 1.5))
})
function isBusy(id: number): boolean {
  return countOf(id) >= busyThreshold.value
}

// --- Quick-change conflicts, from the running order (cast-aware) ---
function entryDancers(entry: ShowProgram): Set<number> {
  if (entry.routineId != null) return castOf(entry.routineId)
  if (!entry.studentIds) return new Set()
  try {
    const parsed = JSON.parse(entry.studentIds)
    return new Set(Array.isArray(parsed) ? parsed.filter((n) => typeof n === 'number') : [])
  } catch {
    return new Set()
  }
}
const routineById = computed(() => new Map(routines.value.map((r) => [r.id, r])))
// Distinct costume labels already in use, for the reuse datalist.
const costumeLabels = computed(() =>
  [
    ...new Set(
      routines.value.map((r) => r.costumeLabel?.trim()).filter((l): l is string => !!l),
    ),
  ].sort(),
)
// A quick change is only asserted with definitive data: BOTH adjacent numbers
// have a costume label and they differ. Unlabeled numbers never flag.
function costumeChanges(a: ShowProgram, b: ShowProgram): boolean {
  const ra = a.routineId != null ? routineById.value.get(a.routineId) : undefined
  const rb = b.routineId != null ? routineById.value.get(b.routineId) : undefined
  const ca = ra?.costumeLabel?.trim().toLowerCase()
  const cb = rb?.costumeLabel?.trim().toLowerCase()
  return !!ca && !!cb && ca !== cb
}
// studentId -> names of numbers they quick-change between (adjacent within a section).
const conflictsByStudent = computed(() => {
  const map = new Map<number, Set<number>>() // studentId -> set of routineIds involved
  const groups: ShowProgram[][] = []
  for (const sec of [...showSections.value].sort((a, b) => a.orderIndex - b.orderIndex))
    groups.push(showProgram.value.filter((p) => p.sectionId === sec.id).sort(byPos))
  groups.push(showProgram.value.filter((p) => p.sectionId == null).sort(byPos))
  for (const g of groups) {
    for (let i = 0; i < g.length - 1; i++) {
      if (!costumeChanges(g[i], g[i + 1])) continue // only flag a proven costume change
      const a = entryDancers(g[i])
      const b = entryDancers(g[i + 1])
      if (a.size === 0 || b.size === 0) continue
      for (const id of a) {
        if (!b.has(id)) continue
        if (!map.has(id)) map.set(id, new Set())
        if (g[i].routineId != null) map.get(id)!.add(g[i].routineId!)
        if (g[i + 1].routineId != null) map.get(id)!.add(g[i + 1].routineId!)
      }
    }
  }
  return map
})
function hasConflict(id: number): boolean {
  return (conflictsByStudent.value.get(id)?.size ?? 0) > 0
}

// --- Overview matrix shape ---
// Columns (numbers) can be ordered two ways: by the show's running order
// (default) or by home group. Rows are dancers grouped by enrolled group.
type ColumnMode = 'running' | 'group'
const columnMode = ref<ColumnMode>('running')

type ColumnGroup = { key: string; label: string; numbers: Routine[] }

// Columns grouped by home group (skip empty groups).
const groupColumns = computed<ColumnGroup[]>(() =>
  numbersByGroup.value
    .filter((g) => g.numbers.length > 0)
    .map((g) => ({ key: `g${g.group.id}`, label: g.group.name, numbers: g.numbers })),
)

// Columns in the show's running order, grouped by section (act); any numbers
// not yet placed in the show order are collected under a trailing group.
const runningColumns = computed<ColumnGroup[]>(() => {
  const routineById = new Map(routines.value.map((r) => [r.id, r]))
  const used = new Set<number>()
  const cols: ColumnGroup[] = []
  const addSection = (key: string, label: string, entries: ShowProgram[]) => {
    const nums: Routine[] = []
    for (const e of [...entries].sort(byPos)) {
      if (e.routineId == null) continue
      const r = routineById.get(e.routineId)
      if (r && !used.has(r.id)) {
        used.add(r.id)
        nums.push(r)
      }
    }
    if (nums.length) cols.push({ key, label, numbers: nums })
  }
  for (const sec of [...showSections.value].sort((a, b) => a.orderIndex - b.orderIndex))
    addSection(`sec${sec.id}`, sec.name, showProgram.value.filter((p) => p.sectionId === sec.id))
  addSection('sec-unassigned', 'Unassigned', showProgram.value.filter((p) => p.sectionId == null))
  const rest = routines.value
    .filter((r) => !used.has(r.id))
    .sort((a, b) => (a.songTitle || '').localeCompare(b.songTitle || ''))
  if (rest.length) cols.push({ key: 'unscheduled', label: 'Not in show order', numbers: rest })
  return cols
})

const matrixColumns = computed<ColumnGroup[]>(() =>
  columnMode.value === 'running' ? runningColumns.value : groupColumns.value,
)
const flatNumbers = computed(() => matrixColumns.value.flatMap((g) => g.numbers))

// Rows: dancers grouped by their enrolled group, then an "Ungrouped" bucket.
const groupedStudentIds = computed(() => {
  const s = new Set<number>()
  for (const list of Object.values(groupRosters.value)) for (const st of list) s.add(st.id)
  return s
})
const ungroupedStudents = computed(() =>
  students.value.filter((s) => !groupedStudentIds.value.has(s.id)),
)
type RowGroup = { key: string; id: number | null; name: string; students: Student[] }
// All non-empty dancer groups (drive the filter chips), before hiding is applied.
const rowGroupsAll = computed<RowGroup[]>(() => {
  const secs: RowGroup[] = sortedClasses.value.map((c) => ({
    key: `g${c.id}`,
    id: c.id as number | null,
    name: c.name,
    students: [...(groupRosters.value[c.id] ?? [])].sort(byName),
  }))
  const ung = [...ungroupedStudents.value].sort(byName)
  if (ung.length) secs.push({ key: 'ungrouped', id: null, name: 'Ungrouped', students: ung })
  return secs.filter((s) => s.students.length > 0)
})
// Row-group visibility filter (empty set = show all).
const hiddenRowGroups = ref<Set<string>>(new Set())
function toggleRowGroup(key: string) {
  const next = new Set(hiddenRowGroups.value)
  if (next.has(key)) next.delete(key)
  else next.add(key)
  hiddenRowGroups.value = next
}
const matrixRows = computed(() =>
  rowGroupsAll.value.filter((s) => !hiddenRowGroups.value.has(s.key)),
)
// Dancers in the studio not cast in anything.
const uncastStudents = computed(() =>
  [...students.value].sort(byName).filter((s) => countOf(s.id) === 0),
)

// --- Dancer search (highlights matching rows in the overview) ---
const dancerSearch = ref('')
const searchMatches = computed<Set<number> | null>(() => {
  const q = dancerSearch.value.trim().toLowerCase()
  if (!q) return null
  const set = new Set<number>()
  for (const s of students.value)
    if (`${s.firstName} ${s.lastName}`.toLowerCase().includes(q)) set.add(s.id)
  return set
})
function isSearchMatch(id: number): boolean {
  return searchMatches.value?.has(id) ?? false
}
function isDimmed(id: number): boolean {
  return searchMatches.value != null && !searchMatches.value.has(id)
}

// --- Quick-change conflicts: adjacent numbers (running order) sharing dancers ---
const quickChangeList = computed(() => {
  const groups: ShowProgram[][] = []
  for (const sec of [...showSections.value].sort((a, b) => a.orderIndex - b.orderIndex))
    groups.push(showProgram.value.filter((p) => p.sectionId === sec.id).sort(byPos))
  groups.push(showProgram.value.filter((p) => p.sectionId == null).sort(byPos))
  const label = (e: ShowProgram) =>
    e.routineId != null ? numberTitleOf(e.routineId) : e.title || 'Guest number'
  const out: { a: string; b: string; dancers: string[] }[] = []
  for (const g of groups) {
    for (let i = 0; i < g.length - 1; i++) {
      if (!costumeChanges(g[i], g[i + 1])) continue // only flag a proven costume change
      const sa = entryDancers(g[i])
      const sb = entryDancers(g[i + 1])
      if (sa.size === 0 || sb.size === 0) continue
      const shared = [...sa].filter((id) => sb.has(id))
      if (shared.length === 0) continue
      out.push({
        a: label(g[i]),
        b: label(g[i + 1]),
        dancers: shared
          .map((id) => {
            const s = studentMap.value.get(id)
            return s ? `${s.firstName} ${s.lastName}` : `#${id}`
          })
          .sort(),
      })
    }
  }
  return out
})

// --- Printable cast sheets (PDF) ---
async function downloadCastSheet(mode: 'number' | 'dancer') {
  const studioId = studioStore.selectedStudioId
  if (studioId == null) return
  try {
    const res = await api.get('/castsheets', {
      params: { studioId, mode },
      responseType: 'blob',
    })
    const blob = new Blob([res.data], { type: 'application/pdf' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = mode === 'dancer' ? 'dancer-schedules.pdf' : 'cast-list.pdf'
    document.body.appendChild(a)
    a.click()
    a.remove()
    URL.revokeObjectURL(url)
  } catch {
    toast.error('Couldn’t generate the PDF.')
  }
}

function numberTitleOf(id: number): string {
  const r = routines.value.find((x) => x.id === id)
  return r?.songTitle || 'Untitled number'
}
function groupNameOf(classId: number): string {
  return classMap.value.get(classId)?.name ?? 'Unknown group'
}

// gender dot color for the grid (mirrors FormationEditor's palette).
function dotColor(g?: Gender | null): string {
  return g === 'Boys' ? '#2563eb' : g === 'Girls' ? '#db2777' : '#94a3b8'
}

// --- Cast picker (plan view) roster + group filter ---
const rosterFilterClassId = ref<number | null>(null)
const pickerRoster = computed(() =>
  rosterFilterClassId.value == null
    ? [...students.value].sort(byName)
    : [...(groupRosters.value[rosterFilterClassId.value] ?? [])].sort(byName),
)

// --- Data loading ---
async function safeGet<T>(url: string, params?: Record<string, unknown>): Promise<T[]> {
  try {
    const { data } = await api.get<T[]>(url, params ? { params } : undefined)
    return Array.isArray(data) ? data : []
  } catch {
    return []
  }
}

async function loadGroupRosters() {
  const entries = await Promise.all(
    classes.value.map(
      async (c) => [c.id, await safeGet<Student>('/students', { classId: c.id })] as const,
    ),
  )
  groupRosters.value = Object.fromEntries(entries)
}

async function loadAll() {
  const studioId = studioStore.selectedStudioId
  if (!studioId) {
    classes.value = []
    routines.value = []
    students.value = []
    allCasts.value = []
    groupRosters.value = {}
    showProgram.value = []
    showSections.value = []
    selectedRoutineId.value = null
    return
  }
  const [cls, rts, studs, csts, prog, secs] = await Promise.all([
    safeGet<DanceClass>('/classes', { studioId }),
    safeGet<Routine>('/routines', { studioId }),
    safeGet<Student>('/students', { studioId }),
    safeGet<RoutineCast>('/routinecast', { studioId }),
    safeGet<ShowProgram>('/showprogram', { studioId }),
    safeGet<ShowSection>('/showsections', { studioId }),
  ])
  classes.value = cls
  routines.value = rts
  students.value = studs
  allCasts.value = csts
  showProgram.value = prog
  showSections.value = secs
  await loadGroupRosters()
  if (!routines.value.some((r) => r.id === selectedRoutineId.value)) {
    selectedRoutineId.value = routines.value[0]?.id ?? null
  }
}

watch(() => studioStore.selectedStudioId, loadAll)
watch(selectedRoutineId, () => {
  rosterFilterClassId.value = null
})

onMounted(async () => {
  if (studioStore.studios.length === 0) {
    try {
      await studioStore.fetchStudios()
    } catch {
      /* graceful: empty */
    }
  }
  await loadAll()
})

// --- Number create / rename / delete / move ---
const newNumberName = ref('')
const newNumberGroupId = ref<number | null>(null)

watch(
  classes,
  () => {
    if (!classes.value.some((c) => c.id === newNumberGroupId.value)) {
      newNumberGroupId.value = classes.value[0]?.id ?? null
    }
  },
  { immediate: true },
)

async function addNumber() {
  const name = newNumberName.value.trim()
  const classId = newNumberGroupId.value
  if (!name || !classId) return
  const payload: Omit<Routine, 'id'> = {
    classId,
    songTitle: name,
    artist: null,
    videoUrl: null,
    choreographyNotes: null,
  }
  try {
    const { data } = await api.post<Routine>('/routines', payload)
    routines.value.push(data)
    selectedRoutineId.value = data.id
    newNumberName.value = ''
    toast.success('Number added')
  } catch {
    /* api.ts surfaces the error toast */
  }
}

async function renameNumber(routine: Routine) {
  try {
    await api.put(`/routines/${routine.id}`, routine)
  } catch {
    /* local only */
  }
}

// Click-to-reuse an existing costume (exact match, no typo drift).
function setCostume(routine: Routine, label: string) {
  routine.costumeLabel = label
  renameNumber(routine)
}
function isSameCostumeLabel(routine: Routine | null, label: string): boolean {
  return (routine?.costumeLabel ?? '').trim().toLowerCase() === label.toLowerCase()
}

async function moveNumberGroup(routine: Routine, classId: number) {
  if (routine.classId === classId) return
  routine.classId = classId
  routines.value = [...routines.value]
  try {
    await api.put(`/routines/${routine.id}`, routine)
  } catch {
    /* keep optimistic local change */
  }
}

async function deleteNumber(routine: Routine) {
  if (
    !(await confirm({
      title: `Delete “${routine.songTitle || 'this number'}”?`,
      message: 'This removes the number, its cast, and its formations.',
      confirmText: 'Delete',
      destructive: true,
    }))
  )
    return
  try {
    await api.delete(`/routines/${routine.id}`)
    routines.value = routines.value.filter((r) => r.id !== routine.id)
    allCasts.value = allCasts.value.filter((c) => c.routineId !== routine.id) // cascaded server-side
    if (selectedRoutineId.value === routine.id) {
      selectedRoutineId.value = routines.value[0]?.id ?? null
    }
    toast.success('Number deleted')
  } catch {
    /* api.ts surfaces the error toast */
  }
}

// --- Cast add / remove (single + bulk) ---
async function setCast(routineId: number, studentId: number, on: boolean) {
  const has = castByRoutine.value.get(routineId)?.has(studentId) ?? false
  if (on === has) return
  if (on) {
    allCasts.value.push({ routineId, studentId })
    try {
      await api.post('/routinecast', { routineId, studentId })
    } catch {
      /* api.ts surfaces the error toast */
    }
  } else {
    allCasts.value = allCasts.value.filter(
      (c) => !(c.routineId === routineId && c.studentId === studentId),
    )
    try {
      await api.delete('/routinecast', { params: { routineId, studentId } })
    } catch {
      /* api.ts surfaces the error toast */
    }
  }
}
function toggleCast(routineId: number, studentId: number) {
  setCast(routineId, studentId, !(castByRoutine.value.get(routineId)?.has(studentId)))
}

const bulkGroupId = ref<number | null>(null)
const copyFromId = ref<number | null>(null)
// Collapsed by default so the cast panel stays out of the way until you edit it.
const castExpanded = ref(false)

async function addGroupToNumber() {
  const routineId = selectedRoutineId.value
  const classId = bulkGroupId.value
  if (!routineId || !classId) return
  const current = castOf(routineId)
  const toAdd = (groupRosters.value[classId] ?? []).filter((s) => !current.has(s.id))
  if (toAdd.length === 0) {
    toast.success('Everyone in that group is already cast')
    return
  }
  for (const s of toAdd) allCasts.value.push({ routineId, studentId: s.id })
  await Promise.all(
    toAdd.map((s) =>
      api.post('/routinecast', { routineId, studentId: s.id }).catch(() => {}),
    ),
  )
  toast.success(`Added ${toAdd.length} from ${groupNameOf(classId)}`)
}

async function copyCast() {
  const routineId = selectedRoutineId.value
  const sourceId = copyFromId.value
  if (!routineId || !sourceId) return
  const current = castOf(routineId)
  const toAdd = [...castOf(sourceId)].filter((id) => !current.has(id))
  if (toAdd.length === 0) {
    toast.success('Nothing new to copy')
    return
  }
  for (const id of toAdd) allCasts.value.push({ routineId, studentId: id })
  await Promise.all(
    toAdd.map((id) => api.post('/routinecast', { routineId, studentId: id }).catch(() => {})),
  )
  copyFromId.value = null
  toast.success(`Copied ${toAdd.length} dancer${toAdd.length === 1 ? '' : 's'}`)
}

async function clearCast() {
  const routineId = selectedRoutineId.value
  if (!routineId) return
  const ids = [...castOf(routineId)]
  if (ids.length === 0) return
  if (
    !(await confirm({
      title: 'Clear the entire cast for this number?',
      confirmText: 'Clear cast',
      destructive: true,
    }))
  )
    return
  allCasts.value = allCasts.value.filter((c) => c.routineId !== routineId)
  await Promise.all(
    ids.map((id) => api.delete('/routinecast', { params: { routineId, studentId: id } }).catch(() => {})),
  )
  toast.success('Cast cleared')
}

// --- Per-dancer detail (overview) ---
const focusStudentId = ref<number | null>(null)
const focusStudent = computed(() =>
  focusStudentId.value == null ? null : (studentMap.value.get(focusStudentId.value) ?? null),
)
const focusConflictNames = computed(() => {
  if (focusStudentId.value == null) return [] as string[]
  const ids = conflictsByStudent.value.get(focusStudentId.value)
  return ids ? [...ids].map(numberTitleOf).sort() : []
})

function openNumber(routineId: number) {
  selectedRoutineId.value = routineId
  view.value = 'plan'
}

// --- Per-number detail drawer (edit a number's cast from the overview) ---
const focusNumberId = ref<number | null>(null)
const focusNumber = computed(() =>
  focusNumberId.value == null
    ? null
    : (routines.value.find((r) => r.id === focusNumberId.value) ?? null),
)
function openNumberDrawer(id: number) {
  focusStudentId.value = null // only one drawer open at a time
  focusNumberId.value = id
}
function arrangeNumber(id: number) {
  focusNumberId.value = null
  openNumber(id)
}
// Cast members of the focused number who quick-change in/out of it.
const focusNumberConflictNames = computed(() => {
  const rid = focusNumberId.value
  if (rid == null) return [] as string[]
  const names: string[] = []
  for (const sid of castOf(rid)) {
    if (conflictsByStudent.value.get(sid)?.has(rid)) {
      const s = studentMap.value.get(sid)
      if (s) names.push(`${s.firstName} ${s.lastName}`)
    }
  }
  return names.sort()
})
// Opening the dancer drawer closes the number drawer.
watch(focusStudentId, (v) => {
  if (v != null) focusNumberId.value = null
})

// --- Column hover highlight (read a number's cast down a tall grid) ---
const hoveredNumberId = ref<number | null>(null)
</script>

<template>
  <div class="flex h-full flex-col">
    <!-- Header -->
    <header class="border-b border-border px-4 py-3 sm:px-6">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 class="text-xl font-semibold tracking-tight">Musical Planner</h1>
          <p class="mt-0.5 text-sm text-muted-foreground">
            Plan a production: numbers organized by cast group, each with a hand-picked cast and
            stage formations.
          </p>
        </div>
        <div class="flex flex-wrap items-center gap-2">
          <!-- View toggle -->
          <div v-if="classes.length > 0" class="inline-flex rounded-md border border-border p-0.5">
            <button
              class="inline-flex items-center gap-1.5 rounded px-2.5 py-1 text-sm transition-colors"
              :class="view === 'plan' ? 'bg-accent font-medium' : 'text-muted-foreground hover:bg-accent'"
              @click="view = 'plan'"
            >
              <ListMusic class="h-4 w-4" /> Plan
            </button>
            <button
              class="inline-flex items-center gap-1.5 rounded px-2.5 py-1 text-sm transition-colors"
              :class="view === 'overview' ? 'bg-accent font-medium' : 'text-muted-foreground hover:bg-accent'"
              @click="view = 'overview'"
            >
              <LayoutGrid class="h-4 w-4" /> Cast overview
            </button>
          </div>
          <button
            v-if="classes.length > 0"
            class="inline-flex items-center gap-1.5 rounded-md border border-border px-3 py-2 text-sm hover:bg-accent disabled:opacity-50"
            :disabled="routines.length === 0"
            title="Cast list — every number with its dancers, in running order"
            @click="downloadCastSheet('number')"
          >
            <FileDown class="h-4 w-4" />
            Cast list
          </button>
          <button
            v-if="classes.length > 0"
            class="inline-flex items-center gap-1.5 rounded-md border border-border px-3 py-2 text-sm hover:bg-accent disabled:opacity-50"
            :disabled="routines.length === 0"
            title="Dancer schedules — each dancer and the numbers they're in"
            @click="downloadCastSheet('dancer')"
          >
            <FileDown class="h-4 w-4" />
            Dancer sheets
          </button>
          <RouterLink
            to="/recital"
            class="inline-flex items-center gap-1.5 rounded-md border border-border px-3 py-2 text-sm hover:bg-accent"
          >
            <ExternalLink class="h-4 w-4" />
            Show order
          </RouterLink>
        </div>
      </div>
    </header>

    <!-- No groups yet -->
    <div v-if="classes.length === 0" class="flex flex-1 items-center justify-center p-8">
      <div class="max-w-md rounded-lg border border-dashed border-border p-8 text-center">
        <Theater class="mx-auto h-8 w-8 text-muted-foreground" />
        <p class="mt-3 text-sm font-medium">No cast groups yet</p>
        <p class="mt-1 text-sm text-muted-foreground">
          Model your production as a studio and its cast groups (e.g. Gold, Silver) as classes.
          Create them in Studios &amp; Classes, then come back to plan numbers.
        </p>
        <RouterLink
          to="/settings/studios"
          class="mt-4 inline-flex items-center gap-1.5 rounded-md border border-border px-3 py-2 text-sm hover:bg-accent"
        >
          <Settings class="h-4 w-4" />
          Manage groups
        </RouterLink>
      </div>
    </div>

    <!-- ============================= PLAN VIEW ============================= -->
    <div
      v-else-if="view === 'plan'"
      class="grid flex-1 grid-cols-1 gap-px overflow-hidden bg-border lg:grid-cols-[minmax(0,20rem)_1fr]"
    >
      <!-- LEFT: numbers by group -->
      <div class="flex flex-col overflow-y-auto bg-background p-4 sm:p-5">
        <!-- Add a number -->
        <div class="mb-4 space-y-2 rounded-md border border-border bg-muted/30 p-3">
          <p class="text-xs font-medium text-muted-foreground">New number</p>
          <input
            v-model="newNumberName"
            placeholder="Number title — e.g. “Deck the Halls”"
            class="w-full rounded-md border border-border bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
            @keyup.enter="addNumber"
          />
          <div class="flex items-center gap-2">
            <select
              v-model="newNumberGroupId"
              class="min-w-0 flex-1 rounded-md border border-border bg-background px-2 py-1.5 text-sm"
            >
              <option v-for="c in classes" :key="c.id" :value="c.id">{{ c.name }}</option>
            </select>
            <button
              class="inline-flex shrink-0 items-center gap-1 rounded-md bg-primary px-3 py-1.5 text-sm font-medium text-primary-foreground hover:opacity-90 disabled:opacity-50"
              :disabled="newNumberName.trim() === '' || newNumberGroupId === null"
              @click="addNumber"
            >
              <Plus class="h-4 w-4" /> Add
            </button>
          </div>
        </div>

        <!-- Grouped list -->
        <div class="space-y-4">
          <div v-for="grp in numbersByGroup" :key="grp.group.id">
            <p class="mb-1.5 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
              {{ grp.group.name }}
            </p>
            <p v-if="grp.numbers.length === 0" class="px-1 text-xs text-muted-foreground">
              No numbers in this group yet.
            </p>
            <ul v-else class="space-y-1">
              <li v-for="r in grp.numbers" :key="r.id">
                <button
                  class="flex w-full items-center gap-2 rounded-md border px-2.5 py-1.5 text-left text-sm transition-colors"
                  :class="
                    r.id === selectedRoutineId
                      ? 'border-foreground bg-accent font-medium'
                      : 'border-border hover:bg-accent'
                  "
                  @click="selectedRoutineId = r.id"
                >
                  <span class="min-w-0 flex-1 truncate">{{ r.songTitle || 'Untitled number' }}</span>
                  <span class="shrink-0 text-xs text-muted-foreground">{{ castOf(r.id).size }}</span>
                </button>
              </li>
            </ul>
          </div>
        </div>
      </div>

      <!-- RIGHT: selected number — cast + formations -->
      <div v-if="selectedRoutine" class="flex flex-col overflow-y-auto bg-background p-4 sm:p-6">
        <!-- Number title + home group + delete -->
        <div class="mb-4 flex flex-wrap items-end gap-3">
          <label class="min-w-[12rem] flex-1 space-y-1">
            <span class="text-xs text-muted-foreground">Number title</span>
            <input
              v-model="selectedRoutine.songTitle"
              class="w-full rounded-md border border-border bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
              @change="renameNumber(selectedRoutine)"
            />
          </label>
          <label class="space-y-1">
            <span class="text-xs text-muted-foreground">Home group</span>
            <select
              :value="selectedRoutine.classId"
              class="rounded-md border border-border bg-background px-2 py-1.5 text-sm"
              @change="
                moveNumberGroup(selectedRoutine, Number(($event.target as HTMLSelectElement).value))
              "
            >
              <option v-for="c in classes" :key="c.id" :value="c.id">{{ c.name }}</option>
            </select>
          </label>
          <label class="min-w-[10rem] space-y-1">
            <span class="text-xs text-muted-foreground">Costume</span>
            <input
              v-model="selectedRoutine.costumeLabel"
              placeholder="e.g. Red velvet dress"
              title="Numbers with the same costume won't flag a quick change back-to-back"
              class="w-full rounded-md border border-border bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
              @change="renameNumber(selectedRoutine)"
            />
          </label>
          <button
            class="inline-flex items-center gap-1.5 rounded-md border border-border px-2.5 py-1.5 text-sm text-muted-foreground hover:bg-destructive/10 hover:text-destructive"
            @click="deleteNumber(selectedRoutine)"
          >
            <Trash2 class="h-3.5 w-3.5" /> Delete
          </button>
        </div>

        <!-- Click-to-reuse an existing costume (avoids retyping / typos) -->
        <div v-if="costumeLabels.length" class="-mt-2 mb-4 flex flex-wrap items-center gap-1">
          <span class="text-[11px] text-muted-foreground">Costumes in use:</span>
          <button
            v-for="c in costumeLabels"
            :key="c"
            type="button"
            class="rounded-full border px-2 py-0.5 text-[11px] transition-colors"
            :class="
              isSameCostumeLabel(selectedRoutine, c)
                ? 'border-foreground bg-foreground text-background'
                : 'border-border text-muted-foreground hover:bg-accent'
            "
            @click="setCostume(selectedRoutine, c)"
          >
            {{ c }}
          </button>
        </div>

        <!-- Cast editor (collapsible) -->
        <div class="mb-5 rounded-md border border-border bg-muted/30 p-3">
          <button
            class="flex w-full items-center gap-1.5 text-left text-sm font-medium"
            @click="castExpanded = !castExpanded"
          >
            <ChevronDown
              class="h-4 w-4 shrink-0 text-muted-foreground transition-transform"
              :class="{ '-rotate-90': !castExpanded }"
            />
            <Users class="h-4 w-4" />
            Cast
            <span class="text-xs font-normal text-muted-foreground">({{ selectedCast.size }} cast)</span>
            <span class="ml-auto text-xs font-normal text-muted-foreground">{{
              castExpanded ? 'Hide' : 'Edit'
            }}</span>
          </button>

          <div v-show="castExpanded" class="mt-2.5 space-y-2.5">
            <label class="flex items-center gap-1.5 text-xs text-muted-foreground">
              Filter roster
              <select
                v-model="rosterFilterClassId"
                class="rounded-md border border-border bg-background px-2 py-1 text-xs"
              >
                <option :value="null">All groups</option>
                <option v-for="c in classes" :key="c.id" :value="c.id">{{ c.name }}</option>
              </select>
            </label>

            <!-- Bulk actions -->
            <div class="flex flex-wrap items-center gap-1.5 border-y border-border/60 py-2">
            <select
              v-model="bulkGroupId"
              class="rounded-md border border-border bg-background px-2 py-1 text-xs"
            >
              <option :value="null" disabled>Add whole group…</option>
              <option v-for="c in classes" :key="c.id" :value="c.id">{{ c.name }}</option>
            </select>
            <button
              class="inline-flex items-center gap-1 rounded-md border border-border px-2 py-1 text-xs hover:bg-accent disabled:opacity-50"
              :disabled="bulkGroupId === null"
              @click="addGroupToNumber"
            >
              <Plus class="h-3.5 w-3.5" /> Add group
            </button>
            <span class="mx-0.5 text-muted-foreground">·</span>
            <select
              v-model="copyFromId"
              class="max-w-[12rem] rounded-md border border-border bg-background px-2 py-1 text-xs"
            >
              <option :value="null" disabled>Copy cast from…</option>
              <template v-for="r in routines" :key="r.id">
                <option v-if="r.id !== selectedRoutineId" :value="r.id">
                  {{ r.songTitle || 'Untitled number' }}
                </option>
              </template>
            </select>
            <button
              class="inline-flex items-center gap-1 rounded-md border border-border px-2 py-1 text-xs hover:bg-accent disabled:opacity-50"
              :disabled="copyFromId === null"
              @click="copyCast"
            >
              <Copy class="h-3.5 w-3.5" /> Copy
            </button>
            <button
              class="ml-auto inline-flex items-center gap-1 rounded-md border border-border px-2 py-1 text-xs text-muted-foreground hover:bg-destructive/10 hover:text-destructive disabled:opacity-50"
              :disabled="selectedCast.size === 0"
              @click="clearCast"
            >
              <Trash2 class="h-3.5 w-3.5" /> Clear
            </button>
          </div>

          <p v-if="pickerRoster.length === 0" class="text-xs text-muted-foreground">
            No students {{ rosterFilterClassId == null ? 'in this studio' : 'in this group' }} yet.
          </p>
          <div v-else class="flex flex-wrap gap-1.5">
            <button
              v-for="s in pickerRoster"
              :key="s.id"
              class="rounded-full border px-2.5 py-0.5 text-xs transition-colors"
              :class="
                selectedCast.has(s.id)
                  ? 'border-foreground bg-foreground text-background'
                  : 'border-border text-muted-foreground hover:bg-accent'
              "
              @click="toggleCast(selectedRoutineId!, s.id)"
            >
              {{ s.firstName }} {{ s.lastName }}
            </button>
          </div>
            <p class="text-[11px] text-muted-foreground">
              Cast overrides group participation for this number's formations and back-to-back quick
              changes.
            </p>
          </div>
        </div>

        <!-- Formation editor driven by the cast -->
        <FormationEditor
          :routine-id="selectedRoutineId"
          :dancers="castStudents"
          :empty-hint="`Add cast members above to arrange them on stage. Home group: ${groupNameOf(selectedRoutine.classId)}.`"
        />
      </div>

      <!-- Nothing selected -->
      <div v-else class="flex flex-col items-center justify-center bg-background p-8 text-center">
        <Theater class="h-6 w-6 text-muted-foreground" />
        <p class="mt-2 text-sm font-medium">No number selected</p>
        <p class="mt-1 max-w-xs text-sm text-muted-foreground">
          Add a number on the left, then pick its cast and map the stage.
        </p>
      </div>
    </div>

    <!-- =========================== OVERVIEW VIEW =========================== -->
    <div v-else class="flex-1 overflow-auto bg-background p-4 sm:p-6">
      <div v-if="flatNumbers.length === 0" class="flex flex-col items-center justify-center p-10 text-center">
        <LayoutGrid class="h-6 w-6 text-muted-foreground" />
        <p class="mt-2 text-sm font-medium">No numbers yet</p>
        <p class="mt-1 max-w-xs text-sm text-muted-foreground">
          Add numbers in the Plan view to see the cast overview.
        </p>
      </div>

      <template v-else>
        <!-- Controls: column ordering + dancer-group filter -->
        <div class="mb-3 flex flex-wrap items-center gap-x-4 gap-y-2">
          <div class="inline-flex items-center gap-1.5">
            <span class="text-xs text-muted-foreground">Numbers:</span>
            <div class="inline-flex rounded-md border border-border p-0.5">
              <button
                class="rounded px-2 py-0.5 text-xs transition-colors"
                :class="columnMode === 'running' ? 'bg-accent font-medium' : 'text-muted-foreground hover:bg-accent'"
                @click="columnMode = 'running'"
              >
                Running order
              </button>
              <button
                class="rounded px-2 py-0.5 text-xs transition-colors"
                :class="columnMode === 'group' ? 'bg-accent font-medium' : 'text-muted-foreground hover:bg-accent'"
                @click="columnMode = 'group'"
              >
                By group
              </button>
            </div>
          </div>
          <div v-if="rowGroupsAll.length > 1" class="inline-flex flex-wrap items-center gap-1.5">
            <span class="text-xs text-muted-foreground">Groups:</span>
            <button
              v-for="rg in rowGroupsAll"
              :key="rg.key"
              class="rounded-full border px-2 py-0.5 text-xs transition-colors"
              :class="
                hiddenRowGroups.has(rg.key)
                  ? 'border-border text-muted-foreground/50 line-through'
                  : 'border-foreground bg-foreground text-background'
              "
              @click="toggleRowGroup(rg.key)"
            >
              {{ rg.name }}
            </button>
          </div>
          <div class="ml-auto inline-flex items-center gap-1.5">
            <Search class="h-3.5 w-3.5 text-muted-foreground" />
            <input
              v-model="dancerSearch"
              placeholder="Find a dancer…"
              class="w-44 rounded-md border border-border bg-background px-2 py-1 text-xs focus:outline-none focus:ring-1 focus:ring-ring"
            />
          </div>
        </div>

        <div class="mb-3 flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-muted-foreground">
          <span class="inline-flex items-center gap-1.5">
            <span class="inline-block h-2.5 w-2.5 rounded-full" style="background: #64748b" /> in a number
          </span>
          <span class="inline-flex items-center gap-1.5">
            <span class="rounded bg-amber-500/15 px-1.5 py-0.5 font-medium text-amber-700 dark:text-amber-400">6</span>
            high load
          </span>
          <span class="inline-flex items-center gap-1.5">
            <AlertTriangle class="h-3.5 w-3.5 text-amber-600 dark:text-amber-400" /> has a quick change
          </span>
          <span class="ml-auto">Click a cell to add/remove · a name for details · a number to open it</span>
        </div>

        <!-- Matrix -->
        <div class="overflow-x-auto rounded-lg border border-border">
          <table class="border-collapse text-xs" @mouseleave="hoveredNumberId = null">
            <thead>
              <!-- group header row -->
              <tr>
                <th class="sticky left-0 z-10 border-b border-r border-border bg-muted/40 p-2"></th>
                <th
                  v-for="g in matrixColumns"
                  :key="g.key"
                  :colspan="g.numbers.length"
                  class="border-b border-l border-border bg-muted/40 px-2 py-1 text-center font-semibold"
                >
                  {{ g.label }}
                </th>
              </tr>
              <!-- number title row -->
              <tr>
                <th
                  class="sticky left-0 z-10 border-b border-r border-border bg-muted/40 px-2 py-1 text-left font-medium"
                >
                  Dancer
                </th>
                <th
                  v-for="n in flatNumbers"
                  :key="n.id"
                  class="h-28 w-8 border-b border-l border-border align-bottom transition-colors"
                  :class="hoveredNumberId === n.id ? 'bg-primary/10' : 'bg-muted/40'"
                  :title="n.songTitle || 'Untitled number'"
                  @mouseenter="hoveredNumberId = n.id"
                >
                  <button
                    class="flex h-full w-full flex-col items-center justify-end gap-1 pb-1 hover:bg-accent"
                    @click="openNumberDrawer(n.id)"
                  >
                    <span
                      class="max-h-24 truncate font-medium [writing-mode:vertical-rl]"
                      style="transform: rotate(180deg)"
                    >
                      {{ n.songTitle || 'Untitled' }}
                    </span>
                    <span class="tabular-nums text-muted-foreground">{{ castOf(n.id).size }}</span>
                  </button>
                </th>
              </tr>
            </thead>
            <tbody>
              <template v-for="sec in matrixRows" :key="sec.key">
                <tr>
                  <td
                    :colspan="flatNumbers.length + 1"
                    class="sticky left-0 border-b border-t border-border bg-muted/20 px-2 py-1 font-semibold uppercase tracking-wide text-muted-foreground"
                  >
                    {{ sec.name }}
                  </td>
                </tr>
                <tr
                  v-for="st in sec.students"
                  :key="`${sec.key}-${st.id}`"
                  class="transition-colors"
                  :class="[
                    isSearchMatch(st.id) ? 'bg-primary/10' : 'hover:bg-accent/40',
                    isDimmed(st.id) ? 'opacity-40' : '',
                  ]"
                >
                  <th
                    class="sticky left-0 z-10 border-b border-r border-border bg-background px-2 py-1 text-left font-normal"
                  >
                    <button
                      class="inline-flex max-w-[12rem] items-center gap-1.5 hover:underline"
                      :class="countOf(st.id) === 0 ? 'text-muted-foreground/60' : ''"
                      @click="focusStudentId = st.id"
                    >
                      <span class="truncate">{{ st.firstName }} {{ st.lastName }}</span>
                      <AlertTriangle
                        v-if="hasConflict(st.id)"
                        class="h-3.5 w-3.5 shrink-0 text-amber-600 dark:text-amber-400"
                      />
                      <span
                        class="shrink-0 rounded px-1.5 py-0.5 text-[11px] tabular-nums"
                        :class="
                          isBusy(st.id)
                            ? 'bg-amber-500/15 font-semibold text-amber-700 dark:text-amber-400'
                            : 'text-muted-foreground'
                        "
                      >
                        {{ countOf(st.id) }}
                      </span>
                    </button>
                  </th>
                  <td
                    v-for="n in flatNumbers"
                    :key="n.id"
                    class="cursor-pointer border-b border-l border-border p-0 text-center transition-colors hover:bg-accent"
                    :class="hoveredNumberId === n.id ? 'bg-primary/5' : ''"
                    @mouseenter="hoveredNumberId = n.id"
                    @click="toggleCast(n.id, st.id)"
                  >
                    <span
                      v-if="castOf(n.id).has(st.id)"
                      class="mx-auto block h-3 w-3 rounded-full"
                      :style="{ background: dotColor(st.gender) }"
                    />
                  </td>
                </tr>
              </template>
            </tbody>
          </table>
        </div>
        <p v-if="matrixRows.length === 0" class="mt-2 text-xs text-muted-foreground">
          All groups are hidden — tap a group chip above to show dancers.
        </p>

        <!-- Quick-change conflicts (back-to-back in the running order) -->
        <details class="mt-4 rounded-md border border-border bg-muted/20 p-3" open>
          <summary class="cursor-pointer select-none text-sm font-medium">
            <span class="inline-flex items-center gap-1.5">
              <AlertTriangle class="h-4 w-4 text-amber-600 dark:text-amber-400" />
              Quick changes ({{ quickChangeList.length }})
            </span>
          </summary>
          <p v-if="showProgram.length === 0" class="mt-2 text-xs text-muted-foreground">
            Set a running order in
            <RouterLink to="/recital" class="underline">Show order</RouterLink>
            to see back-to-back costume changes here.
          </p>
          <p v-else-if="quickChangeList.length === 0" class="mt-2 text-xs text-muted-foreground">
            No costume changes flagged. A quick change only shows when two back-to-back numbers
            have <em>different</em> costume labels — set a Costume on each number to track this.
          </p>
          <div v-else class="mt-2 space-y-1">
            <div v-for="(qc, i) in quickChangeList" :key="i" class="text-xs leading-relaxed">
              <span class="font-medium">{{ qc.a }}</span>
              <span class="text-muted-foreground"> → </span>
              <span class="font-medium">{{ qc.b }}</span>
              <span class="text-muted-foreground">
                — {{ qc.dancers.join(', ') }}
                ({{ qc.dancers.length }} dancer{{ qc.dancers.length === 1 ? '' : 's' }})
              </span>
            </div>
          </div>
        </details>

        <!-- Not yet cast -->
        <div
          v-if="uncastStudents.length > 0"
          class="mt-4 rounded-md border border-border bg-muted/20 p-3"
        >
          <p class="mb-1.5 inline-flex items-center gap-1.5 text-sm font-medium">
            <UserX class="h-4 w-4 text-muted-foreground" />
            Not yet cast ({{ uncastStudents.length }})
          </p>
          <div class="flex flex-wrap gap-1.5">
            <button
              v-for="s in uncastStudents"
              :key="s.id"
              class="rounded-full border border-border px-2.5 py-0.5 text-xs text-muted-foreground hover:bg-accent"
              @click="focusStudentId = s.id"
            >
              {{ s.firstName }} {{ s.lastName }}
            </button>
          </div>
        </div>
      </template>
    </div>

    <!-- ===================== PER-DANCER DETAIL DRAWER ===================== -->
    <div
      v-if="focusStudent"
      class="fixed inset-0 z-50 flex justify-end bg-black/30"
      @click.self="focusStudentId = null"
    >
      <div class="flex h-full w-full max-w-sm flex-col border-l border-border bg-background shadow-xl">
        <div class="flex items-center justify-between border-b border-border px-4 py-3">
          <div>
            <p class="text-sm font-semibold">{{ focusStudent.firstName }} {{ focusStudent.lastName }}</p>
            <p class="text-xs text-muted-foreground">
              In {{ countOf(focusStudent.id) }} number{{ countOf(focusStudent.id) === 1 ? '' : 's' }}
            </p>
          </div>
          <button
            class="rounded p-1.5 text-muted-foreground hover:bg-accent"
            aria-label="Close"
            @click="focusStudentId = null"
          >
            <X class="h-4 w-4" />
          </button>
        </div>

        <div class="flex-1 overflow-y-auto p-4">
          <div
            v-if="focusConflictNames.length > 0"
            class="mb-4 rounded-md border border-amber-500/40 bg-amber-500/10 p-2.5 text-xs text-amber-700 dark:text-amber-400"
          >
            <p class="inline-flex items-center gap-1.5 font-semibold">
              <AlertTriangle class="h-3.5 w-3.5" /> Quick change
            </p>
            <p class="mt-0.5">Back-to-back in the running order: {{ focusConflictNames.join(', ') }}.</p>
          </div>

          <p class="mb-2 text-xs font-medium text-muted-foreground">
            Tap a number to add or remove {{ focusStudent.firstName }}
          </p>
          <div class="space-y-3">
            <div v-for="grp in numbersByGroup" :key="grp.group.id" v-show="grp.numbers.length > 0">
              <p class="mb-1 text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">
                {{ grp.group.name }}
              </p>
              <div class="flex flex-wrap gap-1.5">
                <button
                  v-for="r in grp.numbers"
                  :key="r.id"
                  class="rounded-full border px-2.5 py-0.5 text-xs transition-colors"
                  :class="
                    castOf(r.id).has(focusStudent.id)
                      ? 'border-foreground bg-foreground text-background'
                      : 'border-border text-muted-foreground hover:bg-accent'
                  "
                  @click="toggleCast(r.id, focusStudent.id)"
                >
                  {{ r.songTitle || 'Untitled' }}
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- ===================== NUMBER DETAIL DRAWER ===================== -->
    <div
      v-if="focusNumber"
      class="fixed inset-0 z-50 flex justify-end bg-black/30"
      @click.self="focusNumberId = null"
    >
      <div class="flex h-full w-full max-w-sm flex-col border-l border-border bg-background shadow-xl">
        <div class="flex items-center justify-between border-b border-border px-4 py-3">
          <div class="min-w-0">
            <p class="truncate text-sm font-semibold">
              {{ focusNumber.songTitle || 'Untitled number' }}
            </p>
            <p class="text-xs text-muted-foreground">
              {{ groupNameOf(focusNumber.classId) }} · {{ castOf(focusNumber.id).size }} cast
            </p>
          </div>
          <button
            class="rounded p-1.5 text-muted-foreground hover:bg-accent"
            aria-label="Close"
            @click="focusNumberId = null"
          >
            <X class="h-4 w-4" />
          </button>
        </div>

        <div class="flex-1 overflow-y-auto p-4">
          <div
            v-if="focusNumberConflictNames.length > 0"
            class="mb-4 rounded-md border border-amber-500/40 bg-amber-500/10 p-2.5 text-xs text-amber-700 dark:text-amber-400"
          >
            <p class="inline-flex items-center gap-1.5 font-semibold">
              <AlertTriangle class="h-3.5 w-3.5" /> Quick change
            </p>
            <p class="mt-0.5">Cast here and in a back-to-back number: {{ focusNumberConflictNames.join(', ') }}.</p>
          </div>

          <label class="mb-2 block space-y-1">
            <span class="text-xs font-medium text-muted-foreground">Costume</span>
            <input
              v-model="focusNumber.costumeLabel"
              placeholder="e.g. Red velvet dress"
              class="w-full rounded-md border border-border bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
              @change="renameNumber(focusNumber)"
            />
          </label>
          <div v-if="costumeLabels.length" class="mb-4 flex flex-wrap items-center gap-1">
            <span class="text-[11px] text-muted-foreground">In use:</span>
            <button
              v-for="c in costumeLabels"
              :key="c"
              type="button"
              class="rounded-full border px-2 py-0.5 text-[11px] transition-colors"
              :class="
                isSameCostumeLabel(focusNumber, c)
                  ? 'border-foreground bg-foreground text-background'
                  : 'border-border text-muted-foreground hover:bg-accent'
              "
              @click="setCostume(focusNumber, c)"
            >
              {{ c }}
            </button>
          </div>

          <button
            class="mb-4 inline-flex items-center gap-1.5 rounded-md border border-border px-2.5 py-1.5 text-xs hover:bg-accent"
            @click="arrangeNumber(focusNumber.id)"
          >
            <ListMusic class="h-3.5 w-3.5" /> Open in Plan to arrange formations
          </button>

          <p class="mb-2 text-xs font-medium text-muted-foreground">
            Tap a dancer to add or remove them from this number
          </p>
          <div class="space-y-3">
            <div v-for="rg in rowGroupsAll" :key="rg.key">
              <p class="mb-1 text-[11px] font-semibold uppercase tracking-wide text-muted-foreground">
                {{ rg.name }}
              </p>
              <div class="flex flex-wrap gap-1.5">
                <button
                  v-for="s in rg.students"
                  :key="s.id"
                  class="rounded-full border px-2.5 py-0.5 text-xs transition-colors"
                  :class="
                    castOf(focusNumber.id).has(s.id)
                      ? 'border-foreground bg-foreground text-background'
                      : 'border-border text-muted-foreground hover:bg-accent'
                  "
                  @click="toggleCast(focusNumber.id, s.id)"
                >
                  {{ s.firstName }} {{ s.lastName }}
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

  </div>
</template>
