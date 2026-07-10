<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { RouterLink } from 'vue-router'
import { useStudioStore } from '@/stores/studio'
import { api } from '@/lib/api'
import type { DanceClass, Student } from '@/types'
import {
  Users,
  CalendarCheck,
  BarChart3,
  NotebookPen,
  Music,
  Theater,
  ClipboardList,
  GraduationCap,
  ShieldAlert,
  type LucideIcon,
} from 'lucide-vue-next'

const studioStore = useStudioStore()

const classes = ref<DanceClass[]>([])
const students = ref<Student[]>([])
const loading = ref(false)

// Guards against a slow earlier request overwriting a newer one when the
// selected studio changes rapidly.
let requestId = 0

const injuryCount = computed(
  () => students.value.filter((s) => s.injuryAlert === true).length,
)

async function loadData(studioId: number) {
  const token = ++requestId
  loading.value = true
  // Endpoints owned by other units — may 404/500 in isolation. Default to empty.
  let nextClasses: DanceClass[] = []
  let nextStudents: Student[] = []
  try {
    const { data } = await api.get<DanceClass[]>('/classes', {
      params: { studioId },
    })
    nextClasses = Array.isArray(data) ? data : []
  } catch {
    nextClasses = []
  }
  try {
    const { data } = await api.get<Student[]>('/students', {
      params: { studioId },
    })
    nextStudents = Array.isArray(data) ? data : []
  } catch {
    nextStudents = []
  }
  // Ignore results from a superseded request.
  if (token !== requestId) return
  classes.value = nextClasses
  students.value = nextStudents
  loading.value = false
}

function reset() {
  requestId++ // supersede any in-flight load
  classes.value = []
  students.value = []
  loading.value = false
}

watch(
  () => studioStore.selectedStudioId,
  (id) => {
    if (id != null) loadData(id)
    else reset()
  },
)

onMounted(() => {
  if (studioStore.selectedStudioId != null) {
    loadData(studioStore.selectedStudioId)
  }
})

interface Metric {
  label: string
  value: number
  icon: LucideIcon
  danger?: boolean
}

const metrics = computed<Metric[]>(() => [
  { label: 'Classes', value: classes.value.length, icon: CalendarCheck },
  { label: 'Students', value: students.value.length, icon: GraduationCap },
  { label: 'Injury alerts', value: injuryCount.value, icon: ShieldAlert, danger: true },
])

interface QuickLink {
  to: string
  title: string
  description: string
  icon: LucideIcon
}

const quickLinks: QuickLink[] = [
  { to: '/roster', title: 'Roster', description: 'Students, classes and contact details.', icon: Users },
  { to: '/attendance', title: 'Attendance', description: 'Track presence session by session.', icon: CalendarCheck },
  { to: '/analytics', title: 'Analytics', description: 'Enrollment and attendance insights.', icon: BarChart3 },
  { to: '/lesson-plans', title: 'Lesson Plans', description: 'Weekly coverage and planning notes.', icon: NotebookPen },
  { to: '/choreography', title: 'Choreography', description: 'Routines, formations and music.', icon: Music },
  { to: '/recital', title: 'Recital', description: 'Show order, costumes and programs.', icon: Theater },
  { to: '/auditions', title: 'Auditions', description: 'Score candidates and decide.', icon: ClipboardList },
]
</script>

<template>
  <div class="mx-auto max-w-6xl px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
    <header class="mb-6 border-b border-border pb-4">
      <div class="flex items-center justify-between">
        <h1 class="text-xl font-semibold tracking-tight">Dashboard</h1>
        <span class="text-sm text-muted-foreground">
          {{ studioStore.selectedStudio?.name ?? 'No studio selected' }}
        </span>
      </div>
      <p class="mt-1 text-sm text-muted-foreground">
        At-a-glance overview for the selected studio.
      </p>
    </header>

    <!-- No studio selected -->
    <section
      v-if="studioStore.selectedStudioId == null"
      class="rounded-lg border border-dashed border-border p-8 text-sm text-muted-foreground"
    >
      <p class="mb-1 font-medium text-foreground">No studio selected</p>
      <p>Choose a studio from the sidebar to see its overview.</p>
    </section>

    <template v-else>
      <!-- Metric cards -->
      <section class="grid grid-cols-2 gap-4 sm:grid-cols-3">
        <div
          v-for="metric in metrics"
          :key="metric.label"
          class="rounded-lg border p-4"
          :class="metric.danger ? 'border-destructive/40' : 'border-border'"
        >
          <div class="flex items-center justify-between">
            <p class="text-xs font-medium uppercase tracking-wide text-muted-foreground">
              {{ metric.label }}
            </p>
            <component
              :is="metric.icon"
              class="h-4 w-4"
              :class="metric.danger ? 'text-destructive' : 'text-muted-foreground'"
            />
          </div>
          <p
            class="mt-2 text-3xl font-semibold tabular-nums tracking-tight"
            :class="metric.danger && metric.value > 0 ? 'text-destructive' : 'text-foreground'"
          >
            <span v-if="loading" class="text-muted-foreground">—</span>
            <span v-else>{{ metric.value }}</span>
          </p>
        </div>
      </section>

      <!-- Quick links -->
      <section class="mt-8">
        <h2 class="mb-3 text-sm font-medium text-muted-foreground">Jump to</h2>
        <div class="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          <RouterLink
            v-for="link in quickLinks"
            :key="link.to"
            :to="link.to"
            class="group flex items-start gap-3 rounded-lg border border-border p-4 transition-colors hover:bg-accent"
          >
            <span
              class="flex h-9 w-9 shrink-0 items-center justify-center rounded-md border border-border text-muted-foreground group-hover:text-accent-foreground"
            >
              <component :is="link.icon" class="h-4 w-4" />
            </span>
            <div class="min-w-0">
              <p class="text-sm font-medium tracking-tight">{{ link.title }}</p>
              <p class="mt-0.5 text-sm text-muted-foreground">{{ link.description }}</p>
            </div>
          </RouterLink>
        </div>
      </section>
    </template>
  </div>
</template>
