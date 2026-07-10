<script setup lang="ts">
import { ref, computed, reactive, onMounted } from 'vue'
import { api } from '@/lib/api'
import type { Audition, AuditionCandidate, AuditionDecision } from '@/types'
import { Plus, Trash2, X, ChevronDown, Check } from 'lucide-vue-next'

// ---------------------------------------------------------------------------
// State
// ---------------------------------------------------------------------------
const auditions = ref<Audition[]>([])
const selectedAuditionId = ref<number | null>(null)
const candidates = ref<AuditionCandidate[]>([])
const loading = ref(false)
const auditionMenuOpen = ref(false)

// Create-audition form state
const showCreate = ref(false)
const newTitle = ref('')
const newDate = ref('')
const newSpots = ref<number>(1)

const newSkillName = ref('')
const newCandidateName = ref('')

const selectedAudition = computed(
  () => auditions.value.find((a) => a.id === selectedAuditionId.value) ?? null,
)

// Parsed skill columns for the selected audition.
const skills = computed<string[]>(() => {
  if (!selectedAudition.value) return []
  return parseSkills(selectedAudition.value.skillColumns)
})

const decisions: AuditionDecision[] = ['Yes', 'Undecided', 'No']

// Local per-candidate working copies of parsed Scores, keyed by candidate id.
// Keeps numeric inputs snappy without re-parsing JSON on every keystroke.
const scoreCache = reactive<Record<number, Record<string, number | null>>>({})

// ---------------------------------------------------------------------------
// JSON helpers
// ---------------------------------------------------------------------------
function parseSkills(json: string): string[] {
  try {
    const parsed = JSON.parse(json || '[]')
    return Array.isArray(parsed) ? parsed.filter((s) => typeof s === 'string') : []
  } catch {
    return []
  }
}

function parseScores(json: string): Record<string, number | null> {
  try {
    const parsed = JSON.parse(json || '{}')
    if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
      return parsed as Record<string, number | null>
    }
    return {}
  } catch {
    return {}
  }
}

function ensureScoreEntry(c: AuditionCandidate): Record<string, number | null> {
  if (!scoreCache[c.id]) {
    scoreCache[c.id] = parseScores(c.scores)
  }
  return scoreCache[c.id]
}

function averageFor(c: AuditionCandidate): number | null {
  const scores = ensureScoreEntry(c)
  const vals = skills.value
    .map((s) => scores[s])
    .filter((v): v is number => typeof v === 'number' && !Number.isNaN(v))
  if (vals.length === 0) return null
  return vals.reduce((a, b) => a + b, 0) / vals.length
}

// ---------------------------------------------------------------------------
// Data loading
// ---------------------------------------------------------------------------
async function fetchAuditions() {
  loading.value = true
  try {
    const { data } = await api.get<Audition[]>('/auditions')
    auditions.value = data
    if (selectedAuditionId.value === null && data.length > 0) {
      await selectAudition(data[0].id)
    }
  } catch {
    auditions.value = []
  } finally {
    loading.value = false
  }
}

async function fetchCandidates(auditionId: number) {
  try {
    const { data } = await api.get<AuditionCandidate[]>('/auditioncandidates', {
      params: { auditionId },
    })
    candidates.value = data
    // Reset & prime the score cache for this audition.
    for (const key of Object.keys(scoreCache)) delete scoreCache[Number(key)]
    for (const c of data) scoreCache[c.id] = parseScores(c.scores)
  } catch {
    candidates.value = []
  }
}

async function selectAudition(id: number) {
  selectedAuditionId.value = id
  auditionMenuOpen.value = false
  candidates.value = []
  await fetchCandidates(id)
}

// ---------------------------------------------------------------------------
// Audition create
// ---------------------------------------------------------------------------
async function createAudition() {
  const title = newTitle.value.trim()
  if (!title) return
  const payload = {
    id: 0,
    title,
    date: newDate.value || new Date().toISOString().slice(0, 10),
    spotsAvailable: Number(newSpots.value) || 0,
    skillColumns: '[]',
  }
  try {
    const { data } = await api.post<Audition>('/auditions', payload)
    auditions.value = [data, ...auditions.value]
    showCreate.value = false
    newTitle.value = ''
    newDate.value = ''
    newSpots.value = 1
    await selectAudition(data.id)
  } catch {
    // Backend unavailable (no DB). Leave form open; nothing persisted.
  }
}

// ---------------------------------------------------------------------------
// Skill columns
// ---------------------------------------------------------------------------
async function persistAudition(a: Audition) {
  try {
    await api.put(`/auditions/${a.id}`, a)
  } catch {
    // Ignore persistence failure (no DB); local state already updated.
  }
}

