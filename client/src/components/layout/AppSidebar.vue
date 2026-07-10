<script setup lang="ts">
import { ref, computed } from 'vue'
import { RouterLink } from 'vue-router'
import { useDark, useToggle } from '@vueuse/core'
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
  Settings,
  CircleUser,
  ChevronsLeft,
  ChevronsRight,
  Check,
  ChevronDown,
  Plus,
  Sun,
  Moon,
  X,
} from 'lucide-vue-next'
import { UserButton } from '@clerk/vue'

defineProps<{ open?: boolean }>()
const emit = defineEmits<{ close: [] }>()

const studioStore = useStudioStore()
const collapsed = ref(false) // desktop icon-only mode
const studioMenuOpen = ref(false)
const authEnabled = !!import.meta.env.VITE_CLERK_PUBLISHABLE_KEY

// Dark mode: toggles the `.dark` class Tailwind's darkMode:['class'] strategy
// looks for, and persists the choice to localStorage. Defaults to the OS
// preference (prefers-color-scheme) when nothing has been chosen yet.
const isDark = useDark()
const toggleDark = useToggle(isDark)

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

const settingsNav = [
  { to: '/settings/studios', label: 'Studios & Classes', icon: Settings },
  { to: '/settings/account', label: 'Account', icon: CircleUser },
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
      'fixed inset-y-0 left-0 z-40 flex h-full w-64 flex-col border-r border-border bg-background transition-transform duration-200',
      // Mobile: slide in/out. Desktop: always in flow, width toggles with collapse.
      'md:static md:z-auto md:translate-x-0 md:transition-all',
      open ? 'translate-x-0' : '-translate-x-full',
      collapsed ? 'md:w-16' : 'md:w-64',
    ]"
  >
    <!-- Brand + collapse/close -->
    <div class="flex h-14 items-center justify-between border-b border-border px-3">
      <span v-if="!collapsed" class="text-sm font-semibold tracking-tight">DanceManager</span>
      <!-- Desktop collapse -->
      <button
        class="ml-auto hidden h-8 w-8 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-accent-foreground md:flex"
        :aria-label="collapsed ? 'Expand sidebar' : 'Collapse sidebar'"
        @click="collapsed = !collapsed"
      >
        <ChevronsRight v-if="collapsed" class="h-4 w-4" />
        <ChevronsLeft v-else class="h-4 w-4" />
      </button>
      <!-- Mobile close -->
      <button
        class="ml-auto flex h-8 w-8 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-accent-foreground md:hidden"
        aria-label="Close menu"
        @click="emit('close')"
      >
        <X class="h-4 w-4" />
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
        <RouterLink
          to="/settings/studios"
          class="mt-1 flex w-full items-center gap-1.5 rounded-sm border-t border-border px-2 py-1.5 pt-2 text-sm text-muted-foreground hover:bg-accent hover:text-accent-foreground"
          @click="studioMenuOpen = false"
        >
          <Plus class="h-3.5 w-3.5" />
          New studio
        </RouterLink>
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
        @click="emit('close')"
      >
        <component :is="item.icon" class="h-4 w-4 shrink-0" />
        <span v-if="!collapsed" class="md:inline">{{ item.label }}</span>
      </RouterLink>

      <div class="my-2 border-t border-border" />

      <RouterLink
        v-for="item in settingsNav"
        :key="item.to"
        :to="item.to"
        class="flex items-center gap-3 rounded-md px-3 py-2 text-sm text-muted-foreground transition-colors hover:bg-accent hover:text-accent-foreground"
        active-class="bg-accent text-accent-foreground font-medium"
        :title="collapsed ? item.label : undefined"
        @click="emit('close')"
      >
        <component :is="item.icon" class="h-4 w-4 shrink-0" />
        <span v-if="!collapsed" class="md:inline">{{ item.label }}</span>
      </RouterLink>
    </nav>

    <!-- Footer: theme toggle + account -->
    <div class="space-y-1 border-t border-border p-2">
      <button
        class="flex w-full items-center gap-3 rounded-md px-3 py-2 text-sm text-muted-foreground transition-colors hover:bg-accent hover:text-accent-foreground"
        :class="collapsed ? 'justify-center' : ''"
        :aria-label="isDark ? 'Switch to light mode' : 'Switch to dark mode'"
        :title="collapsed ? (isDark ? 'Light mode' : 'Dark mode') : undefined"
        @click="toggleDark()"
      >
        <Sun v-if="isDark" class="h-4 w-4 shrink-0" />
        <Moon v-else class="h-4 w-4 shrink-0" />
        <span v-if="!collapsed">{{ isDark ? 'Light mode' : 'Dark mode' }}</span>
      </button>
      <div
        v-if="authEnabled"
        class="px-1 pt-1"
        :class="collapsed ? 'flex justify-center' : ''"
      >
        <UserButton :show-name="!collapsed" />
      </div>
    </div>
  </aside>
</template>
