<script setup lang="ts">
import PagePlaceholder from '@/components/layout/PagePlaceholder.vue'
</script>

<template>
  <PagePlaceholder
    title="Attendance"
    description="Rapid daily attendance tracking with a 3-state toggle."
    :planned="[
      'Student list for a selected date with Present / Absent / Excused toggle',
      'Amber warning when attendance drops below 75% over a rolling 4-week window',
      'Slide-out Sheet with Markdown class notes bound to the session date',
    ]"
  />
</template>
