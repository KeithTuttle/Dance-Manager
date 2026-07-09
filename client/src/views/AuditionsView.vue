<script setup lang="ts">
import PagePlaceholder from '@/components/layout/PagePlaceholder.vue'
</script>

<template>
  <PagePlaceholder
    title="Mock Auditions"
    description="Spreadsheet-style candidate scoring with configurable skills."
    :planned="[
      'One row per candidate; header shows 1-5 scale and open spots',
      'Dynamic per-audition skill columns (add/remove updates all candidates)',
      'Live-calculating Average column',
      'Yes/No/Undecided decision control and free-text notes',
    ]"
  />
</template>