async function addSkill() {
  const a = selectedAudition.value
  const name = newSkillName.value.trim()
  if (!a || !name) return
  const current = parseSkills(a.skillColumns)
  if (current.some((s) => s.toLowerCase() === name.toLowerCase())) {
    newSkillName.value = ''
    return
  }
  const next = [...current, name]
  a.skillColumns = JSON.stringify(next)
  newSkillName.value = ''
  await persistAudition(a)
}

async function removeSkill(skill: string) {
  const a = selectedAudition.value
  if (!a) return
  const next = parseSkills(a.skillColumns).filter((s) => s !== skill)
  a.skillColumns = JSON.stringify(next)
  await persistAudition(a)

  // Drop the skill from every candidate's scores and persist.
  for (const c of candidates.value) {
    const scores = ensureScoreEntry(c)
    if (skill in scores) {
      delete scores[skill]
      await persistCandidate(c)
    }
  }
}

// ---------------------------------------------------------------------------
// Candidates
// ---------------------------------------------------------------------------
async function addCandidate() {
  const a = selectedAudition.value
  const name = newCandidateName.value.trim()
  if (!a || !name) return
  const payload = {
    id: 0,
    auditionId: a.id,
    name,
    scores: '{}',
    decision: 'Undecided' as AuditionDecision,
    notes: '',
  }
  try {
    const { data } = await api.post<AuditionCandidate>('/auditioncandidates', payload)
    candidates.value = [...candidates.value, data]
    scoreCache[data.id] = {}
    newCandidateName.value = ''
  } catch {
    // No DB: cannot persist a new candidate.
  }
}

async function removeCandidate(c: AuditionCandidate) {
  try {
    await api.delete(`/auditioncandidates/${c.id}`)
  } catch {
    // Ignore; still drop from local view.
  }
  candidates.value = candidates.value.filter((x) => x.id !== c.id)
  delete scoreCache[c.id]
}

function serializeScores(c: AuditionCandidate): string {
  const scores = ensureScoreEntry(c)
  const clean: Record<string, number> = {}
  for (const skill of skills.value) {
    const v = scores[skill]
    if (typeof v === 'number' && !Number.isNaN(v)) clean[skill] = v
  }
  return JSON.stringify(clean)
}

async function persistCandidate(c: AuditionCandidate) {
  c.scores = serializeScores(c)
  try {
    await api.put(`/auditioncandidates/${c.id}`, c)
  } catch {
    // Ignore persistence failure (no DB); local state already updated.
  }
}

function onScoreInput(c: AuditionCandidate, skill: string, raw: string) {
  const scores = ensureScoreEntry(c)
  if (raw === '') {
    scores[skill] = null
    return
  }
  let n = Number(raw)
  if (Number.isNaN(n)) return
  n = Math.max(1, Math.min(5, Math.round(n)))
  scores[skill] = n
}

function setDecision(c: AuditionCandidate, decision: AuditionDecision) {
  c.decision = decision
  persistCandidate(c)
}

function decisionClass(c: AuditionCandidate, d: AuditionDecision): string {
  const active = c.decision === d
  if (!active) return 'text-muted-foreground hover:bg-accent'
  if (d === 'Yes') return 'bg-emerald-600 text-white'
  if (d === 'No') return 'bg-rose-600 text-white'
  return 'bg-accent text-accent-foreground'
}

onMounted(fetchAuditions)
</script>

