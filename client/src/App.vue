<script setup lang="ts">
import { onMounted } from 'vue'
import AppSidebar from '@/components/layout/AppSidebar.vue'
import { useStudioStore } from '@/stores/studio'

const studioStore = useStudioStore()

onMounted(() => {
  // Best-effort: won't error the UI if the API isn't running yet.
  studioStore.fetchStudios().catch(() => {})
})
</script>

<template>
  <div class="flex h-screen w-full overflow-hidden bg-background text-foreground">
    <AppSidebar />
    <main class="flex-1 overflow-y-auto">
      <RouterView />
    </main>
  </div>
</template>
