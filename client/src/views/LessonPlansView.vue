<script setup lang="ts">
import PagePlaceholder from '@/components/layout/PagePlaceholder.vue'
</script>

<template>
  <PagePlaceholder
    title="Lesson Plans"
    description="Week-over-week lesson planning per class."
    :planned="[
      'Three-column board: Covered This Week / Planned for Next Week / Notes',
      'Dated entries, newest first, preserving history',
      'Optional pre-fill of Covered This Week from prior week\'s plan',
    ]"
  />
</template>
