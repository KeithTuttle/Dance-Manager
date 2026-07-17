<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import {
  GripVertical,
  AlertTriangle,
  Theater,
  Plus,
  Trash2,
  FileDown,
  FolderPlus,
  ArrowUp,
  ArrowDown,
  Users,
} from 'lucide-vue-next'
import { api } from '@/lib/api'
import { confirm } from '@/lib/confirm'
import { toast } from '@/lib/toast'
import { useStudioStore } from '@/stores/studio'
import type {
  ShowProgram,
  ShowSection,
  Routine,
  DanceClass,
  RecitalParticipation,
  Student,
  RoutineCast,
} from '@/types'

const studioStore = useStudioStore()

const program = ref<ShowProgram[]>([])
const sections = ref<ShowSection[]>([])
const routines = ref<Routine[]>([])
const classes = ref<DanceClass[]>([])
const participations = ref<RecitalParticipation[]>([])
const students = ref<Student[]>([])
const casts = ref<RoutineCast[]>([])
const loading = ref(false)

// Routines not yet in the show order, available to add.
const availableRoutines = computed(() => {
  const usedIds = new Set(program.value.map((p) => p.routineId).filter((id) => id != null))
  return routines.value.filter((r) => !usedIds.has(r.id))
})
const routineToAdd = ref<number | null>(null)
const quickAddTitle = ref('')

const routineMap = computed(() => new Map(routines.value.map((r) => [r.id, r])))
const classMap = computed(() => new Map(classes.value.map((c) => [c.id, c])))
const studentMap = computed(() => new Map(students.value.map((s) => [s.id, s])))

/** studentIds actively participating in a given class. */
const participatingByClass = computed(() => {
  const map = new Map<number, Set<number>>()
  for (const p of participations.value) {
    if (!p.isParticipating) continue
    if (!map.has(p.classId)) map.set(p.classId, new Set())
    map.get(p.classId)!.add(p.studentId)
  }
  return map
})

/** studentIds explicitly cast in a given routine (musical numbers). */
const castByRoutine = computed(() => {
  const map = new Map<number, Set<number>>()
  for (const c of casts.value) {
    if (!map.has(c.routineId)) map.set(c.routineId, new Set())
    map.get(c.routineId)!.add(c.studentId)
  }
  return map
})

function rowInfo(entry: ShowProgram): { routine?: Routine; className: string | null; title: string } {
  const routine = entry.routineId != null ? routineMap.value.get(entry.routineId) : undefined
  if (entry.routineId != null) {
    const cls = routine ? classMap.value.get(routine.classId) : undefined
    return {
      routine,
      className: cls?.name ?? 'Unknown class',
      title: routine?.songTitle || `Routine #${entry.routineId}`,
    }
  }
  return { routine: undefined, className: null, title: entry.title || 'Untitled number' }
}

const byPos = (a: ShowProgram, b: ShowProgram) => a.orderPosition - b.orderPosition

/** Full display order (sections by orderIndex, then unassigned; entries by orderPosition). */
const orderedEntries = computed<ShowProgram[]>(() => {
  const result: ShowProgram[] = []
  for (const sec of [...sections.value].sort((a, b) => a.orderIndex - b.orderIndex)) {
    result.push(...program.value.filter((p) => p.sectionId === sec.id).sort(byPos))
  }
  result.push(...program.value.filter((p) => p.sectionId == null).sort(byPos))
  return result
})

/** entry.id -> running program number (1-based across the whole show). */
const numberOf = computed(() => {
  const m = new Map<number, number>()
  orderedEntries.value.forEach((e, i) => m.set(e.id, i + 1))
  return m
})

/** Display groups: each named section in order, then an "Unassigned" bucket. */
const displayGroups = computed(() => {
  const groups: { section: ShowSection | null; entries: ShowProgram[] }[] = []
  for (const sec of [...sections.value].sort((a, b) => a.orderIndex - b.orderIndex)) {
    groups.push({ section: sec, entries: program.value.filter((p) => p.sectionId === sec.id).sort(byPos) })
  }
  groups.push({ section: null, entries: program.value.filter((p) => p.sectionId == null).sort(byPos) })
  return groups
})

