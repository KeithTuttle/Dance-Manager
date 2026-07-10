<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { GripVertical, AlertTriangle, Theater, Plus, Trash2 } from 'lucide-vue-next'
import { api } from '@/lib/api'
import { confirm } from '@/lib/confirm'
import { toast } from '@/lib/toast'
import { useStudioStore } from '@/stores/studio'
import type { ShowProgram, Routine, DanceClass, RecitalParticipation, Student } from '@/types'

const studioStore = useStudioStore()

const program = ref<ShowProgram[]>([])
const routines = ref<Routine[]>([])
const classes = ref<DanceClass[]>([])
const participations = ref<RecitalParticipation[]>([])
const students = ref<Student[]>([])
const loading = ref(false)

// Routines not yet in the show order, available to add.
const availableRoutines = computed(() => {
  const usedIds = new Set(program.value.map((p) => p.routineId))
  return routines.value.filter((r) => !usedIds.has(r.id))
})
const routineToAdd = ref<number | null>(null)

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

interface ProgramRow {
  entry: ShowProgram
  routine?: Routine
  className: string
  title: string
}

const rows = computed<ProgramRow[]>(() =>
  program.value.map((entry) => {
    const routine = routineMap.value.get(entry.routineId)
    const cls = routine ? classMap.value.get(routine.classId) : undefined
    return {
      entry,
      routine,
      className: cls?.name ?? 'Unknown class',
      title: routine?.songTitle ?? `Routine #${entry.routineId}`,
    }
  }),
)

/** Names of students appearing in both row i and row i+1 (back-to-back). */
function quickChangeStudents(index: number): string[] {
  const a = rows.value[index]?.routine
  const b = rows.value[index + 1]?.routine
  if (!a || !b) return []
  const setA = participatingByClass.value.get(a.classId)
  const setB = participatingByClass.value.get(b.classId)
  if (!setA || !setB) return []
  const names: string[] = []
  for (const id of setA) {
    if (setB.has(id)) {
      const s = studentMap.value.get(id)
      names.push(s ? `${s.firstName} ${s.lastName}` : `Student #${id}`)
    }
  }
  return names
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
  const studioQuery = studioId ? `?studioId=${studioId}` : ''
  try {
    const [prog, rts, cls, parts, studs] = await Promise.all([
      safeGet<ShowProgram>(`/showprogram${studioQuery}`),
      safeGet<Routine>(`/routines${studioQuery}`),
      safeGet<DanceClass>(`/classes${studioQuery}`),
      safeGet<RecitalParticipation>('/recitalparticipation'),
      safeGet<Student>(`/students${studioQuery}`),
    ])
    program.value = prog
    routines.value = rts
    classes.value = cls
    participations.value = parts
    students.value = studs
  } finally {
    loading.value = false
  }
}

// Drag-and-drop reordering (native HTML5, no library).
const dragIndex = ref<number | null>(null)
const overIndex = ref<number | null>(null)

function onDragStart(index: number, e: DragEvent) {
  dragIndex.value = index
  // Required for drag to initiate in Firefox.
  if (e.dataTransfer) {
    e.dataTransfer.effectAllowed = 'move'
    e.dataTransfer.setData('text/plain', String(index))
  }
}

function onDragOver(index: number, e: DragEvent) {
  e.preventDefault()
  overIndex.value = index
}

function onDrop(index: number) {
  const from = dragIndex.value
  dragIndex.value = null
  overIndex.value = null
  if (from === null || from === index) return
  const next = [...program.value]
  const [moved] = next.splice(from, 1)
  next.splice(index, 0, moved)
  program.value = next
  persistOrder()
}

function onDragEnd() {
  dragIndex.value = null
  overIndex.value = null
}

async function persistOrder() {
  const ids = program.value.map((p) => p.id)
  // Optimistically reflect new positions locally.
  program.value.forEach((p, i) => (p.orderPosition = i))
  try {
    await api.put('/showprogram/reorder', ids)
  } catch {
    // Backend unavailable (no DB) — keep the optimistic local order.
  }
}

