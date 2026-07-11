<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { api } from '@/lib/api'
import { useStudioStore } from '@/stores/studio'
import type { DanceClass, Routine, Formation, Student, RecitalParticipation, Gender } from '@/types'
import { toast } from '@/lib/toast'
import { confirm } from '@/lib/confirm'
import { Download, Plus, Music, Loader2, Trash2 } from 'lucide-vue-next'

const studioStore = useStudioStore()

// --- Selection state ---
const classes = ref<DanceClass[]>([])
const selectedClassId = ref<number | null>(null)
const routines = ref<Routine[]>([])
const selectedRoutineId = ref<number | null>(null)

// --- Routine detail (left panel) ---
const notesDraft = ref('')

// --- Formations (right panel) ---
const formations = ref<Formation[]>([])
const selectedFormationId = ref<number | null>(null)

// --- Participating students ---
const students = ref<Student[]>([])
const participations = ref<RecitalParticipation[]>([])

const generatingPdf = ref(false)

const selectedRoutine = computed(
  () => routines.value.find((r) => r.id === selectedRoutineId.value) ?? null,
)
const selectedFormation = computed(
  () => formations.value.find((f) => f.id === selectedFormationId.value) ?? null,
)

// Students marked IsParticipating for the selected class.
const participatingStudents = computed(() => {
  const ids = new Set(
    participations.value.filter((p) => p.isParticipating).map((p) => p.studentId),
  )
  return students.value.filter((s) => ids.has(s.id))
})

function initials(s: Student): string {
  const f = s.firstName?.[0] ?? ''
  const l = s.lastName?.[0] ?? ''
  return (f + l).toUpperCase() || '?'
}

// --- Stage geometry (SVG user units) ---
// A rectangular grid inset within the viewBox, leaving room for the UPSTAGE
// label (top) and the number ruler + DOWNSTAGE label (bottom). The container's
// aspect matches VB_W:VB_H so `meet` fills it exactly and nodes stay round.
const VB_W = 320
const VB_H = 210
const GX0 = 16 // grid left
const GY0 = 28 // grid top (below UPSTAGE)
const GW = VB_W - GX0 * 2 // grid width (288)
const GH = 150 // grid height
const GY1 = GY0 + GH // grid bottom
const UNIT = GW / 20 // one ruler unit (−10…+10 across the width)
const NODE_R = 9

// Boy/girl color coding (kept legible in light + dark; initials always shown).
const GENDER_FILL = { Boys: '#2563eb', Girls: '#db2777', none: '#94a3b8' }
function genderFill(g?: Gender | null): string {
  return g === 'Boys' ? GENDER_FILL.Boys : g === 'Girls' ? GENDER_FILL.Girls : GENDER_FILL.none
}
function genderText(g?: Gender | null): string {
  return g ? '#ffffff' : '#0f172a'
}

// --- Video embed parsing (YouTube / Vimeo) ---
const embedSrc = computed(() => parseEmbed(selectedRoutine.value?.videoUrl ?? null))

function parseEmbed(url: string | null | undefined): string | null {
  if (!url) return null
  try {
    const u = new URL(url.trim())
    const host = u.hostname.replace(/^www\./, '')
    if (host === 'youtu.be') {
      const id = u.pathname.slice(1)
      return id ? `https://www.youtube.com/embed/${id}` : null
    }
    if (host === 'youtube.com' || host === 'm.youtube.com') {
      const id = u.searchParams.get('v')
      if (id) return `https://www.youtube.com/embed/${id}`
      // /embed/xxx or /shorts/xxx
      const m = u.pathname.match(/\/(embed|shorts)\/([^/?]+)/)
      if (m) return `https://www.youtube.com/embed/${m[2]}`
      return null
    }
    if (host === 'vimeo.com' || host === 'player.vimeo.com') {
      const m = u.pathname.match(/(\d+)/)
      return m ? `https://player.vimeo.com/video/${m[1]}` : null
    }
    return null
  } catch {
    return null
  }
}

// --- Coordinate parsing ---
type Coord = { x: number; y: number }
type CoordMap = Record<string, Coord>

const coordMap = ref<CoordMap>({})

function parseCoords(json: string | null | undefined): CoordMap {
  if (!json) return {}
  try {
    const parsed = JSON.parse(json)
    if (parsed && typeof parsed === 'object') return parsed as CoordMap
    return {}
  } catch {
    return {}
  }
}