/** studentIds attached to a standalone/quick-add number. */
function studentIdsOf(entry: ShowProgram): number[] {
  if (!entry.studentIds) return []
  try {
    const parsed = JSON.parse(entry.studentIds)
    return Array.isArray(parsed) ? parsed.filter((n) => typeof n === 'number') : []
  } catch {
    return []
  }
}

/**
 * The dancers in a number: for a routine, its explicit cast (musical numbers)
 * if any, else the class's recital participation; for a standalone number, the
 * attached students. Mirrors the backend's StudentSet.
 */
function studentSetFor(entry: ShowProgram): Set<number> {
  if (entry.routineId != null) {
    const cast = castByRoutine.value.get(entry.routineId)
    if (cast && cast.size > 0) return cast
    const routine = routineMap.value.get(entry.routineId)
    return (routine && participatingByClass.value.get(routine.classId)) || new Set<number>()
  }
  return new Set(studentIdsOf(entry))
}

function nameOf(id: number): string {
  const s = studentMap.value.get(id)
  return s ? `${s.firstName} ${s.lastName}` : `Student #${id}`
}

/** Both entries are routine-linked and share the same non-empty costume => no change. */
function sameCostume(a: ShowProgram, b: ShowProgram): boolean {
  const ra = a.routineId != null ? routineMap.value.get(a.routineId) : undefined
  const rb = b.routineId != null ? routineMap.value.get(b.routineId) : undefined
  const ca = ra?.costumeLabel?.trim().toLowerCase()
  const cb = rb?.costumeLabel?.trim().toLowerCase()
  return !!ca && !!cb && ca === cb
}

/** Names sharing back-to-back numbers between an entry and the next in the same section. */
function quickChangeNames(entries: ShowProgram[], index: number): string[] {
  const cur = entries[index]
  const next = entries[index + 1]
  if (!cur || !next) return [] // last number in the section has nothing after it
  if (sameCostume(cur, next)) return [] // same costume — no change needed
  const setA = studentSetFor(cur)
  const setB = studentSetFor(next)
  if (setA.size === 0 || setB.size === 0) return []
  const names: string[] = []
  for (const id of setA) if (setB.has(id)) names.push(nameOf(id))
  return names
}

// Which standalone number's student picker is open (entry id) — or null.
const studentsOpenFor = ref<number | null>(null)

async function toggleStudent(entry: ShowProgram, studentId: number) {
  const current = studentIdsOf(entry)
  const next = current.includes(studentId)
    ? current.filter((id) => id !== studentId)
    : [...current, studentId]
  entry.studentIds = JSON.stringify(next)
  program.value = [...program.value]
  try {
    await api.put(`/showprogram/${entry.id}`, entry)
  } catch {
    /* api.ts surfaces the error toast */
  }
}

async function safeGet<T>(url: string): Promise<T[]> {
  try {
    const { data } = await api.get<T[]>(url)
    return Array.isArray(data) ? data : []
  } catch {
    return []
  }
}

async function load() {
  loading.value = true
  const studioId = studioStore.selectedStudioId
  const q = studioId ? `?studioId=${studioId}` : ''
  try {
    const [prog, secs, rts, cls, parts, studs, cst] = await Promise.all([
      safeGet<ShowProgram>(`/showprogram${q}`),
      safeGet<ShowSection>(`/showsections${q}`),
      safeGet<Routine>(`/routines${q}`),
      safeGet<DanceClass>(`/classes${q}`),
      safeGet<RecitalParticipation>('/recitalparticipation'),
      safeGet<Student>(`/students${q}`),
      safeGet<RoutineCast>(`/routinecast${q}`),
    ])
    program.value = prog
    sections.value = secs
    routines.value = rts
    classes.value = cls
    participations.value = parts
    students.value = studs
    casts.value = cst
  } finally {
    loading.value = false
  }
}

