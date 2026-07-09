<script setup lang="ts">
import PagePlaceholder from '@/components/layout/PagePlaceholder.vue'
</script>

<template>
  <PagePlaceholder
    title="Dashboard"
    description="At-a-glance overview for the selected studio."
    :planned="[
      'Quick counts: students, classes, upcoming recital headcount',
      'Attendance and pay summary cards',
      'Shortcuts into each feature area',
    ]"
  />
</template>
