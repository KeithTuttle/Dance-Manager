<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { api } from '@/lib/api'
import { useStudioStore } from '@/stores/studio'
import type { DanceClass, Routine, Student, RecitalParticipation } from '@/types'
import { toast } from '@/lib/toast'
import { confirm } from '@/lib/confirm'
import { Download, Plus, Music, Loader2, Trash2 } from 'lucide-vue-next'
import FormationEditor from '@/components/FormationEditor.vue'

const studioStore = useStudioStore()

// --- Selection state ---
const classes = ref<DanceClass[]>([])
const selectedClassId = ref<number | null>(null)
const routines = ref<Routine[]>([])
const selectedRoutineId = ref<number | null>(null)

// --- Routine detail (left panel) ---
const notesDraft = ref('')

// --- Participating students ---
const students = ref<Student[]>([])
const participations = ref<RecitalParticipation[]>([])

const generatingPdf = ref(false)

const selectedRoutine = computed(
  () => routines.value.find((r) => r.id === selectedRoutineId.value) ?? null,
)

// Students marked IsParticipating for the selected class (drive the formation editor).
const participatingStudents = computed(() => {
  const ids = new Set(
    participations.value.filter((p) => p.isParticipating).map((p) => p.studentId),
  )
  return students.value.filter((s) => ids.has(s.id))
})

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

      <!-- RIGHT: stage grid (shared formation editor) -->
      <div class="flex flex-col overflow-y-auto bg-background p-4 sm:p-6">
        <FormationEditor
          :routine-id="selectedRoutineId"
          :dancers="participatingStudents"
          empty-hint="Mark students as participating (in the Roster) to arrange them here."
        />
      </div>
    </div>
  </div>
</template>