// --- Add numbers -------------------------------------------------------------
async function addToShow() {
  const routineId = routineToAdd.value
  if (routineId === null) return
  try {
    const { data } = await api.post<ShowProgram>('/showprogram', {
      routineId,
      studioId: studioStore.selectedStudioId,
      sectionId: null,
      orderPosition: program.value.length,
    })
    program.value.push(data)
    routineToAdd.value = null
    toast.success('Added to show order')
  } catch {
    /* api.ts surfaces the error toast */
  }
}

async function quickAdd() {
  const title = quickAddTitle.value.trim()
  if (!title) return
  try {
    const { data } = await api.post<ShowProgram>('/showprogram', {
      routineId: null,
      title,
      studioId: studioStore.selectedStudioId,
      sectionId: null,
      orderPosition: program.value.length,
    })
    program.value.push(data)
    quickAddTitle.value = ''
    toast.success('Added to show order')
  } catch {
    /* api.ts surfaces the error toast */
  }
}

async function removeFromShow(entry: ShowProgram) {
  if (
    !(await confirm({
      title: 'Remove this number from the show order?',
      confirmText: 'Remove',
      destructive: true,
    }))
  )
    return
  const prev = program.value
  program.value = program.value.filter((p) => p.id !== entry.id)
  try {
    await api.delete(`/showprogram/${entry.id}`)
    await persistOrder(orderedEntries.value)
    toast.success('Removed')
  } catch {
    program.value = prev
  }
}

// --- Ordering + section assignment ------------------------------------------
async function persistOrder(order: ShowProgram[]) {
  order.forEach((e, i) => (e.orderPosition = i))
  program.value = [...program.value]
  try {
    await api.put(
      '/showprogram/reorder',
      order.map((e) => ({ id: e.id, sectionId: e.sectionId ?? null })),
    )
  } catch {
    /* keep optimistic local order */
  }
}

/** Move a dragged entry to a section, inserting before `beforeId` (or at the section's end). */
function moveEntry(dragId: number, targetSectionId: number | null, beforeId: number | null) {
  const dragged = program.value.find((e) => e.id === dragId)
  if (!dragged) return
  const without = orderedEntries.value.filter((e) => e.id !== dragId)
  dragged.sectionId = targetSectionId
  let insertAt: number
  if (beforeId == null) {
    let last = -1
    without.forEach((e, i) => {
      if ((e.sectionId ?? null) === targetSectionId) last = i
    })
    insertAt = last + 1
  } else {
    insertAt = without.findIndex((e) => e.id === beforeId)
    if (insertAt < 0) insertAt = without.length
  }
  without.splice(insertAt, 0, dragged)
  persistOrder(without)
}

function moveToSection(entry: ShowProgram, sectionId: number | null) {
  if ((entry.sectionId ?? null) === sectionId) return
  moveEntry(entry.id, sectionId, null)
}

// Native HTML5 drag-and-drop.
const dragId = ref<number | null>(null)
const overId = ref<number | null>(null)

function onDragStart(id: number, e: DragEvent) {
  dragId.value = id
  if (e.dataTransfer) {
    e.dataTransfer.effectAllowed = 'move'
    e.dataTransfer.setData('text/plain', String(id))
  }
}
function onDragOverRow(id: number, e: DragEvent) {
  e.preventDefault()
  overId.value = id
}
function onDropRow(target: ShowProgram) {
  const from = dragId.value
  dragId.value = null
  overId.value = null
  if (from === null || from === target.id) return
  moveEntry(from, target.sectionId ?? null, target.id)
}
function onDropSection(sectionId: number | null) {
  const from = dragId.value
  dragId.value = null
  overId.value = null
  if (from === null) return
  moveEntry(from, sectionId, null)
}
function onDragEnd() {
  dragId.value = null
  overId.value = null
}

