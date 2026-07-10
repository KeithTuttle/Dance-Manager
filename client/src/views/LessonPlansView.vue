<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { ChevronDown, Plus, Loader2, NotebookPen } from 'lucide-vue-next'
import { api } from '@/lib/api'
import { useStudioStore } from '@/stores/studio'
import type { DanceClass, LessonPlanEntry } from '@/types'
import type { LessonPlanField, NewLessonPlanEntry } from './lessonPlans/types'

const studioStore = useStudioStore()

const classes = ref<DanceClass[]>([])
const selectedClassId = ref<number | null>(null)
const classMenuOpen = ref(false)

const entries = ref<LessonPlanEntry[]>([])
const loadingClasses = ref(false)
const loadingEntries = ref(false)
const adding = ref(false)

const selectedClass = computed(
  () => classes.value.find((c) => c.id === selectedClassId.value) ?? null,
)

// --- Date helpers -----------------------------------------------------------

/** ISO (yyyy-mm-dd) for the upcoming Monday, or today if today is Monday. */
function nextMondayIso(): string {
  const d = new Date()
  const day = d.getDay() // 0=Sun ... 1=Mon
  const delta = (8 - day) % 7 // days until next Monday; 0 if today is Monday
  d.setDate(d.getDate() + delta)
  return toIsoDate(d)
}