async function addToShow() {
  const routineId = routineToAdd.value
  if (routineId === null) return
  try {
    const { data } = await api.post<ShowProgram>('/showprogram', {
      routineId,
      orderPosition: program.value.length,
    })
    program.value.push(data)
    routineToAdd.value = null
    toast.success('Added to show order')
  } catch {
    /* api.ts already surfaces the error toast */
  }
}

async function removeFromShow(entry: ShowProgram) {
  if (
    !(await confirm({
      title: 'Remove this routine from the show order?',
      confirmText: 'Remove',
      destructive: true,
    }))
  )
    return
  const prev = program.value
  program.value = program.value.filter((p) => p.id !== entry.id)
  try {
    await api.delete(`/showprogram/${entry.id}`)
    await persistOrder() // reindex remaining positions
    toast.success('Removed')
  } catch {
    program.value = prev
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
          Drag routines to set the recital running order.
        </p>
      </div>
      <span v-if="rows.length" class="text-xs text-muted-foreground">{{ rows.length }} routines</span>
    </div>

    <div class="mb-4 flex items-center gap-2">
      <select
        v-model="routineToAdd"
        class="h-8 min-w-[16rem] rounded-md border border-border bg-background px-2 text-sm"
      >
        <option :value="null" disabled>
          {{ availableRoutines.length === 0 ? 'No routines to add' : 'Select a routine to add…' }}
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
        <Plus class="h-3.5 w-3.5" /> Add to show
      </button>
    </div>

    <div v-if="loading" class="rounded-lg border border-border p-8 text-center text-sm text-muted-foreground">
      Loading show order…
    </div>

    <div
      v-else-if="rows.length === 0"
      class="flex flex-col items-center gap-2 rounded-lg border border-dashed border-border p-10 text-center"
    >
      <Theater class="h-6 w-6 text-muted-foreground" />
      <p class="text-sm font-medium">No routines in the program yet</p>
      <p class="text-sm text-muted-foreground">
        Once routines are added to the show, they will appear here to be ordered.
      </p>
    </div>

    <ul v-else class="space-y-1">
      <template v-for="(row, index) in rows" :key="row.entry.id">
        <li
          draggable="true"
          :class="[
            'flex items-center gap-3 rounded-md border border-border bg-background px-3 py-2.5 transition-colors',
            overIndex === index ? 'border-foreground bg-accent' : '',
            dragIndex === index ? 'opacity-50' : '',
          ]"
          @dragstart="onDragStart(index, $event)"
          @dragover="onDragOver(index, $event)"
          @drop="onDrop(index)"
          @dragend="onDragEnd"
        >
          <GripVertical class="h-4 w-4 shrink-0 cursor-grab text-muted-foreground" />
          <span
            class="flex h-6 w-6 shrink-0 items-center justify-center rounded bg-muted text-xs font-medium tabular-nums text-muted-foreground"
          >
            {{ index + 1 }}
          </span>
          <div class="min-w-0 flex-1">
            <p class="truncate text-sm font-medium">{{ row.title }}</p>
            <p class="truncate text-xs text-muted-foreground">{{ row.className }}</p>
          </div>
          <button
            class="shrink-0 rounded p-1.5 text-muted-foreground hover:bg-destructive/10 hover:text-destructive"
            aria-label="Remove from show order"
            @click="removeFromShow(row.entry)"
          >
            <Trash2 class="h-4 w-4" />
          </button>
        </li>

        <!-- Quick Change Alert between consecutive routines sharing a student -->
        <li
          v-if="quickChangeStudents(index).length"
          :key="`qc-${row.entry.id}`"
          class="flex items-start gap-2 rounded-md border border-amber-500/50 bg-amber-500/10 px-3 py-2 text-sm text-amber-700 dark:text-amber-400"
        >
          <AlertTriangle class="mt-0.5 h-4 w-4 shrink-0" />
          <span>
            <span class="font-semibold">Quick Change Alert:</span>
            {{ quickChangeStudents(index).join(', ') }}
            {{ quickChangeStudents(index).length > 1 ? 'have' : 'has' }} back-to-back routines.
          </span>
        </li>
      </template>
    </ul>
  </section>
</template>
