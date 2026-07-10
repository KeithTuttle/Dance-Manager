<script setup lang="ts">
import { ref, computed } from 'vue'
import { useStudioStore } from '@/stores/studio'
import ShowOrderBoard from './recital/ShowOrderBoard.vue'
import CostumeLedger from './recital/CostumeLedger.vue'
import RecitalPlanning from './recital/RecitalPlanning.vue'

type TabKey = 'show-order' | 'costume-ledger' | 'planning'

const studioStore = useStudioStore()

const tabs: { key: TabKey; label: string }[] = [
  { key: 'show-order', label: 'Show Order' },
  { key: 'costume-ledger', label: 'Costume Ledger' },
  { key: 'planning', label: 'Recital Planning' },
]

const activeTab = ref<TabKey>('show-order')
const studioName = computed(() => studioStore.selectedStudio?.name ?? 'No studio selected')
</script>

<template>
  <div class="mx-auto max-w-6xl px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
    <header class="mb-4 border-b border-border pb-4">
      <div class="flex items-center justify-between">
        <h1 class="text-xl font-semibold tracking-tight">Recital Logistics</h1>
        <span class="text-sm text-muted-foreground">{{ studioName }}</span>
      </div>
      <p class="mt-1 text-sm text-muted-foreground">
        Show order, costume ledger, and recital planning.
      </p>
    </header>

    <!-- Tabs -->
    <div role="tablist" class="mb-6 flex items-center gap-1 border-b border-border">
      <button
        v-for="tab in tabs"
        :key="tab.key"
        role="tab"
        :aria-selected="activeTab === tab.key"
        :class="[
          '-mb-px border-b-2 px-4 py-2 text-sm font-medium transition-colors',
          activeTab === tab.key
            ? 'border-foreground text-foreground'
            : 'border-transparent text-muted-foreground hover:text-foreground',
        ]"
        @click="activeTab = tab.key"
      >
        {{ tab.label }}
      </button>
    </div>

    <!-- Panels -->
    <ShowOrderBoard v-if="activeTab === 'show-order'" />
    <CostumeLedger v-else-if="activeTab === 'costume-ledger'" />
    <RecitalPlanning v-else />
  </div>
</template>
