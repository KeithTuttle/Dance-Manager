<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import {
  CircleUser,
  Save,
  Loader2,
  Users,
  Plus,
  Copy,
  Trash2,
  LogIn,
  Crown,
} from 'lucide-vue-next'
import { api } from '@/lib/api'
import { toast } from '@/lib/toast'
import { confirm } from '@/lib/confirm'
import type { TeamResponse, TeamMember, TeamInvitation } from '@/types'

interface SettingsResponse {
  tenantId: number
  name: string
}

const name = ref('')
const loading = ref(false)
const saving = ref(false)
const loaded = ref(false)

// --- Team state ---
const team = ref<TeamResponse | null>(null)
const inviteEmail = ref('')
const creatingInvite = ref(false)
const joinCode = ref('')
const joining = ref(false)

const isOwner = computed(() => team.value?.yourRole === 'Owner')

function memberLabel(m: TeamMember): string {
  return m.displayName || m.email || `Teacher #${m.id}`
}

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
  await loadTeam()
}

async function loadTeam() {
  try {
    const { data } = await api.get<TeamResponse>('/team')
    team.value = data
  } catch {
    team.value = null
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

// --- Invites ---
async function createInvite() {
  creatingInvite.value = true
  try {
    await api.post<TeamInvitation>('/team/invitations', {
      email: inviteEmail.value.trim() || null,
    })
    inviteEmail.value = ''
    await loadTeam()
    toast.success('Invite created — share the code with your teacher')
  } catch {
    /* api.ts surfaces the error toast */
  } finally {
    creatingInvite.value = false
  }
}

async function copyCode(code: string) {
  try {
    await navigator.clipboard.writeText(code)
    toast.success('Code copied')
  } catch {
    toast.error(`Couldn’t copy — the code is ${code}`)
  }
}

async function revokeInvite(inv: TeamInvitation) {
  if (
    !(await confirm({
      title: `Revoke invite ${inv.code}?`,
      message: 'The code will stop working immediately.',
      confirmText: 'Revoke',
      destructive: true,
    }))
  )
    return
  try {
    await api.delete(`/team/invitations/${inv.id}`)
    await loadTeam()
    toast.success('Invite revoked')
  } catch {
    /* api.ts surfaces the error toast */
  }
}

async function removeMember(m: TeamMember) {
  if (
    !(await confirm({
      title: `Remove ${memberLabel(m)} from the team?`,
      message: 'They lose access to this account’s data. Their next sign-in starts a fresh, empty account.',
      confirmText: 'Remove',
      destructive: true,
    }))
  )
    return
  try {
    await api.delete(`/team/members/${m.id}`)
    await loadTeam()
    toast.success('Member removed')
  } catch {
    /* api.ts surfaces the error toast */
  }
}

// --- Join another team ---
async function join() {
  const code = joinCode.value.trim()
  if (!code) return
  if (
    !(await confirm({
      title: 'Join this team?',
      message:
        'Your account moves into the inviting team — you’ll see their studios and data instead of your current (empty) account.',
      confirmText: 'Join',
    }))
  )
    return
  joining.value = true
  try {
    const { data } = await api.post<{ tenantName: string }>('/team/join', { code })
    toast.success(`Joined ${data.tenantName}!`)
    // Old studio selection belongs to the previous tenant — reset and reload.
    localStorage.removeItem('selectedStudioId')
    window.location.reload()
  } catch {
    /* api.ts surfaces the error toast */
  } finally {
    joining.value = false
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

    <template v-else>
      <section class="rounded-lg border border-border p-5">
        <label class="block space-y-1.5">
          <span class="text-sm font-medium">Studio-group name</span>
          <p class="text-xs text-muted-foreground">
            This is your account's display name — shown to teachers you invite.
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

      <!-- Team -->
      <section v-if="team" class="mt-6 rounded-lg border border-border p-5">
        <h2 class="inline-flex items-center gap-1.5 text-sm font-semibold">
          <Users class="h-4 w-4" /> Team
        </h2>
        <p class="mt-1 text-xs text-muted-foreground">
          Teachers who share this account's studios and data.
        </p>

        <!-- Members -->
        <ul class="mt-3 divide-y divide-border rounded-md border border-border">
          <li
            v-for="m in team.members"
            :key="m.id"
            class="flex items-center gap-2 px-3 py-2 text-sm"
          >
            <Crown v-if="m.role === 'Owner'" class="h-3.5 w-3.5 shrink-0 text-amber-500" />
            <CircleUser v-else class="h-3.5 w-3.5 shrink-0 text-muted-foreground" />
            <span class="min-w-0 flex-1 truncate">
              {{ memberLabel(m) }}
              <span v-if="m.isYou" class="text-xs text-muted-foreground">(you)</span>
            </span>
            <span class="shrink-0 text-xs text-muted-foreground">{{ m.role }}</span>
            <button
              v-if="isOwner && !m.isYou"
              class="shrink-0 rounded p-1 text-muted-foreground hover:bg-destructive/10 hover:text-destructive"
              aria-label="Remove member"
              @click="removeMember(m)"
            >
              <Trash2 class="h-3.5 w-3.5" />
            </button>
          </li>
        </ul>

        <!-- Invites (owners) -->
        <template v-if="isOwner">
          <div class="mt-4 flex flex-wrap items-center gap-2">
            <input
              v-model="inviteEmail"
              placeholder="Teacher's email (optional note)"
              class="min-w-[14rem] flex-1 rounded-md border border-border bg-background px-2.5 py-1.5 text-sm focus:outline-none focus:ring-1 focus:ring-ring"
              @keyup.enter="createInvite"
            />
            <button
              class="inline-flex items-center gap-1.5 rounded-md bg-primary px-3 py-1.5 text-sm font-medium text-primary-foreground hover:opacity-90 disabled:opacity-50"
              :disabled="creatingInvite"
              @click="createInvite"
            >
              <Loader2 v-if="creatingInvite" class="h-4 w-4 animate-spin" />
              <Plus v-else class="h-4 w-4" />
              New invite
            </button>
          </div>
          <p class="mt-1.5 text-xs text-muted-foreground">
            Share the code with your teacher: they sign up at this site, then enter it under
            Account → “Join a team”.
          </p>

          <ul v-if="team.invitations.length" class="mt-3 space-y-1.5">
            <li
              v-for="inv in team.invitations"
              :key="inv.id"
              class="flex items-center gap-2 rounded-md border border-border bg-muted/30 px-3 py-1.5 text-sm"
            >
              <code class="font-mono text-sm font-semibold tracking-wider">{{ inv.code }}</code>
              <span v-if="inv.email" class="min-w-0 flex-1 truncate text-xs text-muted-foreground">
                for {{ inv.email }}
              </span>
              <span v-else class="flex-1" />
              <button
                class="shrink-0 rounded p-1 text-muted-foreground hover:bg-accent"
                aria-label="Copy code"
                @click="copyCode(inv.code)"
              >
                <Copy class="h-3.5 w-3.5" />
              </button>
              <button
                class="shrink-0 rounded p-1 text-muted-foreground hover:bg-destructive/10 hover:text-destructive"
                aria-label="Revoke invite"
                @click="revokeInvite(inv)"
              >
                <Trash2 class="h-3.5 w-3.5" />
              </button>
            </li>
          </ul>
        </template>
      </section>

      <!-- Join a team -->
      <section class="mt-6 rounded-lg border border-border p-5">
        <h2 class="inline-flex items-center gap-1.5 text-sm font-semibold">
          <LogIn class="h-4 w-4" /> Join a team
        </h2>
        <p class="mt-1 text-xs text-muted-foreground">
          Got an invite code from another teacher? Enter it here to move into their account.
        </p>
        <div class="mt-3 flex flex-wrap items-center gap-2">
          <input
            v-model="joinCode"
            placeholder="Invite code, e.g. K7M2XW9B"
            class="min-w-[12rem] rounded-md border border-border bg-background px-2.5 py-1.5 font-mono text-sm uppercase tracking-wider focus:outline-none focus:ring-1 focus:ring-ring"
            @keyup.enter="join"
          />
          <button
            class="inline-flex items-center gap-1.5 rounded-md border border-border px-3 py-1.5 text-sm hover:bg-accent disabled:opacity-50"
            :disabled="joining || !joinCode.trim()"
            @click="join"
          >
            <Loader2 v-if="joining" class="h-4 w-4 animate-spin" />
            <LogIn v-else class="h-4 w-4" />
            Join
          </button>
        </div>
      </section>
    </template>
  </div>
</template>