<template>
  <div class="mx-auto max-w-full px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
    <!-- Header -->
    <header class="mb-6 border-b border-border pb-4">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <h1 class="text-xl font-semibold tracking-tight">Mock Auditions</h1>

        <!-- Audition selector -->
        <div class="relative">
          <button
            class="flex min-w-56 items-center justify-between gap-2 rounded-md border border-border px-3 py-2 text-sm hover:bg-accent"
            @click="auditionMenuOpen = !auditionMenuOpen"
          >
            <span class="truncate">{{ selectedAudition?.title ?? 'Select audition' }}</span>
            <ChevronDown class="h-4 w-4 shrink-0 text-muted-foreground" />
          </button>
          <div
            v-if="auditionMenuOpen"
            class="absolute right-0 z-20 mt-1 max-h-72 w-64 overflow-y-auto rounded-md border border-border bg-popover p-1 shadow-md"
          >
            <p
              v-if="auditions.length === 0"
              class="px-2 py-1.5 text-xs text-muted-foreground"
            >
              No auditions yet
            </p>
            <button
              v-for="a in auditions"
              :key="a.id"
              class="flex w-full items-center justify-between rounded-sm px-2 py-1.5 text-sm hover:bg-accent"
              @click="selectAudition(a.id)"
            >
              <span class="truncate">{{ a.title }}</span>
              <Check v-if="a.id === selectedAuditionId" class="h-4 w-4" />
            </button>
            <div class="my-1 border-t border-border"></div>
            <button
              class="flex w-full items-center gap-2 rounded-sm px-2 py-1.5 text-sm text-foreground hover:bg-accent"
              @click="auditionMenuOpen = false; showCreate = true"
            >
              <Plus class="h-4 w-4" /> New audition
            </button>
          </div>
        </div>
      </div>

      <!-- Header note: rating scale + open spots -->
      <p v-if="selectedAudition" class="mt-2 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-muted-foreground">
        <span>Rating scale: <span class="font-medium text-foreground">1–5</span> (1 = weakest, 5 = strongest)</span>
        <span>Open spots: <span class="font-medium text-foreground">{{ selectedAudition.spotsAvailable }}</span></span>
        <span v-if="selectedAudition.date">Date: <span class="font-medium text-foreground">{{ selectedAudition.date }}</span></span>
      </p>
      <p v-else class="mt-2 text-sm text-muted-foreground">
        Select or create an audition to begin scoring candidates.
      </p>
    </header>

    <!-- Create audition form -->
    <div
      v-if="showCreate"
      class="mb-6 rounded-lg border border-border p-4"
    >
      <div class="mb-3 flex items-center justify-between">
        <h2 class="text-sm font-medium">New audition</h2>
        <button
          class="flex h-7 w-7 items-center justify-center rounded-md text-muted-foreground hover:bg-accent"
          aria-label="Cancel"
          @click="showCreate = false"
        >
          <X class="h-4 w-4" />
        </button>
      </div>
      <div class="grid grid-cols-1 gap-3 sm:grid-cols-4">
        <label class="flex flex-col gap-1 text-xs text-muted-foreground sm:col-span-2">
          Title
          <input
            v-model="newTitle"
            type="text"
            placeholder="Spring Company Auditions"
            class="rounded-md border border-border bg-background px-2.5 py-1.5 text-sm text-foreground outline-none focus:border-foreground"
            @keydown.enter="createAudition"
          />
        </label>
        <label class="flex flex-col gap-1 text-xs text-muted-foreground">
          Date
          <input
            v-model="newDate"
            type="date"
            class="rounded-md border border-border bg-background px-2.5 py-1.5 text-sm text-foreground outline-none focus:border-foreground"
          />
        </label>
        <label class="flex flex-col gap-1 text-xs text-muted-foreground">
          Open spots
          <input
            v-model.number="newSpots"
            type="number"
            min="0"
            class="rounded-md border border-border bg-background px-2.5 py-1.5 text-sm text-foreground outline-none focus:border-foreground"
          />
        </label>
      </div>
      <div class="mt-3 flex justify-end">
        <button
          class="rounded-md bg-foreground px-3 py-1.5 text-sm font-medium text-background hover:opacity-90 disabled:opacity-50"
          :disabled="!newTitle.trim()"
          @click="createAudition"
        >
          Create audition
        </button>
      </div>
    </div>

    <!-- Main scoring area -->
    <template v-if="selectedAudition">
      <!-- Skill column controls -->
      <div class="mb-4 flex flex-wrap items-center gap-2">
        <span class="text-xs font-medium uppercase tracking-wide text-muted-foreground">Skills:</span>
        <span
          v-for="skill in skills"
          :key="skill"
          class="inline-flex items-center gap-1 rounded-full border border-border bg-background px-2.5 py-1 text-xs"
        >
          {{ skill }}
          <button
            class="text-muted-foreground hover:text-rose-600"
            :aria-label="`Remove ${skill}`"
            @click="removeSkill(skill)"
          >
            <X class="h-3 w-3" />
          </button>
        </span>
        <span v-if="skills.length === 0" class="text-xs text-muted-foreground">No skill columns yet</span>
        <div class="flex items-center gap-1">
          <input
            v-model="newSkillName"
            type="text"
            placeholder="Add skill…"
            class="w-32 rounded-md border border-border bg-background px-2 py-1 text-xs text-foreground outline-none focus:border-foreground"
            @keydown.enter="addSkill"
          />
          <button
            class="flex h-7 w-7 items-center justify-center rounded-md border border-border text-muted-foreground hover:bg-accent"
            aria-label="Add skill"
            :disabled="!newSkillName.trim()"
            @click="addSkill"
          >
            <Plus class="h-4 w-4" />
          </button>
        </div>
      </div>

      <!-- Spreadsheet table -->
      <div class="overflow-x-auto rounded-lg border border-border">
        <table class="w-full border-collapse text-sm">
          <thead>
            <tr class="border-b border-border bg-muted/40 text-left">
              <th class="sticky left-0 z-10 bg-muted/40 px-3 py-2 font-medium">Candidate</th>
              <th
                v-for="skill in skills"
                :key="skill"
                class="px-2 py-2 text-center font-medium"
              >
                {{ skill }}
              </th>
              <th class="px-2 py-2 text-center font-medium">Avg</th>
              <th class="px-3 py-2 text-center font-medium">Decision</th>
              <th class="px-3 py-2 font-medium">Notes</th>
              <th class="px-2 py-2"></th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="c in candidates"
              :key="c.id"
              class="border-b border-border last:border-0 hover:bg-accent/30"
            >
              <!-- Name -->
              <td class="sticky left-0 z-10 bg-background px-3 py-1.5 font-medium">
                <input
                  v-model="c.name"
                  type="text"
                  class="w-40 rounded-sm border border-transparent bg-transparent px-1 py-0.5 outline-none hover:border-border focus:border-foreground"
                  @blur="persistCandidate(c)"
                />
              </td>
              <!-- Score cells -->
              <td
                v-for="skill in skills"
                :key="skill"
                class="px-1 py-1 text-center"
              >
                <input
                  type="number"
                  min="1"
                  max="5"
                  :value="ensureScoreEntry(c)[skill] ?? ''"
                  class="w-12 rounded-sm border border-border bg-background px-1 py-1 text-center outline-none focus:border-foreground"
                  @input="onScoreInput(c, skill, ($event.target as HTMLInputElement).value)"
                  @blur="persistCandidate(c)"
                />
              </td>
              <!-- Average -->
              <td class="px-2 py-1 text-center font-medium tabular-nums">
                {{ averageFor(c) === null ? '—' : averageFor(c)!.toFixed(2) }}
              </td>
              <!-- Decision toggle -->
              <td class="px-3 py-1">
                <div class="inline-flex overflow-hidden rounded-md border border-border">
                  <button
                    v-for="d in decisions"
                    :key="d"
                    class="border-l border-border px-2 py-1 text-xs first:border-l-0 transition-colors"
                    :class="decisionClass(c, d)"
                    @click="setDecision(c, d)"
                  >
                    {{ d === 'Undecided' ? '?' : d }}
                  </button>
                </div>
              </td>
              <!-- Notes -->
              <td class="px-3 py-1">
                <input
                  v-model="c.notes"
                  type="text"
                  placeholder="Notes…"
                  class="w-48 rounded-sm border border-transparent bg-transparent px-1 py-0.5 outline-none hover:border-border focus:border-foreground"
                  @blur="persistCandidate(c)"
                />
              </td>
              <!-- Remove -->
              <td class="px-2 py-1 text-center">
                <button
                  class="flex h-7 w-7 items-center justify-center rounded-md text-muted-foreground hover:bg-accent hover:text-rose-600"
                  :aria-label="`Remove ${c.name}`"
                  @click="removeCandidate(c)"
                >
                  <Trash2 class="h-4 w-4" />
                </button>
              </td>
            </tr>

            <!-- Empty state -->
            <tr v-if="candidates.length === 0">
              <td
                :colspan="skills.length + 5"
                class="px-3 py-8 text-center text-sm text-muted-foreground"
              >
                No candidates yet. Add one below to start scoring.
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- Add candidate -->
      <div class="mt-4 flex items-center gap-2">
        <input
          v-model="newCandidateName"
          type="text"
          placeholder="Candidate name…"
          class="w-64 rounded-md border border-border bg-background px-2.5 py-1.5 text-sm text-foreground outline-none focus:border-foreground"
          @keydown.enter="addCandidate"
        />
        <button
          class="flex items-center gap-1.5 rounded-md bg-foreground px-3 py-1.5 text-sm font-medium text-background hover:opacity-90 disabled:opacity-50"
          :disabled="!newCandidateName.trim()"
          @click="addCandidate"
        >
          <Plus class="h-4 w-4" /> Add candidate
        </button>
      </div>
    </template>

    <!-- No audition selected empty state -->
    <div
      v-else-if="!showCreate"
      class="rounded-lg border border-dashed border-border p-8 text-center text-sm text-muted-foreground"
    >
      <p class="mb-3 font-medium text-foreground">No audition selected</p>
      <p class="mb-4">Create an audition to configure skill columns and score candidates.</p>
      <button
        class="inline-flex items-center gap-1.5 rounded-md border border-border px-3 py-1.5 text-sm hover:bg-accent"
        @click="showCreate = true"
      >
        <Plus class="h-4 w-4" /> New audition
      </button>
    </div>
  </div>
</template>