function toIsoDate(d: Date): string {
  const y = d.getFullYear()
  const m = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${y}-${m}-${day}`
}

/** Human-friendly WeekOf label, e.g. "Mon, Jul 13, 2026". */
function formatWeekOf(iso: string): string {
  const [y, m, d] = iso.split('-').map(Number)
  if (!y || !m || !d) return iso
  const date = new Date(y, m - 1, d)
  return date.toLocaleDateString(undefined, {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  })
}

// --- Data loading -----------------------------------------------------------

async function loadClasses() {
  const studioId = studioStore.selectedStudioId
  classes.value = []
  selectedClassId.value = null
  entries.value = []
  if (studioId === null) return

  loadingClasses.value = true
  try {
    const { data } = await api.get<DanceClass[]>('/classes', { params: { studioId } })
    classes.value = data
    if (data.length > 0) {
      selectedClassId.value = data[0].id
    }
  } catch {
    classes.value = []
  } finally {
    loadingClasses.value = false
  }
}

async function loadEntries() {
  entries.value = []
  if (selectedClassId.value === null) return

  loadingEntries.value = true
  try {
    const { data } = await api.get<LessonPlanEntry[]>('/lessonplanentries', {
      params: { classId: selectedClassId.value },
    })
    entries.value = data
  } catch {
    entries.value = []
  } finally {
    loadingEntries.value = false
  }
}

function pickClass(id: number) {
  selectedClassId.value = id
  classMenuOpen.value = false
}

// --- Mutations --------------------------------------------------------------

async function addWeek() {
  if (selectedClassId.value === null || adding.value) return

  // Continuity: pre-fill "Covered This Week" from the most recent week's plan.
  const prior = entries.value[0] // newest-first
  const payload: NewLessonPlanEntry = {
    classId: selectedClassId.value,
    weekOf: nextMondayIso(),
    coveredThisWeek: prior?.plannedNextWeek ?? null,
    plannedNextWeek: null,
    notes: null,
  }

  adding.value = true
  try {
    const { data } = await api.post<LessonPlanEntry>('/lessonplanentries', payload)
    // Re-sort newest-first in case the new week isn't strictly the latest.
    entries.value = [data, ...entries.value].sort((a, b) => b.weekOf.localeCompare(a.weekOf))
  } catch {
    // DB unavailable: leave state untouched, no crash.
  } finally {
    adding.value = false
  }
}

async function saveField(entry: LessonPlanEntry, field: LessonPlanField, event: Event) {
  const value = (event.target as HTMLTextAreaElement).value
  if ((entry[field] ?? '') === value) return // no change
  entry[field] = value

  try {
    await api.put(`/lessonplanentries/${entry.id}`, { ...entry })
  } catch {
    // DB unavailable: keep the local edit visible, no crash.
  }
}

// --- Reactions --------------------------------------------------------------

onMounted(async () => {
  if (studioStore.studios.length === 0) {
    await studioStore.fetchStudios().catch(() => {})
  }
  await loadClasses()
})

watch(() => studioStore.selectedStudioId, loadClasses)
watch(selectedClassId, loadEntries)
</script>

<template>
  <div class="mx-auto max-w-6xl px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
    <!-- Header -->
    <header class="mb-6 border-b border-border pb-4">
      <div class="flex items-center justify-between">
        <h1 class="text-xl font-semibold tracking-tight">Lesson Plans</h1>
        <span class="text-sm text-muted-foreground">
          {{ studioStore.selectedStudio?.name ?? 'No studio selected' }}
        </span>
      </div>
      <p class="mt-1 text-sm text-muted-foreground">
        Week-over-week lesson planning per class.
      </p>
    </header>

    <!-- Controls: class selector + add week -->
    <div class="mb-4 flex flex-wrap items-center gap-3">
      <div class="relative">
        <button
          class="flex min-w-56 items-center justify-between gap-3 rounded-md border border-border px-3 py-2 text-sm hover:bg-accent disabled:cursor-not-allowed disabled:opacity-50"
          :disabled="loadingClasses || classes.length === 0"
          @click="classMenuOpen = !classMenuOpen"
        >
          <span class="truncate">
            {{
              loadingClasses
                ? 'Loading classes…'
                : (selectedClass?.name ?? 'No classes')
            }}
          </span>
          <ChevronDown class="h-4 w-4 shrink-0 text-muted-foreground" />
        </button>
        <div
          v-if="classMenuOpen && classes.length > 0"
          class="absolute left-0 z-20 mt-1 max-h-64 w-full min-w-56 overflow-y-auto rounded-md border border-border bg-popover p-1 shadow-md"
        >
          <button
            v-for="c in classes"
            :key="c.id"
            class="flex w-full items-center rounded-sm px-2 py-1.5 text-left text-sm hover:bg-accent"
            :class="c.id === selectedClassId ? 'font-medium' : ''"
            @click="pickClass(c.id)"
          >
            <span class="truncate">{{ c.name }}</span>
          </button>
        </div>
      </div>

      <button
        class="flex items-center gap-2 rounded-md border border-border bg-foreground px-3 py-2 text-sm font-medium text-background transition-colors hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-50"
        :disabled="selectedClassId === null || adding"
        @click="addWeek"
      >
        <Loader2 v-if="adding" class="h-4 w-4 animate-spin" />
        <Plus v-else class="h-4 w-4" />
        Add week
      </button>
    </div>

    <!-- Board -->
    <section class="overflow-hidden rounded-lg border border-border">
      <!-- Column headers -->
      <div
        class="grid grid-cols-[10rem_1fr_1fr_1fr] border-b border-border bg-muted/40 text-xs font-medium uppercase tracking-wide text-muted-foreground"
      >
        <div class="px-4 py-3">Week of</div>
        <div class="border-l border-border px-4 py-3">Covered This Week</div>
        <div class="border-l border-border px-4 py-3">Planned for Next Week</div>
        <div class="border-l border-border px-4 py-3">Notes</div>
      </div>

      <!-- Loading -->
      <div
        v-if="loadingEntries"
        class="flex items-center gap-2 px-4 py-10 text-sm text-muted-foreground"
      >
        <Loader2 class="h-4 w-4 animate-spin" />
        Loading lesson plan…
      </div>

      <!-- Empty state -->
      <div
        v-else-if="entries.length === 0"
        class="flex flex-col items-center gap-3 px-4 py-14 text-center text-sm text-muted-foreground"
      >
        <NotebookPen class="h-8 w-8 opacity-40" />
        <p v-if="selectedClassId === null" class="font-medium text-foreground">
          Select a class to plan
        </p>
        <template v-else>
          <p class="font-medium text-foreground">No weeks planned yet</p>
          <p>Use “Add week” to start tracking what you cover each week.</p>
        </template>
      </div>

      <!-- Rows -->
      <div v-else>
        <div
          v-for="entry in entries"
          :key="entry.id"
          class="grid grid-cols-[10rem_1fr_1fr_1fr] border-b border-border last:border-b-0"
        >
          <div class="px-4 py-3 text-sm font-medium">
            {{ formatWeekOf(entry.weekOf) }}
          </div>
          <div class="border-l border-border">
            <textarea
              :value="entry.coveredThisWeek ?? ''"
              rows="3"
              placeholder="What did you cover?"
              class="h-full w-full resize-y bg-transparent px-4 py-3 text-sm leading-relaxed outline-none placeholder:text-muted-foreground/60 focus:bg-accent/40"
              @blur="saveField(entry, 'coveredThisWeek', $event)"
            />
          </div>
          <div class="border-l border-border">
            <textarea
              :value="entry.plannedNextWeek ?? ''"
              rows="3"
              placeholder="What's next?"
              class="h-full w-full resize-y bg-transparent px-4 py-3 text-sm leading-relaxed outline-none placeholder:text-muted-foreground/60 focus:bg-accent/40"
              @blur="saveField(entry, 'plannedNextWeek', $event)"
            />
          </div>
          <div class="border-l border-border">
            <textarea
              :value="entry.notes ?? ''"
              rows="3"
              placeholder="Notes"
              class="h-full w-full resize-y bg-transparent px-4 py-3 text-sm leading-relaxed outline-none placeholder:text-muted-foreground/60 focus:bg-accent/40"
              @blur="saveField(entry, 'notes', $event)"
            />
          </div>
        </div>
      </div>
    </section>
  </div>
</template>
