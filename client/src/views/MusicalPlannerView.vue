<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { RouterLink } from 'vue-router'
import { api } from '@/lib/api'
import { useStudioStore } from '@/stores/studio'
import type { DanceClass, Routine, Student, RoutineCast } from '@/types'
import { toast } from '@/lib/toast'
import { confirm } from '@/lib/confirm'
import { Theater, Plus, Trash2, Users, ExternalLink, Settings } from 'lucide-vue-next'
import FormationEditor from '@/components/FormationEditor.vue'

// Musical Planner: model a production as the selected Studio, its cast groups as
// Classes, and each number as a Routine whose "home group" is its Class. A
// number's real cast is an explicit RoutineCast set that can span groups.
const studioStore = useStudioStore()

const classes = ref<DanceClass[]>([]) // cast groups
const routines = ref<Routine[]>([]) // numbers
const students = ref<Student[]>([]) // studio roster
const casts = ref<RoutineCast[]>([]) // cast of the selected number
const selectedRoutineId = ref<number | null>(null)

const selectedRoutine = computed(
  () => routines.value.find((r) => r.id === selectedRoutineId.value) ?? null,
)
const classMap = computed(() => new Map(classes.value.map((c) => [c.id, c])))

// Numbers grouped by home group (class), groups in name order.
const numbersByGroup = computed(() => {
  const groups = [...classes.value].sort((a, b) => a.name.localeCompare(b.name))
  return groups.map((g) => ({
    group: g,
    numbers: routines.value
      .filter((r) => r.classId === g.id)
      .sort((a, b) => (a.songTitle || '').localeCompare(b.songTitle || '')),
  }))
})

// --- Cast of the selected number ---
const castIds = computed(() => new Set(casts.value.map((c) => c.studentId)))
const castStudents = computed(() => students.value.filter((s) => castIds.value.has(s.id)))

// Roster shown in the cast picker — all studio students, or one group when filtered.
const rosterFilterClassId = ref<number | null>(null)
const filteredRoster = ref<Student[]>([])
const pickerRoster = computed(() =>
  rosterFilterClassId.value == null ? students.value : filteredRoster.value,
)

watch(rosterFilterClassId, async (id) => {
  if (id == null) {
    filteredRoster.value = []
    return
  }
  try {
    const { data } = await api.get<Student[]>('/students', { params: { classId: id } })
    filteredRoster.value = data ?? []
  } catch {
    filteredRoster.value = []
  }
})

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
}

async function loadRoutines() {
  const studioId = studioStore.selectedStudioId
  if (!studioId) {
    routines.value = []
    selectedRoutineId.value = null
    return
  }
  try {
    const { data } = await api.get<Routine[]>('/routines', { params: { studioId } })
    routines.value = data ?? []
  } catch {
    routines.value = []
  }
  if (!routines.value.some((r) => r.id === selectedRoutineId.value)) {
    selectedRoutineId.value = routines.value[0]?.id ?? null
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

async function loadCast() {
  rosterFilterClassId.value = null
  if (!selectedRoutineId.value) {
    casts.value = []
    return
  }
  try {
    const { data } = await api.get<RoutineCast[]>('/routinecast', {
      params: { routineId: selectedRoutineId.value },
    })
    casts.value = data ?? []
  } catch {
    casts.value = []
  }
}

async function loadAll() {
  await Promise.all([loadClasses(), loadRoutines(), loadStudents()])
}

watch(() => studioStore.selectedStudioId, loadAll)
watch(selectedRoutineId, loadCast)

onMounted(async () => {
  if (studioStore.studios.length === 0) {
    try {
      await studioStore.fetchStudios()
    } catch {
      /* graceful: empty */
    }
  }
  await loadAll()
  await loadCast()
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
    if (selectedRoutineId.value === routine.id) {
      selectedRoutineId.value = routines.value[0]?.id ?? null
    }
    toast.success('Number deleted')
  } catch {
    /* api.ts surfaces the error toast */
  }
}

// --- Cast add / remove ---
async function toggleCast(studentId: number) {
  const routineId = selectedRoutineId.value
  if (!routineId) return
  if (castIds.value.has(studentId)) {
    casts.value = casts.value.filter((c) => c.studentId !== studentId)
    try {
      await api.delete('/routinecast', { params: { routineId, studentId } })
    } catch {
      /* api.ts surfaces the error toast */
    }
  } else {
    casts.value.push({ routineId, studentId })
    try {
      await api.post('/routinecast', { routineId, studentId })
    } catch {
      /* api.ts surfaces the error toast */
    }
  }
}

function groupNameOf(classId: number): string {
  return classMap.value.get(classId)?.name ?? 'Unknown group'
}
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
        <RouterLink
          to="/recital"
          class="inline-flex items-center gap-1.5 rounded-md border border-border px-3 py-2 text-sm hover:bg-accent"
        >
          <ExternalLink class="h-4 w-4" />
          Show order &amp; quick changes
        </RouterLink>
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

    <!-- Planner -->
    <div v-else class="grid flex-1 grid-cols-1 gap-px overflow-hidden bg-border lg:grid-cols-[minmax(0,20rem)_1fr]">
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
          <button
            class="inline-flex items-center gap-1.5 rounded-md border border-border px-2.5 py-1.5 text-sm text-muted-foreground hover:bg-destructive/10 hover:text-destructive"
            @click="deleteNumber(selectedRoutine)"
          >
            <Trash2 class="h-3.5 w-3.5" /> Delete
          </button>
        </div>

        <!-- Cast editor -->
        <div class="mb-5 space-y-2 rounded-md border border-border bg-muted/30 p-3">
          <div class="flex flex-wrap items-center justify-between gap-2">
            <p class="inline-flex items-center gap-1.5 text-sm font-medium">
              <Users class="h-4 w-4" />
              Cast
              <span class="text-xs font-normal text-muted-foreground">
                ({{ castStudents.length }} cast) — pick anyone, across groups
              </span>
            </p>
            <label class="flex items-center gap-1.5 text-xs text-muted-foreground">
              Filter
              <select
                v-model="rosterFilterClassId"
                class="rounded-md border border-border bg-background px-2 py-1 text-xs"
              >
                <option :value="null">All groups</option>
                <option v-for="c in classes" :key="c.id" :value="c.id">{{ c.name }}</option>
              </select>
            </label>
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
                castIds.has(s.id)
                  ? 'border-foreground bg-foreground text-background'
                  : 'border-border text-muted-foreground hover:bg-accent'
              "
              @click="toggleCast(s.id)"
            >
              {{ s.firstName }} {{ s.lastName }}
            </button>
          </div>
          <p class="text-[11px] text-muted-foreground">
            Cast overrides group participation for this number's formations and back-to-back quick
            changes.
          </p>
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
  </div>
</template>
