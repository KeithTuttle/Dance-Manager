<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { RouterLink } from 'vue-router'
import { Music, Plus, Trash2, ExternalLink, Users, Map as MapIcon } from 'lucide-vue-next'
import { api } from '@/lib/api'
import { confirm } from '@/lib/confirm'
import { useStudioStore } from '@/stores/studio'
import type {
  Routine,
  DanceClass,
  RecitalParticipation,
  SongChoice,
  CostumeOption,
  Gender,
} from '@/types'

const studioStore = useStudioStore()

const routines = ref<Routine[]>([])
const classes = ref<DanceClass[]>([])
const participations = ref<RecitalParticipation[]>([])
const songChoices = ref<SongChoice[]>([])
const costumeOptions = ref<CostumeOption[]>([])
const loading = ref(false)

const selectedRoutineId = ref<number | null>(null)

const classMap = computed(() => new Map(classes.value.map((c) => [c.id, c])))
const selectedRoutine = computed(() => routines.value.find((r) => r.id === selectedRoutineId.value) ?? null)
const selectedClassId = computed(() => selectedRoutine.value?.classId ?? null)

function routineLabel(r: Routine): string {
  const cls = classMap.value.get(r.classId)
  return `${cls?.name ?? 'Class'} — ${r.songTitle || 'Untitled routine'}`
}

// --- Headcount ---------------------------------------------------------------
const headcount = computed(() => {
  const classId = selectedClassId.value
  if (classId === null) return { yes: 0, no: 0 }
  let yes = 0
  let no = 0
  for (const p of participations.value) {
    if (p.classId !== classId) continue
    if (p.isParticipating) yes++
    else no++
  }
  return { yes, no }
})

const boysOptions = computed(() => costumeOptions.value.filter((o) => o.gender === 'Boys'))
const girlsOptions = computed(() => costumeOptions.value.filter((o) => o.gender === 'Girls'))

// --- Loading -----------------------------------------------------------------
async function safeGet<T>(url: string): Promise<T[]> {
  try {
    const { data } = await api.get<T[]>(url)
    return Array.isArray(data) ? data : []
  } catch {
    return []
  }
}

async function loadForRoutine() {
  const id = selectedRoutineId.value
  if (id === null) {
    songChoices.value = []
    costumeOptions.value = []
    return
  }
  const [songs, options] = await Promise.all([
    safeGet<SongChoice>(`/songchoices?routineId=${id}`),
    safeGet<CostumeOption>(`/costumeoptions?routineId=${id}`),
  ])
  songChoices.value = songs
  costumeOptions.value = options
}

async function load() {
  loading.value = true
  const studioId = studioStore.selectedStudioId
  const studioQuery = studioId ? `?studioId=${studioId}` : ''
  try {
    const [rts, cls, parts] = await Promise.all([
      safeGet<Routine>(`/routines${studioQuery}`),
      safeGet<DanceClass>(`/classes${studioQuery}`),
      safeGet<RecitalParticipation>('/recitalparticipation'),
    ])
    routines.value = rts
    classes.value = cls
    participations.value = parts
    if (!routines.value.some((r) => r.id === selectedRoutineId.value)) {
      selectedRoutineId.value = routines.value[0]?.id ?? null
    }
    await loadForRoutine()
  } finally {
    loading.value = false
  }
}

// --- Song Choices CRUD (optimistic so the UI stays usable without a DB) ------
let tempId = -1

async function addSongChoice() {
  if (selectedRoutineId.value === null) return
  const draft: SongChoice = {
    id: tempId--,
    routineId: selectedRoutineId.value,
    songTitle: '',
    artist: null,
    musicCutNotes: null,
  }
  songChoices.value.push(draft)
  try {
    const { data } = await api.post<SongChoice>('/songchoices', draft)
    Object.assign(draft, data)
  } catch {
    // Keep the local draft; persistence unavailable.
  }
}

async function saveSongChoice(choice: SongChoice) {
  if (choice.id < 0) return // not yet persisted
  try {
    await api.put(`/songchoices/${choice.id}`, choice)
  } catch {
    /* local only */
  }
}

async function deleteSongChoice(choice: SongChoice) {
  if (!(await confirm({ title: 'Delete this song choice?', confirmText: 'Delete', destructive: true })))
    return
  songChoices.value = songChoices.value.filter((c) => c.id !== choice.id)
  if (choice.id < 0) return
  try {
    await api.delete(`/songchoices/${choice.id}`)
  } catch {
    /* local only */
  }
}

