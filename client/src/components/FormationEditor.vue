<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { api } from '@/lib/api'
import { toast } from '@/lib/toast'
import { confirm } from '@/lib/confirm'
import { Plus, Trash2, Loader2, Sparkles } from 'lucide-vue-next'
import type { Formation, Student, Gender } from '@/types'

// Reusable formation stage editor: formation list + CRUD, drag-to-place, AI
// suggestions, and quick layouts — for one routine's set of dancers. Used by
// Choreography (dancers = recital participants) and the Musical Planner
// (dancers = the number's explicit cast).
const props = withDefaults(
  defineProps<{
    routineId: number | null
    dancers: Student[]
    emptyHint?: string
  }>(),
  { emptyHint: 'No dancers to arrange yet.' },
)

// --- Formations ---
const formations = ref<Formation[]>([])
const selectedFormationId = ref<number | null>(null)
const selectedFormation = computed(
  () => formations.value.find((f) => f.id === selectedFormationId.value) ?? null,
)

async function loadFormations() {
  if (!props.routineId) {
    formations.value = []
    selectedFormationId.value = null
    return
  }
  try {
    const { data } = await api.get<Formation[]>('/formations', {
      params: { routineId: props.routineId },
    })
    formations.value = data ?? []
  } catch {
    formations.value = []
  }
  if (!formations.value.some((f) => f.id === selectedFormationId.value)) {
    selectedFormationId.value = formations.value[0]?.id ?? null
  }
}

watch(() => props.routineId, loadFormations, { immediate: true })

// --- Stage geometry (SVG user units) ---
const VB_W = 320
const VB_H = 210
const GX0 = 16
const GY0 = 28
const GW = VB_W - GX0 * 2
const GH = 150
const GY1 = GY0 + GH
const UNIT = GW / 20
const NODE_R = 9

const GENDER_FILL = { Boys: '#2563eb', Girls: '#db2777', none: '#94a3b8' }
function genderFill(g?: Gender | null): string {
  return g === 'Boys' ? GENDER_FILL.Boys : g === 'Girls' ? GENDER_FILL.Girls : GENDER_FILL.none
}
function genderText(g?: Gender | null): string {
  return g ? '#ffffff' : '#0f172a'
}
function initials(s: Student): string {
  const f = s.firstName?.[0] ?? ''
  const l = s.lastName?.[0] ?? ''
  return (f + l).toUpperCase() || '?'
}

// --- Coordinates ---
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

watch(selectedFormationId, () => {
  coordMap.value = parseCoords(selectedFormation.value?.studentCoordinates)
})

// Node list = the routine's dancers, positioned from coordMap (default spread).
const nodes = computed(() =>
  props.dancers.map((s, i) => {
    const c = coordMap.value[String(s.id)]
    return {
      student: s,
      x: c?.x ?? 50,
      y: c?.y ?? 10 + ((i * 12) % 80),
      placed: !!c,
    }
  }),
)

