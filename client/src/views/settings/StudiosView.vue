<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { Building2, Plus, Trash2, Rows3 } from 'lucide-vue-next'
import { api } from '@/lib/api'
import { confirm } from '@/lib/confirm'
import { toast } from '@/lib/toast'
import { useStudioStore } from '@/stores/studio'
import type { Studio, DanceClass, PayType } from '@/types'

const studioStore = useStudioStore()

const studios = ref<Studio[]>([])
const classes = ref<DanceClass[]>([])
const loading = ref(false)
const classesLoading = ref(false)

const selectedStudioId = ref<number | null>(null)
const selectedStudio = computed(
  () => studios.value.find((s) => s.id === selectedStudioId.value) ?? null,
)

const PAY_TYPES: PayType[] = ['Hourly', 'PerHeadcount']

let tempId = -1

// --- Loading -----------------------------------------------------------------
async function loadStudios() {
  loading.value = true
  try {
    const { data } = await api.get<Studio[]>('/studios')
    studios.value = Array.isArray(data) ? data : []
    if (!studios.value.some((s) => s.id === selectedStudioId.value)) {
      selectedStudioId.value = studios.value[0]?.id ?? null
    }
  } catch {
    studios.value = []
  } finally {
    loading.value = false
  }
}

async function loadClasses() {
  const studioId = selectedStudioId.value
  if (studioId === null) {
    classes.value = []
    return
  }
  classesLoading.value = true
  try {
    const { data } = await api.get<DanceClass[]>('/classes', { params: { studioId } })
    classes.value = Array.isArray(data) ? data : []
  } catch {
    classes.value = []
  } finally {
    classesLoading.value = false
  }
}

async function selectStudioForClasses(id: number) {
  selectedStudioId.value = id
  await loadClasses()
}

// --- Studio CRUD ---------------------------------------------------------------
async function addStudio() {
  const draft: Studio = {
    id: tempId--,
    name: 'New Studio',
    address: null,
    payType: 'Hourly',
    payRate: 0,
  }
  studios.value.push(draft)
  try {
    const { data } = await api.post<Studio>('/studios', draft)
    Object.assign(draft, data)
    await studioStore.fetchStudios()
    toast.success('Studio added')
    await selectStudioForClasses(draft.id)
  } catch {
    /* api.ts already surfaces the error toast; draft stays local */
  }
}

async function saveStudio(studio: Studio) {
  if (studio.id < 0) return // not yet persisted
  try {
    await api.put(`/studios/${studio.id}`, studio)
    await studioStore.fetchStudios()
    toast.success('Saved')
  } catch {
    /* api.ts already surfaces the error toast */
  }
}

async function deleteStudio(studio: Studio) {
  if (
    !(await confirm({
      title: `Delete “${studio.name}”?`,
      message: 'This permanently removes the studio and everything tied to it (classes, students, routines, etc).',
      confirmText: 'Delete',
      destructive: true,
    }))
  )
    return
  const prev = studios.value
  studios.value = studios.value.filter((s) => s.id !== studio.id)
  if (selectedStudioId.value === studio.id) {
    selectedStudioId.value = studios.value[0]?.id ?? null
    await loadClasses()
  }
  if (studio.id < 0) return
  try {
    await api.delete(`/studios/${studio.id}`)
    await studioStore.fetchStudios()
    toast.success('Studio deleted')
  } catch {
    studios.value = prev // restore on failure
  }
}

// --- Class CRUD ------------------------------------------------------------
async function addClass() {
  const studioId = selectedStudioId.value
  if (studioId === null) return
  const draft: DanceClass = { id: tempId--, studioId, name: 'New Class' }
  classes.value.push(draft)
  try {
    const { data } = await api.post<DanceClass>('/classes', draft)
    Object.assign(draft, data)
    toast.success('Class added')
  } catch {
    /* local only */
  }
}

async function saveClass(cls: DanceClass) {
  if (cls.id < 0) return
  try {
    await api.put(`/classes/${cls.id}`, cls)
    toast.success('Saved')
  } catch {
    /* api.ts already surfaces the error toast */
  }
}

async function deleteClass(cls: DanceClass) {
  if (
    !(await confirm({
      title: `Delete “${cls.name}”?`,
      message: 'This permanently removes the class and its schedule, attendance, and lesson plans.',
      confirmText: 'Delete',
      destructive: true,
    }))
  )
    return
  const prev = classes.value
  classes.value = classes.value.filter((c) => c.id !== cls.id)
  if (cls.id < 0) return
  try {
    await api.delete(`/classes/${cls.id}`)
    toast.success('Class deleted')
  } catch {
    classes.value = prev
  }
}

onMounted(async () => {
  await loadStudios()
  await loadClasses()
})
</script>

