import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { api } from '@/lib/api'
import type { Studio } from '@/types'

/**
 * Global studio-selector state. The selected studio filters classes, rosters
 * and financial data across the whole app (feature 1). Selection persists to
 * localStorage so it survives reloads.
 */
export const useStudioStore = defineStore('studio', () => {
  const studios = ref<Studio[]>([])
  const selectedStudioId = ref<number | null>(
    Number(localStorage.getItem('selectedStudioId')) || null,
  )
  const loading = ref(false)

  const selectedStudio = computed(
    () => studios.value.find((s) => s.id === selectedStudioId.value) ?? null,
  )

  async function fetchStudios() {
    loading.value = true
    try {
      const { data } = await api.get<Studio[]>('/studios')
      studios.value = data
      if (selectedStudioId.value === null && data.length > 0) {
        selectStudio(data[0].id)
      }
    } finally {
      loading.value = false
    }
  }

  function selectStudio(id: number) {
    selectedStudioId.value = id
    localStorage.setItem('selectedStudioId', String(id))
  }

  return { studios, selectedStudioId, selectedStudio, loading, fetchStudios, selectStudio }
})
