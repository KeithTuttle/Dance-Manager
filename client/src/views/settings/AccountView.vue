<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { CircleUser, Save, Loader2 } from 'lucide-vue-next'
import { api } from '@/lib/api'
import { toast } from '@/lib/toast'

interface SettingsResponse {
  tenantId: number
  name: string
}

const name = ref('')
const loading = ref(false)
const saving = ref(false)
const loaded = ref(false)

async function load() {
  loading.value = true
  try {
    const { data } = await api.get<SettingsResponse>('/settings')
    name.value = data.name
    loaded.value = true
  } catch {
    loaded.value = false
  } finally {
    loading.value = false
  }
}

async function save() {
  const trimmed = name.value.trim()
  if (!trimmed) return
  saving.value = true
  try {
    await api.put('/settings', { name: trimmed })
    toast.success('Saved')
  } catch {
    /* api.ts already surfaces the error toast */
  } finally {
    saving.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="mx-auto max-w-2xl px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
    <header class="mb-6 border-b border-border pb-4">
      <h1 class="text-xl font-semibold tracking-tight">Account</h1>
      <p class="mt-1 text-sm text-muted-foreground">Manage your studio-group account.</p>
    </header>

    <div v-if="loading" class="rounded-lg border border-border p-8 text-center text-sm text-muted-foreground">
      Loading…
    </div>

    <div
      v-else-if="!loaded"
      class="flex flex-col items-center gap-2 rounded-lg border border-dashed border-border p-10 text-center"
    >
      <CircleUser class="h-6 w-6 text-muted-foreground" />
      <p class="text-sm font-medium">Couldn’t load account settings</p>
      <p class="text-sm text-muted-foreground">The database may be offline. Try again shortly.</p>
    </div>

    <section v-else class="rounded-lg border border-border p-5">
      <label class="block space-y-1.5">
        <span class="text-sm font-medium">Studio-group name</span>
        <p class="text-xs text-muted-foreground">
          This is your account's display name — shown to anyone you invite in the future.
        </p>
        <input
          v-model="name"
          type="text"
          class="mt-2 w-full rounded-md border border-border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
        />
      </label>
      <button
        class="mt-4 inline-flex items-center gap-2 rounded-md bg-primary px-3 py-2 text-sm font-medium text-primary-foreground hover:opacity-90 disabled:opacity-50"
        :disabled="saving || !name.trim()"
        @click="save"
      >
        <Loader2 v-if="saving" class="h-4 w-4 animate-spin" />
        <Save v-else class="h-4 w-4" />
        {{ saving ? 'Saving…' : 'Save' }}
      </button>
    </section>
  </div>
</template>