// --- Sections ---------------------------------------------------------------
async function addSection() {
  try {
    const { data } = await api.post<ShowSection>('/showsections', {
      studioId: studioStore.selectedStudioId,
      name: `Act ${sections.value.length + 1}`,
      orderIndex: sections.value.length,
    })
    sections.value.push(data)
  } catch {
    /* api.ts surfaces the error toast */
  }
}

async function renameSection(section: ShowSection) {
  try {
    await api.put(`/showsections/${section.id}`, section)
  } catch {
    /* local only */
  }
}

async function deleteSection(section: ShowSection) {
  if (
    !(await confirm({
      title: `Delete section “${section.name}”?`,
      message: 'Its numbers move to “Unassigned” — they are not deleted.',
      confirmText: 'Delete section',
      destructive: true,
    }))
  )
    return
  sections.value = sections.value.filter((s) => s.id !== section.id)
  program.value.forEach((p) => {
    if (p.sectionId === section.id) p.sectionId = null
  })
  program.value = [...program.value]
  try {
    await api.delete(`/showsections/${section.id}`)
  } catch {
    /* server SetNull mirrors the optimistic local change */
  }
}

async function moveSection(section: ShowSection, dir: -1 | 1) {
  const ordered = [...sections.value].sort((a, b) => a.orderIndex - b.orderIndex)
  const i = ordered.findIndex((s) => s.id === section.id)
  const j = i + dir
  if (j < 0 || j >= ordered.length) return
  ;[ordered[i], ordered[j]] = [ordered[j], ordered[i]]
  ordered.forEach((s, idx) => (s.orderIndex = idx))
  sections.value = [...sections.value]
  try {
    await api.put('/showsections/reorder', ordered.map((s) => s.id))
  } catch {
    /* keep optimistic order */
  }
}

