<script setup lang="ts">
import PagePlaceholder from '@/components/layout/PagePlaceholder.vue'
</script>

<template>
  <PagePlaceholder
    title="Roster & Progression"
    description="Multi-studio roster management and the Student Progression Matrix."
    :planned="[
      'Spreadsheet-style progression matrix (milestone columns x student rows, cell status dropdown)',
      'Injury alert red badge with hover popup of movement modifications',
      'Student detail with timestamped StudentNotes list',
      'Recital participation Yes/No running tally per class',
    ]"
  />
</template>