// Node list = participating students, positioned from coordMap (default spread).
const nodes = computed(() =>
  participatingStudents.value.map((s, i) => {
    const c = coordMap.value[String(s.id)]
    return {
      student: s,
      x: c?.x ?? 50,
      y: c?.y ?? 10 + ((i * 12) % 80),
      placed: !!c,
    }
  }),
)

// --- Data loading ---
async function loadClasses() {
  const studioId = studioStore.selectedStudioId
  if (!studioId) {
    classes.value = []
    return
  }
  try {
    const { data } = await api.get<DanceClass[]>('/classes', { params: { studioId } })
    classes.value = data ?? []
  } catch {
    classes.value = []
  }
  if (!classes.value.some((c) => c.id === selectedClassId.value)) {
    selectedClassId.value = classes.value[0]?.id ?? null
  }
}

async function loadStudents() {
  const studioId = studioStore.selectedStudioId
  if (!studioId) {
    students.value = []
    return
  }
  try {
    const { data } = await api.get<Student[]>('/students', { params: { studioId } })
    students.value = data ?? []
  } catch {
    students.value = []
  }
}

async function loadRoutines() {
  if (!selectedClassId.value) {
    routines.value = []
    selectedRoutineId.value = null
    return
  }
  try {
    const { data } = await api.get<Routine[]>('/routines', {
      params: { classId: selectedClassId.value },
    })
    routines.value = data ?? []
  } catch {
    routines.value = []
  }
  if (!routines.value.some((r) => r.id === selectedRoutineId.value)) {
    selectedRoutineId.value = routines.value[0]?.id ?? null
  }
}

async function loadParticipation() {
  if (!selectedClassId.value) {
    participations.value = []
    return
  }
  try {
    const { data } = await api.get<RecitalParticipation[]>('/recitalparticipation', {
      params: { classId: selectedClassId.value },
    })
    participations.value = data ?? []
  } catch {
    participations.value = []
  }
}

async function loadFormations() {
  if (!selectedRoutineId.value) {
    formations.value = []
    selectedFormationId.value = null
    return
  }
  try {
    const { data } = await api.get<Formation[]>('/formations', {
      params: { routineId: selectedRoutineId.value },
    })
    formations.value = data ?? []
  } catch {
    formations.value = []
  }
  if (!formations.value.some((f) => f.id === selectedFormationId.value)) {
    selectedFormationId.value = formations.value[0]?.id ?? null
  }
}

// --- Watchers wiring the cascade ---
watch(
  () => studioStore.selectedStudioId,
  async () => {
    await Promise.all([loadClasses(), loadStudents()])
  },
)

watch(selectedClassId, async () => {
  await Promise.all([loadRoutines(), loadParticipation()])
})

watch(selectedRoutineId, () => {
  notesDraft.value = selectedRoutine.value?.choreographyNotes ?? ''
  loadFormations()
})

watch(selectedFormationId, () => {
  coordMap.value = parseCoords(selectedFormation.value?.studentCoordinates)
})

onMounted(async () => {
  if (studioStore.studios.length === 0) {
    try {
      await studioStore.fetchStudios()
    } catch {
      /* graceful: empty */
    }
  }
  await Promise.all([loadClasses(), loadStudents()])
})

// --- Save choreography notes on blur ---
async function saveNotes() {
  const routine = selectedRoutine.value
  if (!routine) return
  if ((routine.choreographyNotes ?? '') === notesDraft.value) return
  const updated: Routine = { ...routine, choreographyNotes: notesDraft.value }
  try {
    await api.put(`/routines/${routine.id}`, updated)
    const idx = routines.value.findIndex((r) => r.id === routine.id)
    if (idx !== -1) routines.value[idx] = updated
  } catch {
    /* graceful: keep local draft */
  }
}

// --- Routine create / edit ---
async function addRoutine() {
  const classId = selectedClassId.value
  if (!classId) return
  const payload: Omit<Routine, 'id'> = {
    classId,
    songTitle: 'New Routine',
    artist: null,
    videoUrl: null,
    choreographyNotes: null,
  }
  try {
    const { data } = await api.post<Routine>('/routines', payload)
    routines.value.push(data)
    selectedRoutineId.value = data.id
    toast.success('Routine added')
  } catch {
    /* api.ts already surfaces the error toast */
  }
}

