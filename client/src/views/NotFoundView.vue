<script setup lang="ts">
import { RouterLink } from 'vue-router'
import { Compass } from 'lucide-vue-next'
</script>

<template>
  <div class="mx-auto flex min-h-full max-w-md flex-col items-center justify-center px-6 py-24 text-center">
    <div class="mb-4 flex h-12 w-12 items-center justify-center rounded-full border border-border text-muted-foreground">
      <Compass class="h-6 w-6" />
    </div>
    <p class="text-sm font-medium text-muted-foreground">404</p>
    <h1 class="mt-1 text-xl font-semibold tracking-tight">Page not found</h1>
    <p class="mt-2 text-sm text-muted-foreground">
      That page doesn’t exist or may have moved.
    </p>
    <RouterLink
      to="/"
      class="mt-6 inline-flex items-center rounded-md bg-primary px-3 py-2 text-sm font-medium text-primary-foreground hover:opacity-90"
    >
      Back to Dashboard
    </RouterLink>
  </div>
</template>
