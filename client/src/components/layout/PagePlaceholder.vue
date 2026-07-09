<script setup lang="ts">
import { useStudioStore } from '@/stores/studio'

defineProps<{
  title: string
  description: string
  planned?: string[]
}>()

const studioStore = useStudioStore()
</script>

<template>
  <div class="mx-auto max-w-6xl px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
    <header class="mb-6 border-b border-border pb-4">
      <div class="flex items-center justify-between">
        <h1 class="text-xl font-semibold tracking-tight">{{ title }}</h1>
        <span class="text-sm text-muted-foreground">
          {{ studioStore.selectedStudio?.name ?? 'No studio selected' }}
        </span>
      </div>
      <p class="mt-1 text-sm text-muted-foreground">{{ description }}</p>
    </header>

    <section
      class="rounded-lg border border-dashed border-border p-8 text-sm text-muted-foreground"
    >
      <p class="mb-3 font-medium text-foreground">Scaffold placeholder</p>
      <p>This page is wired into routing, the studio store, and the API client, ready to build out.</p>
      <ul v-if="planned?.length" class="mt-4 list-disc space-y-1 pl-5">
        <li v-for="item in planned" :key="item">{{ item }}</li>
      </ul>
      <slot />
    </section>
  </div>
</template>