async function saveRoutineFields() {
  const routine = selectedRoutine.value
  if (!routine) return
  try {
    await api.put(`/routines/${routine.id}`, routine)
    toast.success('Saved')
  } catch {
    /* api.ts already surfaces the error toast */
  }
}

async function deleteRoutine() {
  const routine = selectedRoutine.value
  if (!routine) return
  if (
    !(await confirm({
      title: `Delete “${routine.songTitle || 'this routine'}”?`,
      message: 'This removes the routine and all of its formations.',
      confirmText: 'Delete',
      destructive: true,
    }))
  )
    return
  try {
    await api.delete(`/routines/${routine.id}`)
    routines.value = routines.value.filter((r) => r.id !== routine.id)
    selectedRoutineId.value = routines.value[0]?.id ?? null
    toast.success('Routine deleted')
  } catch {
    /* api.ts already surfaces the error toast */
  }
}

// --- Formation management ---
async function addFormation() {
  if (!selectedRoutineId.value) return
  const payload: Omit<Formation, 'id'> = {
    routineId: selectedRoutineId.value,
    formationName: `Formation ${formations.value.length + 1}`,
    orderIndex: formations.value.length,
    studentCoordinates: '{}',
  }
  try {
    const { data } = await api.post<Formation>('/formations', payload)
    formations.value.push(data)
    selectedFormationId.value = data.id
  } catch {
    /* graceful: DB unavailable */
  }
}

async function saveFormation() {
  const formation = selectedFormation.value
  if (!formation) return
  const updated: Formation = {
    ...formation,
    studentCoordinates: JSON.stringify(coordMap.value),
  }
  try {
    await api.put(`/formations/${formation.id}`, updated)
    const idx = formations.value.findIndex((f) => f.id === formation.id)
    if (idx !== -1) formations.value[idx] = updated
  } catch {
    /* graceful: DB unavailable */
  }
}

async function saveFormationName() {
  const formation = selectedFormation.value
  if (!formation) return
  try {
    await api.put(`/formations/${formation.id}`, formation)
    toast.success('Saved')
  } catch {
    /* api.ts already surfaces the error toast */
  }
}

async function deleteFormation() {
  const formation = selectedFormation.value
  if (!formation) return
  if (
    !(await confirm({
      title: `Delete “${formation.formationName || 'this formation'}”?`,
      confirmText: 'Delete',
      destructive: true,
    }))
  )
    return
  try {
    await api.delete(`/formations/${formation.id}`)
    formations.value = formations.value.filter((f) => f.id !== formation.id)
    selectedFormationId.value = formations.value[0]?.id ?? null
    toast.success('Formation deleted')
  } catch {
    /* api.ts already surfaces the error toast */
  }
}

// --- Drag handling on the SVG stage ---
const stageRef = ref<SVGSVGElement | null>(null)
const draggingId = ref<number | null>(null)

function onPointerDown(studentId: number, e: PointerEvent) {
  draggingId.value = studentId
  ;(e.target as Element).setPointerCapture?.(e.pointerId)
  // Ensure a coord entry exists at the current node position.
  if (!coordMap.value[String(studentId)]) {
    const node = nodes.value.find((n) => n.student.id === studentId)
    coordMap.value[String(studentId)] = { x: node?.x ?? 50, y: node?.y ?? 50 }
  }
}

function onPointerMove(e: PointerEvent) {
  if (draggingId.value === null || !stageRef.value) return
  const rect = stageRef.value.getBoundingClientRect()
  if (rect.width === 0 || rect.height === 0) return
  // client px -> viewBox units (aspect matches, so this is linear), then map
  // the grid area (inset by GX0/GY0) back to the stored 0–100 percentages.
  const vbX = ((e.clientX - rect.left) / rect.width) * VB_W
  const vbY = ((e.clientY - rect.top) / rect.height) * VB_H
  const x = ((vbX - GX0) / GW) * 100
  const y = ((vbY - GY0) / GH) * 100
  coordMap.value[String(draggingId.value)] = {
    x: Math.min(100, Math.max(0, x)),
    y: Math.min(100, Math.max(0, y)),
  }
}

function onPointerUp() {
  if (draggingId.value === null) return
  draggingId.value = null
  saveFormation()
}