<template>
  <div class="mx-auto max-w-4xl px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
    <header class="mb-6 border-b border-border pb-4">
      <h1 class="text-xl font-semibold tracking-tight">Studios &amp; Classes</h1>
      <p class="mt-1 text-sm text-muted-foreground">
        Manage studios and their classes. Deleting a studio removes everything tied to it.
      </p>
    </header>

    <!-- Studios -->
    <section class="mb-8 rounded-lg border border-border">
      <div class="flex items-center justify-between border-b border-border px-4 py-2.5">
        <h2 class="text-sm font-semibold">Studios</h2>
        <button
          class="inline-flex items-center gap-1 rounded-md border border-border px-2.5 py-1 text-xs hover:bg-accent"
          @click="addStudio"
        >
          <Plus class="h-3.5 w-3.5" /> Add studio
        </button>
      </div>

      <div v-if="loading" class="px-4 py-6 text-center text-sm text-muted-foreground">Loading…</div>
      <div
        v-else-if="studios.length === 0"
        class="flex flex-col items-center gap-2 px-4 py-10 text-center"
      >
        <Building2 class="h-6 w-6 text-muted-foreground" />
        <p class="text-sm font-medium">No studios yet</p>
        <p class="text-sm text-muted-foreground">Add your first studio to get started.</p>
      </div>
      <div v-else class="divide-y divide-border">
        <div
          v-for="s in studios"
          :key="s.id"
          class="grid items-center gap-2 px-4 py-2.5 sm:grid-cols-[1.4fr_1.4fr_0.9fr_0.7fr_auto_auto]"
          :class="s.id === selectedStudioId ? 'bg-accent/40' : ''"
        >
          <input
            v-model="s.name"
            placeholder="Studio name"
            class="rounded border border-border bg-background px-2 py-1 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
            @change="saveStudio(s)"
          />
          <input
            v-model="s.address"
            placeholder="Address"
            class="rounded border border-border bg-background px-2 py-1 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
            @change="saveStudio(s)"
          />
          <select
            v-model="s.payType"
            class="rounded border border-border bg-background px-2 py-1 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
            @change="saveStudio(s)"
          >
            <option v-for="pt in PAY_TYPES" :key="pt" :value="pt">
              {{ pt === 'Hourly' ? 'Hourly' : 'Per Headcount' }}
            </option>
          </select>
          <input
            v-model.number="s.payRate"
            type="number"
            step="0.01"
            min="0"
            placeholder="Rate"
            class="rounded border border-border bg-background px-2 py-1 text-sm tabular-nums focus:outline-none focus:ring-1 focus:ring-ring"
            @change="saveStudio(s)"
          />
          <button
            class="inline-flex items-center gap-1 whitespace-nowrap rounded-md border border-border px-2 py-1 text-xs hover:bg-accent"
            :disabled="s.id < 0"
            @click="selectStudioForClasses(s.id)"
          >
            <Rows3 class="h-3.5 w-3.5" /> Classes
          </button>
          <button
            class="justify-self-end rounded p-1.5 text-muted-foreground hover:bg-destructive/10 hover:text-destructive"
            aria-label="Delete studio"
            @click="deleteStudio(s)"
          >
            <Trash2 class="h-4 w-4" />
          </button>
        </div>
      </div>
    </section>

    <!-- Classes for selected studio -->
    <section class="rounded-lg border border-border">
      <div class="flex flex-wrap items-center justify-between gap-2 border-b border-border px-4 py-2.5">
        <h2 class="text-sm font-semibold">
          Classes
          <span v-if="selectedStudio" class="font-normal text-muted-foreground">
            — {{ selectedStudio.name }}
          </span>
        </h2>
        <button
          class="inline-flex items-center gap-1 rounded-md border border-border px-2.5 py-1 text-xs hover:bg-accent disabled:opacity-50"
          :disabled="selectedStudioId === null"
          @click="addClass"
        >
          <Plus class="h-3.5 w-3.5" /> Add class
        </button>
      </div>

      <div v-if="selectedStudioId === null" class="px-4 py-8 text-center text-sm text-muted-foreground">
        Select a studio above to manage its classes.
      </div>
      <div v-else-if="classesLoading" class="px-4 py-6 text-center text-sm text-muted-foreground">
        Loading…
      </div>
      <div v-else-if="classes.length === 0" class="px-4 py-8 text-center text-sm text-muted-foreground">
        No classes yet for this studio.
      </div>
      <div v-else class="divide-y divide-border">
        <div v-for="c in classes" :key="c.id" class="flex items-center gap-2 px-4 py-2.5">
          <input
            v-model="c.name"
            placeholder="Class name"
            class="flex-1 rounded border border-border bg-background px-2 py-1 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
            @change="saveClass(c)"
          />
          <button
            class="rounded p-1.5 text-muted-foreground hover:bg-destructive/10 hover:text-destructive"
            aria-label="Delete class"
            @click="deleteClass(c)"
          >
            <Trash2 class="h-4 w-4" />
          </button>
        </div>
      </div>
    </section>
  </div>
</template>