// --- Formation management ---
async function addFormation() {
  if (!props.routineId) return
  const payload: Omit<Formation, 'id'> = {
    routineId: props.routineId,
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
  if (!coordMap.value[String(studentId)]) {
    const node = nodes.value.find((n) => n.student.id === studentId)
    coordMap.value[String(studentId)] = { x: node?.x ?? 50, y: node?.y ?? 50 }
  }
}

function onPointerMove(e: PointerEvent) {
  if (draggingId.value === null || !stageRef.value) return
  const rect = stageRef.value.getBoundingClientRect()
  if (rect.width === 0 || rect.height === 0) return
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

// --- AI formation suggestion + quick layouts ---
const aiConfigured = ref(true)
const aiDescription = ref('')
const suggesting = ref(false)

const rosterSummary = computed(() => {
  const boys = props.dancers.filter((s) => s.gender === 'Boys').length
  const girls = props.dancers.filter((s) => s.gender === 'Girls').length
  const other = props.dancers.length - boys - girls
  const parts: string[] = []
  if (boys) parts.push(`${boys} ${boys === 1 ? 'boy' : 'boys'}`)
  if (girls) parts.push(`${girls} ${girls === 1 ? 'girl' : 'girls'}`)
  if (other) parts.push(`${other} more`)
  return parts.join(', ')
})

async function probeAiConfig() {
  try {
    const { data } = await api.post('/formations/suggest', { dancers: [], description: '' })
    aiConfigured.value = data?.configured !== false
  } catch {
    aiConfigured.value = false
  }
}

function applyLayout(coords: CoordMap) {
  coordMap.value = coords
  saveFormation()
}

async function suggestFormation() {
  if (!selectedFormation.value || props.dancers.length === 0) return
  suggesting.value = true
  try {
    const dancers = props.dancers.map((s) => ({
      studentId: s.id,
      gender: s.gender ?? null,
      firstName: s.firstName ?? null,
    }))
    const { data } = await api.post('/formations/suggest', {
      dancers,
      description: aiDescription.value,
    })
    if (data?.configured === false) {
      aiConfigured.value = false
      toast.error('AI isn’t set up yet — use a Quick layout for now.')
      return
    }
    if (!data?.ok || !data.coordinates) {
      toast.error('Couldn’t generate a formation. Try again, or use a Quick layout.')
      return
    }
    applyLayout({ ...(data.coordinates as CoordMap) })
    toast.success('Formation suggested — drag any dancer to fine-tune.')
  } catch {
    toast.error('Couldn’t generate a formation. Try again, or use a Quick layout.')
  } finally {
    suggesting.value = false
  }
}

// Deterministic quick layouts (free, offline; also the AI fallback).
function centeredRow(count: number): number[] {
  if (count <= 1) return [50]
  const left = 15
  const step = (85 - left) / (count - 1)
  return Array.from({ length: count }, (_, i) => left + i * step)
}

function layoutRows(rows: number) {
  const list = props.dancers
  const n = list.length
  if (n === 0) return
  const perRow = Math.ceil(n / rows)
  const coords: CoordMap = {}
  let idx = 0
  for (let r = 0; r < rows && idx < n; r++) {
    const countThis = Math.min(perRow, n - idx)
    const xs = centeredRow(countThis)
    const y = rows === 1 ? 55 : 25 + r * (55 / (rows - 1))
    for (let c = 0; c < countThis; c++, idx++) coords[String(list[idx].id)] = { x: xs[c], y }
  }
  applyLayout(coords)
}

function layoutStaggered() {
  const list = props.dancers
  const n = list.length
  if (n === 0) return
  const coords: CoordMap = {}
  const perRow = Math.ceil(n / 2)
  let idx = 0
  for (let r = 0; r < 2 && idx < n; r++) {
    const countThis = Math.min(perRow, n - idx)
    const xs = centeredRow(countThis)
    const offset = r === 1 && xs.length > 1 ? (xs[1] - xs[0]) / 2 : 0
    const y = 30 + r * 30
    for (let c = 0; c < countThis; c++, idx++)
      coords[String(list[idx].id)] = { x: Math.min(92, xs[c] + offset), y }
  }
  applyLayout(coords)
}

function layoutV() {
  const list = props.dancers
  const n = list.length
  if (n === 0) return
  const coords: CoordMap = {}
  const apexX = 50
  const apexY = 22
  const half = Math.max(1, Math.ceil((n - 1) / 2))
  let li = 0
  let ri = 0
  list.forEach((s, i) => {
    if (i === 0) {
      coords[String(s.id)] = { x: apexX, y: apexY }
      return
    }
    const goLeft = i % 2 === 1
    const rank = goLeft ? ++li : ++ri
    const t = rank / half
    coords[String(s.id)] = { x: apexX + (goLeft ? -33 : 33) * t, y: apexY + 55 * t }
  })
  applyLayout(coords)
}

onMounted(probeAiConfig)
</script>

<template>
  <div class="flex flex-1 flex-col">
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

    <!-- AI suggestion + quick layouts -->
    <div
      v-if="selectedFormation"
      class="mb-4 space-y-2 rounded-md border border-border bg-muted/30 p-3"
    >
      <p v-if="dancers.length === 0" class="text-xs text-muted-foreground">
        {{ emptyHint }}
      </p>
      <template v-else>
        <div v-if="aiConfigured" class="space-y-1.5">
          <p class="text-xs font-medium text-muted-foreground">
            Suggest with AI · arranging {{ rosterSummary }}
          </p>
          <div class="flex items-center gap-2">
            <input
              v-model="aiDescription"
              placeholder="Describe the number — e.g. “upbeat jazz, boys down center”"
              class="min-w-0 flex-1 rounded-md border border-border bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
              @keyup.enter="suggestFormation"
            />
            <button
              class="inline-flex shrink-0 items-center gap-1.5 rounded-md bg-primary px-3 py-1.5 text-sm font-medium text-primary-foreground hover:opacity-90 disabled:opacity-50"
              :disabled="suggesting"
              @click="suggestFormation"
            >
              <Loader2 v-if="suggesting" class="h-4 w-4 animate-spin" />
              <Sparkles v-else class="h-4 w-4" />
              Suggest
            </button>
          </div>
          <p class="text-[11px] italic text-muted-foreground">
            * AI-generated — results can vary. Review and drag dancers to adjust.
          </p>
        </div>
        <div class="flex flex-wrap items-center gap-1.5">
          <span class="text-xs text-muted-foreground">Quick layouts:</span>
          <button
            class="rounded-md border border-border px-2 py-1 text-xs hover:bg-accent"
            @click="layoutRows(1)"
          >
            One line
          </button>
          <button
            class="rounded-md border border-border px-2 py-1 text-xs hover:bg-accent"
            @click="layoutRows(2)"
          >
            Two rows
          </button>
          <button
            class="rounded-md border border-border px-2 py-1 text-xs hover:bg-accent"
            @click="layoutRows(3)"
          >
            Three rows
          </button>
          <button
            class="rounded-md border border-border px-2 py-1 text-xs hover:bg-accent"
            @click="layoutStaggered"
          >
            Staggered
          </button>
          <button
            class="rounded-md border border-border px-2 py-1 text-xs hover:bg-accent"
            @click="layoutV"
          >
            V-shape
          </button>
        </div>
      </template>
    </div>

    <!-- Stage -->
    <div
      v-if="!selectedFormation"
      class="flex flex-1 items-center justify-center rounded-md border border-dashed border-border p-8 text-center text-sm text-muted-foreground"
    >
      No formations yet. Click "Add" to create one.
    </div>
    <div v-else class="relative w-full">
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
        <!-- grid lines -->
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
          <title>{{ `${n.student.firstName} ${n.student.lastName}`.trim() }}</title>
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

      <div class="mt-3 flex flex-wrap items-center justify-between gap-2">
        <p class="text-xs text-muted-foreground">
          Drag nodes to position dancers. {{ dancers.length }} dancer(s). Positions save
          automatically.
        </p>
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
    </div>
  </div>
</template>
