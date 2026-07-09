<script setup lang="ts">
import PagePlaceholder from '@/components/layout/PagePlaceholder.vue'
</script>

<template>
  <PagePlaceholder
    title="Recital Logistics"
    description="Show order, costume ledger, and recital planning."
    :planned="[
      'Drag-and-drop sortable Show Order program',
      'Dense Costume Ledger: multi-column sort, filter by payment status, inline edit',
      'Recital Planning: song choices, boy/girl costume options, formations reference',
      'Quick Change Alert banner for back-to-back routines with the same student',
      'IsParticipating = false instantly excludes from ledger and mapper',
    ]"
  />
</template>