// --- Print ------------------------------------------------------------------
async function downloadShowOrder() {
  const studioId = studioStore.selectedStudioId
  if (studioId == null) return
  try {
    const res = await api.get('/showprogram/pdf', { params: { studioId }, responseType: 'blob' })
    const blob = new Blob([res.data], { type: 'application/pdf' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = 'show-order.pdf'
    document.body.appendChild(a)
    a.click()
    a.remove()
    URL.revokeObjectURL(url)
  } catch {
    toast.error('Couldn’t generate the show order PDF.')
  }
}

onMounted(load)
watch(() => studioStore.selectedStudioId, load)
</script>

<template>
  <section>
    <div class="mb-4 flex items-center justify-between">
      <div>
        <h2 class="text-base font-semibold tracking-tight">Show Order</h2>
        <p class="text-sm text-muted-foreground">
          Group numbers into acts and drag to set the recital running order.
        </p>
      </div>
      <button
        class="inline-flex items-center gap-1.5 rounded-md border border-border px-2.5 py-1.5 text-sm hover:bg-accent disabled:opacity-50"
        :disabled="program.length === 0"
        @click="downloadShowOrder"
      >
        <FileDown class="h-3.5 w-3.5" /> Print show order (PDF)
      </button>
    </div>

    <!-- Add controls -->
    <div class="mb-3 flex flex-wrap items-center gap-2">
      <select
        v-model="routineToAdd"
        class="h-8 min-w-[14rem] rounded-md border border-border bg-background px-2 text-sm"
      >
        <option :value="null" disabled>
          {{ availableRoutines.length === 0 ? 'No routines to add' : 'Add one of your routines…' }}
        </option>
        <option v-for="r in availableRoutines" :key="r.id" :value="r.id">
          {{ r.songTitle || 'Untitled routine' }}
        </option>
      </select>
      <button
        class="inline-flex items-center gap-1 rounded-md border border-border px-2.5 py-1.5 text-sm hover:bg-accent disabled:opacity-50"
        :disabled="routineToAdd === null"
        @click="addToShow"
      >
        <Plus class="h-3.5 w-3.5" /> Add routine
      </button>

      <span class="mx-1 text-muted-foreground">·</span>

      <input
        v-model="quickAddTitle"
        placeholder="Quick add a number (e.g. guest class)…"
        class="h-8 min-w-[16rem] rounded-md border border-border bg-background px-2 text-sm"
        @keyup.enter="quickAdd"
      />
      <button
        class="inline-flex items-center gap-1 rounded-md border border-border px-2.5 py-1.5 text-sm hover:bg-accent disabled:opacity-50"
        :disabled="quickAddTitle.trim() === ''"
        @click="quickAdd"
      >
        <Plus class="h-3.5 w-3.5" /> Quick add
      </button>

      <button
        class="ml-auto inline-flex items-center gap-1 rounded-md border border-border px-2.5 py-1.5 text-sm hover:bg-accent"
        @click="addSection"
      >
        <FolderPlus class="h-3.5 w-3.5" /> Add section
      </button>
    </div>

    <div v-if="loading" class="rounded-lg border border-border p-8 text-center text-sm text-muted-foreground">
      Loading show order…
    </div>

    <div
      v-else-if="program.length === 0 && sections.length === 0"
      class="flex flex-col items-center gap-2 rounded-lg border border-dashed border-border p-10 text-center"
    >
      <Theater class="h-6 w-6 text-muted-foreground" />
      <p class="text-sm font-medium">No numbers in the program yet</p>
      <p class="text-sm text-muted-foreground">
        Add one of your routines, quick-add a guest number, or create a section to get started.
      </p>
    </div>

    <div v-else class="space-y-5">
      <div
        v-for="group in displayGroups"
        :key="group.section?.id ?? 'unassigned'"
        v-show="group.section !== null || group.entries.length > 0 || sections.length === 0"
        class="rounded-lg border border-border"
        @dragover.prevent
        @drop="onDropSection(group.section?.id ?? null)"
      >
        <!-- Section header -->
        <div
          v-if="group.section"
          class="flex items-center gap-2 border-b border-border bg-muted/40 px-3 py-2"
        >
          <input
            v-model="group.section.name"
            class="min-w-0 flex-1 rounded border border-transparent bg-transparent px-1 py-0.5 text-sm font-semibold hover:border-border focus:border-border focus:outline-none"
            @change="renameSection(group.section)"
          />
          <button
            class="rounded p-1 text-muted-foreground hover:bg-accent disabled:opacity-30"
            aria-label="Move section up"
            @click="moveSection(group.section, -1)"
          >
            <ArrowUp class="h-3.5 w-3.5" />
          </button>
          <button
            class="rounded p-1 text-muted-foreground hover:bg-accent disabled:opacity-30"
            aria-label="Move section down"
            @click="moveSection(group.section, 1)"
          >
            <ArrowDown class="h-3.5 w-3.5" />
          </button>
          <button
            class="rounded p-1 text-muted-foreground hover:bg-destructive/10 hover:text-destructive"
            aria-label="Delete section"
            @click="deleteSection(group.section)"
          >
            <Trash2 class="h-3.5 w-3.5" />
          </button>
        </div>
        <div
          v-else-if="sections.length > 0"
          class="border-b border-border bg-muted/20 px-3 py-2 text-sm font-semibold text-muted-foreground"
        >
          Unassigned
        </div>

        <!-- Numbers in this section -->
        <p
          v-if="group.entries.length === 0"
          class="px-3 py-4 text-center text-xs text-muted-foreground"
        >
          Drag numbers here.
        </p>
        <ul v-else class="divide-y divide-border">
          <template v-for="(entry, index) in group.entries" :key="entry.id">
            <li
              draggable="true"
              :class="[
                'flex items-center gap-3 bg-background px-3 py-2.5 transition-colors',
                overId === entry.id ? 'bg-accent' : '',
                dragId === entry.id ? 'opacity-50' : '',
              ]"
              @dragstart="onDragStart(entry.id, $event)"
              @dragover="onDragOverRow(entry.id, $event)"
              @drop.stop="onDropRow(entry)"
              @dragend="onDragEnd"
            >
              <GripVertical class="h-4 w-4 shrink-0 cursor-grab text-muted-foreground" />
              <span
                class="flex h-6 w-6 shrink-0 items-center justify-center rounded bg-muted text-xs font-medium tabular-nums text-muted-foreground"
              >
                {{ numberOf.get(entry.id) }}
              </span>
              <div class="min-w-0 flex-1">
                <p class="truncate text-sm font-medium">{{ rowInfo(entry).title }}</p>
                <p v-if="rowInfo(entry).className" class="truncate text-xs text-muted-foreground">
                  {{ rowInfo(entry).className }}
                </p>
                <p v-else-if="studentIdsOf(entry).length" class="truncate text-xs text-muted-foreground">
                  {{ studentIdsOf(entry).map(nameOf).join(', ') }}
                </p>
                <p v-else class="truncate text-xs italic text-muted-foreground">Guest / other number</p>
              </div>
              <!-- Attach students (standalone/quick-add numbers only) -->
              <button
                v-if="entry.routineId == null"
                class="inline-flex shrink-0 items-center gap-1 rounded border border-border px-2 py-1 text-xs text-muted-foreground hover:bg-accent"
                :title="'Add students from your roster to this number'"
                @click="studentsOpenFor = studentsOpenFor === entry.id ? null : entry.id"
              >
                <Users class="h-3.5 w-3.5" />
                <span v-if="studentIdsOf(entry).length" class="tabular-nums">{{ studentIdsOf(entry).length }}</span>
              </button>
              <select
                :value="entry.sectionId ?? ''"
                class="h-7 max-w-[9rem] rounded border border-border bg-background px-1.5 text-xs text-muted-foreground"
                aria-label="Move to section"
                @change="moveToSection(entry, ($event.target as HTMLSelectElement).value === '' ? null : Number(($event.target as HTMLSelectElement).value))"
              >
                <option value="">Unassigned</option>
                <option v-for="s in sections" :key="s.id" :value="s.id">{{ s.name }}</option>
              </select>
              <button
                class="shrink-0 rounded p-1.5 text-muted-foreground hover:bg-destructive/10 hover:text-destructive"
                aria-label="Remove from show order"
                @click="removeFromShow(entry)"
              >
                <Trash2 class="h-4 w-4" />
              </button>
            </li>

            <!-- Student picker for a standalone/quick-add number -->
            <li
              v-if="studentsOpenFor === entry.id"
              :key="`stu-${entry.id}`"
              class="bg-muted/30 px-3 py-2.5"
            >
              <p class="mb-1.5 text-xs font-medium text-muted-foreground">
                Students in this number (from your roster)
              </p>
              <p v-if="students.length === 0" class="text-xs text-muted-foreground">
                No students in this studio yet.
              </p>
              <div v-else class="flex flex-wrap gap-1.5">
                <button
                  v-for="s in students"
                  :key="s.id"
                  class="rounded-full border px-2 py-0.5 text-xs transition-colors"
                  :class="
                    studentIdsOf(entry).includes(s.id)
                      ? 'border-foreground bg-foreground text-background'
                      : 'border-border text-muted-foreground hover:bg-accent'
                  "
                  @click="toggleStudent(entry, s.id)"
                >
                  {{ s.firstName }} {{ s.lastName }}
                </button>
              </div>
            </li>

            <!-- Quick Change Alert between consecutive numbers in this section -->
            <li
              v-if="quickChangeNames(group.entries, index).length"
              :key="`qc-${entry.id}`"
              class="flex items-start gap-2 bg-amber-500/10 px-3 py-2 text-sm text-amber-700 dark:text-amber-400"
            >
              <AlertTriangle class="mt-0.5 h-4 w-4 shrink-0" />
              <span>
                <span class="font-semibold">Quick Change Alert:</span>
                {{ quickChangeNames(group.entries, index).join(', ') }}
                {{ quickChangeNames(group.entries, index).length > 1 ? 'have' : 'has' }} back-to-back
                numbers.
              </span>
            </li>
          </template>
        </ul>
      </div>
    </div>
  </section>
</template>
