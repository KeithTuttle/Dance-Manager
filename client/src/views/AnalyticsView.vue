<script setup lang="ts">
import PagePlaceholder from '@/components/layout/PagePlaceholder.vue'
</script>

<template>
  <PagePlaceholder
    title="Analytics"
    description="Attendance trends and estimated pay for the selected studio."
    :planned="[
      'Monthly attendance trend line chart',
      'Total Estimated Pay metric card from studio pay rules (Hourly / PerHeadcount)',
      'Low-engagement student flags',
    ]"
  />
</template>
