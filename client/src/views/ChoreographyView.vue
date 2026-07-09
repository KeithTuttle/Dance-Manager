<script setup lang="ts">
import PagePlaceholder from '@/components/layout/PagePlaceholder.vue'
</script>

<template>
  <PagePlaceholder
    title="Choreography & Formations"
    description="Split-screen routine documentation and stage-formation mapper."
    :planned="[
      'Left: embedded YouTube/Vimeo player + rich-text counts & lyrical cues',
      'Right: interactive 2D stage grid with draggable student-initial nodes',
      'Formation nodes limited to students marked IsParticipating = true',
      'Generate Sub Handoff PDF (roster + notes + music + formation snapshots)',
    ]"
  />
</template>