// --- Generate Sub Handoff PDF ---
async function generateHandoff() {
  if (!selectedClassId.value) return
  generatingPdf.value = true
  try {
    const res = await api.get('/subhandoff', {
      params: { classId: selectedClassId.value },
      responseType: 'blob',
    })
    const blob = new Blob([res.data], { type: 'application/pdf' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = 'sub-handoff.pdf'
    document.body.appendChild(a)
    a.click()
    a.remove()
    URL.revokeObjectURL(url)
  } catch {
    /* graceful: DB unavailable -> no download */
  } finally {
    generatingPdf.value = false
  }
}
</script>

<template>
  <div class="flex h-full flex-col">
    <!-- Header + selectors -->
    <header class="border-b border-border px-4 py-3 sm:px-6">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 class="text-xl font-semibold tracking-tight">Choreography &amp; Formations</h1>
          <p class="mt-0.5 text-sm text-muted-foreground">
            Document routines and map the stage. Drag student nodes to build formations.
          </p>
        </div>
        <button
          class="inline-flex items-center gap-2 rounded-md border border-border bg-foreground px-3 py-2 text-sm font-medium text-background transition-opacity hover:opacity-90 disabled:opacity-50"
          :disabled="!selectedClassId || generatingPdf"
          @click="generateHandoff"
        >
          <Loader2 v-if="generatingPdf" class="h-4 w-4 animate-spin" />
          <Download v-else class="h-4 w-4" />
          Generate Sub Handoff
        </button>
      </div>

      <div class="mt-3 flex flex-wrap items-center gap-3">
        <label class="flex items-center gap-2 text-sm">
          <span class="text-muted-foreground">Class</span>
          <select
            v-model="selectedClassId"
            class="rounded-md border border-border bg-background px-2 py-1.5 text-sm"
          >
            <option v-if="classes.length === 0" :value="null">No classes</option>
            <option v-for="c in classes" :key="c.id" :value="c.id">{{ c.name }}</option>
          </select>
        </label>
        <label class="flex items-center gap-2 text-sm">
          <span class="text-muted-foreground">Routine</span>
          <select
            v-model="selectedRoutineId"
            class="rounded-md border border-border bg-background px-2 py-1.5 text-sm"
          >
            <option v-if="routines.length === 0" :value="null">No routines</option>
            <option v-for="r in routines" :key="r.id" :value="r.id">
              {{ r.songTitle || 'Untitled' }}
            </option>
          </select>
        </label>
        <button
          class="inline-flex items-center gap-1.5 rounded-md border border-border px-2.5 py-1.5 text-sm hover:bg-accent disabled:opacity-50"
          :disabled="!selectedClassId"
          @click="addRoutine"
        >
          <Plus class="h-3.5 w-3.5" /> Add routine
        </button>
        <button
          class="inline-flex items-center gap-1.5 rounded-md border border-border px-2.5 py-1.5 text-sm text-muted-foreground hover:bg-destructive/10 hover:text-destructive disabled:opacity-50"
          :disabled="!selectedRoutine"
          @click="deleteRoutine"
        >
          <Trash2 class="h-3.5 w-3.5" /> Delete routine
        </button>
      </div>
    </header>

    <!-- Empty state -->
    <div v-if="!selectedRoutine" class="flex flex-1 items-center justify-center p-8">
      <div class="max-w-sm rounded-lg border border-dashed border-border p-8 text-center">
        <Music class="mx-auto h-8 w-8 text-muted-foreground" />
        <p class="mt-3 text-sm font-medium">No routine selected</p>
        <p class="mt-1 text-sm text-muted-foreground">
          Select a class and routine above to document choreography and map formations. If nothing
          appears, the database may be offline.
        </p>
      </div>
    </div>

    <!-- 50/50 split -->
    <div v-else class="grid flex-1 grid-cols-1 gap-px overflow-hidden bg-border lg:grid-cols-2">
      <!-- LEFT: video + notes -->
      <div class="flex flex-col overflow-y-auto bg-background p-4 sm:p-6">
        <div class="mb-4 grid grid-cols-1 gap-2 sm:grid-cols-2">
          <label class="space-y-1">
            <span class="text-xs text-muted-foreground">Song title</span>
            <input
              v-model="selectedRoutine.songTitle"
              type="text"
              class="w-full rounded-md border border-border bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
              @change="saveRoutineFields"
            />
          </label>
          <label class="space-y-1">
            <span class="text-xs text-muted-foreground">Artist</span>
            <input
              v-model="selectedRoutine.artist"
              type="text"
              class="w-full rounded-md border border-border bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
              @change="saveRoutineFields"
            />
          </label>
          <label class="space-y-1 sm:col-span-2">
            <span class="text-xs text-muted-foreground">Video link (YouTube or Vimeo)</span>
            <input
              v-model="selectedRoutine.videoUrl"
              type="text"
              placeholder="https://youtube.com/watch?v=…"
              class="w-full rounded-md border border-border bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
              @change="saveRoutineFields"
            />
          </label>
        </div>

        <div class="aspect-video w-full overflow-hidden rounded-md border border-border bg-black">
          <iframe
            v-if="embedSrc"
            :src="embedSrc"
            class="h-full w-full"
            frameborder="0"
            allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
            allowfullscreen
          />
          <div
            v-else
            class="flex h-full w-full items-center justify-center text-center text-sm text-muted-foreground"
          >
            <span>
              {{
                selectedRoutine.videoUrl ? 'Unsupported video link' : 'No video link for this routine'
              }}
            </span>
          </div>
        </div>

        <label class="mt-4 text-sm font-medium">Choreography notes (counts &amp; lyrical cues)</label>
        <textarea
          v-model="notesDraft"
          class="mt-2 min-h-[200px] flex-1 resize-y rounded-md border border-border bg-background p-3 font-mono text-sm leading-relaxed focus:outline-none focus:ring-1 focus:ring-ring"
          placeholder="8-count breakdowns, lyrical cues, entrances/exits…"
          @blur="saveNotes"
        />
      </div>

      <!-- RIGHT: stage grid -->
      <div class="flex flex-col overflow-y-auto bg-background p-4 sm:p-6">
        <!-- Formation selector -->
        <div class="mb-4 flex flex-wrap items-center gap-2">
          <span class="text-sm font-medium">Formations</span>
          <div class="flex flex-wrap gap-1.5">
            <button
              v-for="f in formations"
              :key="f.id"
              class="rounded-md border px-2.5 py-1 text-xs transition-colors"
              :class="
                f.id === selectedFormationId
                  ? 'border-foreground bg-foreground text-background'
                  : 'border-border hover:bg-accent'
              "
              @click="selectedFormationId = f.id"
            >
              {{ f.formationName || 'Formation' }}
            </button>
          </div>
          <button
            class="inline-flex items-center gap-1 rounded-md border border-border px-2.5 py-1 text-xs hover:bg-accent"
            @click="addFormation"
          >
            <Plus class="h-3.5 w-3.5" /> Add
          </button>
        </div>

        <!-- Rename / delete the selected formation -->
        <div v-if="selectedFormation" class="mb-4 flex items-center gap-2">
          <input
            v-model="selectedFormation.formationName"
            placeholder="Formation name"
            class="min-w-0 flex-1 rounded-md border border-border bg-background px-2.5 py-1 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
            @change="saveFormationName"
          />
          <button
            class="shrink-0 rounded p-1.5 text-muted-foreground hover:bg-destructive/10 hover:text-destructive"
            aria-label="Delete formation"
            @click="deleteFormation"
          >
            <Trash2 class="h-4 w-4" />
          </button>
        </div>

        <!-- Stage -->
        <div
          v-if="!selectedFormation"
          class="flex flex-1 items-center justify-center rounded-md border border-dashed border-border p-8 text-center text-sm text-muted-foreground"
        >
          No formations yet. Click "Add" to create one.
        </div>
        <template v-else>
          <div class="relative w-full">
            <svg
              ref="stageRef"
              :viewBox="`0 0 ${VB_W} ${VB_H}`"
              preserveAspectRatio="xMidYMid meet"
              class="aspect-[32/21] w-full touch-none select-none rounded-md border border-border bg-muted"
              @pointermove="onPointerMove"
              @pointerup="onPointerUp"
              @pointerleave="onPointerUp"
            >
              <!-- UPSTAGE (back of stage) -->
              <text
                :x="VB_W / 2"
                :y="16"
                text-anchor="middle"
                font-size="8"
                letter-spacing="1.5"
                class="fill-muted-foreground font-semibold"
              >
                UPSTAGE
              </text>

              <!-- grid border -->
              <rect
                :x="GX0"
                :y="GY0"
                :width="GW"
                :height="GH"
                fill="none"
                class="stroke-muted-foreground"
                stroke-width="1"
                opacity="0.5"
              />
              <!-- grid lines. Note: --border and --muted share the same value in
                   dark mode (see style.css), so stroke-border is invisible against
                   this bg-muted surface there; stroke-muted-foreground + opacity
                   stays visible (if subtle) in both themes. -->
              <g class="stroke-muted-foreground" stroke-width="0.4" opacity="0.35">
                <line
                  v-for="k in 21"
                  :key="`v${k}`"
                  :x1="GX0 + (k - 1) * UNIT"
                  :y1="GY0"
                  :x2="GX0 + (k - 1) * UNIT"
                  :y2="GY1"
                  :opacity="k - 1 === 10 ? 1 : undefined"
                />
                <line
                  v-for="r in 5"
                  :key="`h${r}`"
                  :x1="GX0"
                  :y1="GY0 + (r * GH) / 6"
                  :x2="GX0 + GW"
                  :y2="GY0 + (r * GH) / 6"
                />
              </g>

              <!-- number ruler: 10 … 0 … 10 -->
              <g class="fill-muted-foreground" font-size="7" text-anchor="middle">
                <text v-for="k in 21" :key="`num${k}`" :x="GX0 + (k - 1) * UNIT" :y="GY1 + 13">
                  {{ Math.abs(k - 11) }}
                </text>
              </g>

              <!-- DOWNSTAGE (front / audience) -->
              <text
                :x="VB_W / 2"
                :y="GY1 + 30"
                text-anchor="middle"
                font-size="8"
                letter-spacing="1.5"
                class="fill-muted-foreground font-semibold"
              >
                DOWNSTAGE
              </text>

              <!-- nodes (colored by gender; initials always shown) -->
              <g
                v-for="n in nodes"
                :key="n.student.id"
                class="cursor-grab"
                :class="{ 'cursor-grabbing': draggingId === n.student.id }"
                @pointerdown="onPointerDown(n.student.id, $event)"
              >
                <circle
                  :cx="GX0 + (n.x / 100) * GW"
                  :cy="GY0 + (n.y / 100) * GH"
                  :r="NODE_R"
                  :fill="genderFill(n.student.gender)"
                  :stroke="draggingId === n.student.id ? '#0f172a' : 'rgba(15,23,42,0.25)'"
                  :stroke-width="draggingId === n.student.id ? 1.75 : 0.75"
                />
                <text
                  :x="GX0 + (n.x / 100) * GW"
                  :y="GY0 + (n.y / 100) * GH"
                  text-anchor="middle"
                  dominant-baseline="central"
                  font-size="7"
                  font-weight="600"
                  :fill="genderText(n.student.gender)"
                  class="pointer-events-none"
                >
                  {{ initials(n.student) }}
                </text>
              </g>
            </svg>
          </div>

          <div class="mt-3 flex flex-wrap items-center justify-between gap-2">
            <p class="text-xs text-muted-foreground">
              Drag nodes to position dancers. {{ participatingStudents.length }} participating
              student(s). Positions save automatically.
            </p>
            <!-- Gender legend -->
            <div class="flex items-center gap-3 text-xs text-muted-foreground">
              <span class="inline-flex items-center gap-1.5">
                <span class="h-2.5 w-2.5 rounded-full" :style="{ background: GENDER_FILL.Boys }" />
                Boys
              </span>
              <span class="inline-flex items-center gap-1.5">
                <span class="h-2.5 w-2.5 rounded-full" :style="{ background: GENDER_FILL.Girls }" />
                Girls
              </span>
              <span class="inline-flex items-center gap-1.5">
                <span class="h-2.5 w-2.5 rounded-full" :style="{ background: GENDER_FILL.none }" />
                Unspecified
              </span>
            </div>
          </div>
          <div
            v-if="participatingStudents.length === 0"
            class="mt-2 text-xs text-muted-foreground"
          >
            No participating students found for this class (mark students as participating in the
            Recital view, or the database may be offline).
          </div>
        </template>
      </div>
    </div>
  </div>
</template>