// --- Costume Options CRUD ----------------------------------------------------
async function addCostumeOption(gender: Gender) {
  if (selectedRoutineId.value === null) return
  const draft: CostumeOption = {
    id: tempId--,
    routineId: selectedRoutineId.value,
    gender,
    description: '',
    photoLink: null,
    optionLink: null,
  }
  costumeOptions.value.push(draft)
  try {
    const { data } = await api.post<CostumeOption>('/costumeoptions', draft)
    Object.assign(draft, data)
  } catch {
    /* local only */
  }
}

async function saveCostumeOption(option: CostumeOption) {
  if (option.id < 0) return
  try {
    await api.put(`/costumeoptions/${option.id}`, option)
  } catch {
    /* local only */
  }
}

async function deleteCostumeOption(option: CostumeOption) {
  if (!(await confirm({ title: 'Delete this costume option?', confirmText: 'Delete', destructive: true })))
    return
  costumeOptions.value = costumeOptions.value.filter((o) => o.id !== option.id)
  if (option.id < 0) return
  try {
    await api.delete(`/costumeoptions/${option.id}`)
  } catch {
    /* local only */
  }
}

onMounted(load)
watch(() => studioStore.selectedStudioId, load)
watch(selectedRoutineId, loadForRoutine)
</script>

<template>
  <section class="space-y-6">
    <div class="flex flex-wrap items-end justify-between gap-3">
      <div>
        <h2 class="text-base font-semibold tracking-tight">Recital Planning</h2>
        <p class="text-sm text-muted-foreground">Song choices, costume options and headcount per routine.</p>
      </div>
      <select
        v-model="selectedRoutineId"
        class="h-8 min-w-[16rem] rounded-md border border-border bg-background px-2 text-sm"
      >
        <option :value="null" disabled>Select a routine…</option>
        <option v-for="r in routines" :key="r.id" :value="r.id">{{ routineLabel(r) }}</option>
      </select>
    </div>

    <div v-if="loading" class="rounded-lg border border-border p-8 text-center text-sm text-muted-foreground">
      Loading planning…
    </div>

    <div
      v-else-if="routines.length === 0"
      class="flex flex-col items-center gap-2 rounded-lg border border-dashed border-border p-10 text-center"
    >
      <Music class="h-6 w-6 text-muted-foreground" />
      <p class="text-sm font-medium">No routines yet</p>
      <p class="text-sm text-muted-foreground">Add routines to start planning songs and costumes.</p>
    </div>

    <template v-else-if="selectedRoutine">
      <!-- Headcount + formations reference -->
      <div class="grid gap-4 sm:grid-cols-2">
        <div class="rounded-lg border border-border p-4">
          <div class="mb-2 flex items-center gap-2 text-sm font-medium">
            <Users class="h-4 w-4 text-muted-foreground" />
            Participation Headcount
          </div>
          <div class="flex gap-6">
            <div>
              <p class="text-2xl font-semibold tabular-nums text-emerald-600 dark:text-emerald-400">
                {{ headcount.yes }}
              </p>
              <p class="text-xs text-muted-foreground">Participating</p>
            </div>
            <div>
              <p class="text-2xl font-semibold tabular-nums text-muted-foreground">{{ headcount.no }}</p>
              <p class="text-xs text-muted-foreground">Not participating</p>
            </div>
          </div>
        </div>

        <div class="rounded-lg border border-border p-4">
          <div class="mb-2 flex items-center gap-2 text-sm font-medium">
            <MapIcon class="h-4 w-4 text-muted-foreground" />
            Formations Reference
          </div>
          <p class="mb-3 text-xs text-muted-foreground">
            Stage formations are managed in the Choreography workspace.
          </p>
          <RouterLink
            to="/choreography"
            class="inline-flex items-center gap-1.5 rounded-md border border-border px-3 py-1.5 text-sm hover:bg-accent"
          >
            Open Formation Mapper
            <ExternalLink class="h-3.5 w-3.5" />
          </RouterLink>
        </div>
      </div>

      <!-- Song Choices -->
      <div class="rounded-lg border border-border">
        <div class="flex items-center justify-between border-b border-border px-4 py-2.5">
          <h3 class="text-sm font-semibold">Song Choices</h3>
          <button
            class="inline-flex items-center gap-1 rounded-md border border-border px-2.5 py-1 text-xs hover:bg-accent"
            @click="addSongChoice"
          >
            <Plus class="h-3.5 w-3.5" /> Add song
          </button>
        </div>
        <div v-if="songChoices.length === 0" class="px-4 py-6 text-center text-sm text-muted-foreground">
          No song choices yet.
        </div>
        <div v-else class="divide-y divide-border">
          <div v-for="song in songChoices" :key="song.id" class="grid items-center gap-2 px-4 py-2 sm:grid-cols-[1fr_1fr_1.5fr_auto]">
            <input
              v-model="song.songTitle"
              placeholder="Song title"
              class="rounded border border-border bg-background px-2 py-1 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
              @change="saveSongChoice(song)"
            />
            <input
              v-model="song.artist"
              placeholder="Artist"
              class="rounded border border-border bg-background px-2 py-1 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
              @change="saveSongChoice(song)"
            />
            <input
              v-model="song.musicCutNotes"
              placeholder="Music-cut notes"
              class="rounded border border-border bg-background px-2 py-1 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
              @change="saveSongChoice(song)"
            />
            <button
              class="justify-self-end rounded p-1.5 text-muted-foreground hover:bg-destructive/10 hover:text-destructive"
              aria-label="Delete song choice"
              @click="deleteSongChoice(song)"
            >
              <Trash2 class="h-4 w-4" />
            </button>
          </div>
        </div>
      </div>

      <!-- Costume Options -->
      <div class="grid gap-4 lg:grid-cols-2">
        <div v-for="group in (['Boys', 'Girls'] as Gender[])" :key="group" class="rounded-lg border border-border">
          <div class="flex items-center justify-between border-b border-border px-4 py-2.5">
            <h3 class="text-sm font-semibold">{{ group }} Costume Options</h3>
            <button
              class="inline-flex items-center gap-1 rounded-md border border-border px-2.5 py-1 text-xs hover:bg-accent"
              @click="addCostumeOption(group)"
            >
              <Plus class="h-3.5 w-3.5" /> Add
            </button>
          </div>
          <div
            v-if="(group === 'Boys' ? boysOptions : girlsOptions).length === 0"
            class="px-4 py-6 text-center text-sm text-muted-foreground"
          >
            No {{ group.toLowerCase() }} options yet.
          </div>
          <div v-else class="divide-y divide-border">
            <div v-for="opt in (group === 'Boys' ? boysOptions : girlsOptions)" :key="opt.id" class="flex gap-3 p-4">
              <a
                v-if="opt.photoLink"
                :href="opt.photoLink"
                target="_blank"
                rel="noopener noreferrer"
                class="shrink-0"
              >
                <img
                  :src="opt.photoLink"
                  alt="Costume preview"
                  class="h-16 w-16 rounded border border-border object-cover"
                  @error="($event.target as HTMLImageElement).style.display = 'none'"
                />
              </a>
              <div class="flex-1 space-y-1.5">
                <input
                  v-model="opt.description"
                  placeholder="Description / size notes"
                  class="w-full rounded border border-border bg-background px-2 py-1 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
                  @change="saveCostumeOption(opt)"
                />
                <input
                  v-model="opt.photoLink"
                  placeholder="Photo link (https://…)"
                  class="w-full rounded border border-border bg-background px-2 py-1 text-xs focus:outline-none focus:ring-1 focus:ring-ring"
                  @change="saveCostumeOption(opt)"
                />
                <div class="flex items-center gap-2">
                  <input
                    v-model="opt.optionLink"
                    placeholder="Vendor link (https://…)"
                    class="w-full rounded border border-border bg-background px-2 py-1 text-xs focus:outline-none focus:ring-1 focus:ring-ring"
                    @change="saveCostumeOption(opt)"
                  />
                  <a
                    v-if="opt.optionLink"
                    :href="opt.optionLink"
                    target="_blank"
                    rel="noopener noreferrer"
                    class="shrink-0 rounded p-1 text-muted-foreground hover:text-foreground"
                    aria-label="Open vendor link"
                  >
                    <ExternalLink class="h-4 w-4" />
                  </a>
                </div>
              </div>
              <button
                class="self-start rounded p-1.5 text-muted-foreground hover:bg-destructive/10 hover:text-destructive"
                aria-label="Delete costume option"
                @click="deleteCostumeOption(opt)"
              >
                <Trash2 class="h-4 w-4" />
              </button>
            </div>
          </div>
        </div>
      </div>
    </template>
  </section>
</template>
