<script setup lang="ts">
import { ref, computed } from 'vue'
import { useStudioStore } from '@/stores/studio'
import {
  LayoutDashboard,
  Users,
  CalendarCheck,
  BarChart3,
  NotebookPen,
  Music,
  Theater,
  ClipboardList,
  ChevronsLeft,
  ChevronsRight,
  Check,
  ChevronDown,
} from 'lucide-vue-next'

const studioStore = useStudioStore()
const collapsed = ref(false)
const studioMenuOpen = ref(false)

const nav = [
  { to: '/', label: 'Dashboard', icon: LayoutDashboard },
  { to: '/roster', label: 'Roster', icon: Users },
  { to: '/attendance', label: 'Attendance', icon: CalendarCheck },
  { to: '/analytics', label: 'Analytics', icon: BarChart3 },
  { to: '/lesson-plans', label: 'Lesson Plans', icon: NotebookPen },
  { to: '/choreography', label: 'Choreography', icon: Music },
  { to: '/recital', label: 'Recital', icon: Theater },
  { to: '/auditions', label: 'Auditions', icon: ClipboardList },
]

const selectedName = computed(() => studioStore.selectedStudio?.name ?? 'Select studio')

function pick(id: number) {
  studioStore.selectStudio(id)
  studioMenuOpen.value = false
}
</script>

<template>
  <aside
    :class="[
      'flex h-full flex-col border-r border-border bg-background transition-all duration-200',
      collapsed ? 'w-16' : 'w-64',
    ]"
  >
    <!-- Brand + collapse toggle -->
    <div class="flex h-14 items-center justify-between border-b border-border px-3">
      <span v-if="!collapsed" class="text-sm font-semibold tracking-tight">DanceManager</span>
      <button
        class="ml-auto flex h-8 w-8 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-accent-foreground"
        :aria-label="collapsed ? 'Expand sidebar' : 'Collapse sidebar'"
        @click="collapsed = !collapsed"
      >
        <ChevronsRight v-if="collapsed" class="h-4 w-4" />
        <ChevronsLeft v-else class="h-4 w-4" />
      </button>
    </div>

    <!-- Studio selector -->
    <div v-if="!collapsed" class="relative border-b border-border p-3">
      <button
        class="flex w-full items-center justify-between rounded-md border border-border px-3 py-2 text-sm hover:bg-accent"
        @click="studioMenuOpen = !studioMenuOpen"
      >
        <span class="truncate">{{ selectedName }}</span>
        <ChevronDown class="h-4 w-4 shrink-0 text-muted-foreground" />
      </button>
      <div
        v-if="studioMenuOpen"
        class="absolute left-3 right-3 z-20 mt-1 max-h-64 overflow-y-auto rounded-md border border-border bg-popover p-1 shadow-md"
      >
        <p v-if="studioStore.studios.length === 0" class="px-2 py-1.5 text-xs text-muted-foreground">
          No studios yet
        </p>
        <button
          v-for="s in studioStore.studios"
          :key="s.id"
          class="flex w-full items-center justify-between rounded-sm px-2 py-1.5 text-sm hover:bg-accent"
          @click="pick(s.id)"
        >
          <span class="truncate">{{ s.name }}</span>
          <Check v-if="s.id === studioStore.selectedStudioId" class="h-4 w-4" />
        </button>
      </div>
    </div>

    <!-- Nav -->
    <nav class="flex-1 space-y-1 overflow-y-auto p-2">
      <RouterLink
        v-for="item in nav"
        :key="item.to"
        :to="item.to"
        class="flex items-center gap-3 rounded-md px-3 py-2 text-sm text-muted-foreground transition-colors hover:bg-accent hover:text-accent-foreground"
        active-class="bg-accent text-accent-foreground font-medium"
        :title="collapsed ? item.label : undefined"
      >
        <component :is="item.icon" class="h-4 w-4 shrink-0" />
        <span v-if="!collapsed">{{ item.label }}</span>
      </RouterLink>
    </nav>
  </aside>
</template>
