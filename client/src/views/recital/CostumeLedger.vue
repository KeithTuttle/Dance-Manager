<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import { ArrowUp, ArrowDown, Table2, Plus, Trash2 } from 'lucide-vue-next'
import { api } from '@/lib/api'
import { confirm } from '@/lib/confirm'
import { toast } from '@/lib/toast'
import { useStudioStore } from '@/stores/studio'
import type {
  CostumeRecord,
  Student,
  DanceClass,
  RecitalParticipation,
  OrderStatus,
} from '@/types'

const studioStore = useStudioStore()

const records = ref<CostumeRecord[]>([])
const students = ref<Student[]>([])
const classes = ref<DanceClass[]>([])
const participations = ref<RecitalParticipation[]>([])
const loading = ref(false)

const classFilter = ref<number | null>(null)
const paidFilter = ref<'all' | 'paid' | 'unpaid'>('all')

const orderStatuses: OrderStatus[] = ['NotOrdered', 'Ordered', 'Shipped', 'Delivered']

const studentMap = computed(() => new Map(students.value.map((s) => [s.id, s])))

function studentName(id: number): string {
  const s = studentMap.value.get(id)
  return s ? `${s.lastName}, ${s.firstName}` : `Student #${id}`
}

// --- Participation exclusion -------------------------------------------------
// A student is excluded when they are explicitly NOT participating.
// With a class filter: exclude if their record for that class is false.
// Without: exclude only if they have participation rows and none are true.
const excludedStudentIds = computed(() => {
  const excluded = new Set<number>()
  if (classFilter.value !== null) {
    for (const p of participations.value) {
      if (p.classId === classFilter.value && !p.isParticipating) excluded.add(p.studentId)
    }
    return excluded
  }
  const byStudent = new Map<number, boolean>()
  for (const p of participations.value) {
    byStudent.set(p.studentId, (byStudent.get(p.studentId) ?? false) || p.isParticipating)
  }
  for (const [id, anyTrue] of byStudent) {
    if (!anyTrue) excluded.add(id)
  }
  return excluded
})

// --- Add record ---------------------------------------------------------------
// Participating students who don't already have a costume record.
const availableStudents = computed(() => {
  const existing = new Set(records.value.map((r) => r.studentId))
  return students.value.filter((s) => !excludedStudentIds.value.has(s.id) && !existing.has(s.id))
})
const studentToAdd = ref<number | null>(null)

async function addRecord() {
  const studentId = studentToAdd.value
  if (studentId === null) return
  const payload = {
    studentId,
    costumeSize: null,
    feeAmount: 0,
    isPaid: false,
    orderStatus: 'NotOrdered' as OrderStatus,
    alterationNotes: null,
  }
  try {
    const { data } = await api.post<CostumeRecord>('/costumerecords', payload)
    records.value.push(data)
    studentToAdd.value = null
    toast.success('Costume record added')
  } catch {
    /* api.ts already surfaces the error toast */
  }
}

// --- Sorting -----------------------------------------------------------------
type SortKey = 'student' | 'costumeSize' | 'feeAmount' | 'isPaid' | 'orderStatus'
interface SortCriterion {
  key: SortKey
  dir: 'asc' | 'desc'
}
const sortCriteria = ref<SortCriterion[]>([{ key: 'student', dir: 'asc' }])

function toggleSort(key: SortKey) {
  const existing = sortCriteria.value.find((c) => c.key === key)
  const rest = sortCriteria.value.filter((c) => c.key !== key)
  if (!existing) {
    sortCriteria.value = [{ key, dir: 'asc' }, ...rest]
  } else if (existing.dir === 'asc') {
    sortCriteria.value = [{ key, dir: 'desc' }, ...rest]
  } else {
    // Third click removes this column from the sort priority list.
    sortCriteria.value = rest
  }
}

function sortIcon(key: SortKey): 'asc' | 'desc' | null {
  return sortCriteria.value.find((c) => c.key === key)?.dir ?? null
}

function sortValue(rec: CostumeRecord, key: SortKey): string | number {
  switch (key) {
    case 'student':
      return studentName(rec.studentId).toLowerCase()
    case 'costumeSize':
      return (rec.costumeSize ?? '').toLowerCase()
    case 'feeAmount':
      return rec.feeAmount
    case 'isPaid':
      return rec.isPaid ? 1 : 0
    case 'orderStatus':
      return orderStatuses.indexOf(rec.orderStatus)
  }
}

const visibleRecords = computed(() => {
  let rows = records.value.filter((r) => !excludedStudentIds.value.has(r.studentId))
  if (paidFilter.value === 'paid') rows = rows.filter((r) => r.isPaid)
  else if (paidFilter.value === 'unpaid') rows = rows.filter((r) => !r.isPaid)

  return [...rows].sort((a, b) => {
    for (const c of sortCriteria.value) {
      const va = sortValue(a, c.key)
      const vb = sortValue(b, c.key)
      if (va < vb) return c.dir === 'asc' ? -1 : 1
      if (va > vb) return c.dir === 'asc' ? 1 : -1
    }
    return 0
  })
})

const totalFee = computed(() => visibleRecords.value.reduce((sum, r) => sum + (r.feeAmount || 0), 0))
const unpaidCount = computed(() => visibleRecords.value.filter((r) => !r.isPaid).length)

// --- Data loading ------------------------------------------------------------
async function safeGet<T>(url: string): Promise<T[]> {
  try {
    const { data } = await api.get<T[]>(url)
    return Array.isArray(data) ? data : []
  } catch {
    return []
  }
}

async function loadRecords() {
  const studioId = studioStore.selectedStudioId
  if (classFilter.value !== null) {
    records.value = await safeGet<CostumeRecord>(`/costumerecords?classId=${classFilter.value}`)
  } else if (studioId) {
    records.value = await safeGet<CostumeRecord>(`/costumerecords?studioId=${studioId}`)
  } else {
    records.value = await safeGet<CostumeRecord>('/costumerecords')
  }
}

async function load() {
  loading.value = true
  const studioId = studioStore.selectedStudioId
  const studioQuery = studioId ? `?studioId=${studioId}` : ''
  try {
    const [studs, cls, parts] = await Promise.all([
      safeGet<Student>(`/students${studioQuery}`),
      safeGet<DanceClass>(`/classes${studioQuery}`),
      safeGet<RecitalParticipation>('/recitalparticipation'),
    ])
    students.value = studs
    classes.value = cls
    participations.value = parts
    await loadRecords()
  } finally {
    loading.value = false
  }
}

// --- Inline editing ----------------------------------------------------------
async function saveRecord(rec: CostumeRecord) {
  try {
    await api.put(`/costumerecords/${rec.id}`, rec)
  } catch {
    // No DB — keep the optimistic local edit.
  }
}

function onFeeInput(rec: CostumeRecord, value: string) {
  const parsed = parseFloat(value)
  rec.feeAmount = Number.isFinite(parsed) ? parsed : 0
  saveRecord(rec)
}

function togglePaid(rec: CostumeRecord) {
  rec.isPaid = !rec.isPaid
  saveRecord(rec)
}

async function deleteRecord(rec: CostumeRecord) {
  if (
    !(await confirm({
      title: `Delete ${studentName(rec.studentId)}’s costume record?`,
      confirmText: 'Delete',
      destructive: true,
    }))
  )
    return
  const prev = records.value
  records.value = records.value.filter((r) => r.id !== rec.id)
  try {
    await api.delete(`/costumerecords/${rec.id}`)
    toast.success('Record deleted')
  } catch {
    records.value = prev
  }
}

onMounted(load)
watch(() => studioStore.selectedStudioId, () => {
  classFilter.value = null
  load()
})
watch(classFilter, loadRecords)

function money(n: number): string {
  return n.toLocaleString(undefined, { style: 'currency', currency: 'USD' })
}
</script>

<template>
  <section>
    <div class="mb-4 flex flex-wrap items-end justify-between gap-3">
      <div>
        <h2 class="text-base font-semibold tracking-tight">Costume Ledger</h2>
        <p class="text-sm text-muted-foreground">
          Sizes, fees, payment and order status. Non-participating students are excluded.
        </p>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <select
          v-model="studentToAdd"
          class="h-8 min-w-[14rem] rounded-md border border-border bg-background px-2 text-sm"
        >
          <option :value="null" disabled>
            {{ availableStudents.length === 0 ? 'No students to add' : 'Add a student…' }}
          </option>
          <option v-for="s in availableStudents" :key="s.id" :value="s.id">
            {{ s.lastName }}, {{ s.firstName }}
          </option>
        </select>
        <button
          class="inline-flex h-8 items-center gap-1 rounded-md border border-border px-2.5 text-sm hover:bg-accent disabled:opacity-50"
          :disabled="studentToAdd === null"
          @click="addRecord"
        >
          <Plus class="h-3.5 w-3.5" /> Add record
        </button>
        <select
          v-model="classFilter"
          class="h-8 rounded-md border border-border bg-background px-2 text-sm"
        >
          <option :value="null">All classes</option>
          <option v-for="c in classes" :key="c.id" :value="c.id">{{ c.name }}</option>
        </select>
        <select
          v-model="paidFilter"
          class="h-8 rounded-md border border-border bg-background px-2 text-sm"
        >
          <option value="all">All payments</option>
          <option value="paid">Paid only</option>
          <option value="unpaid">Unpaid only</option>
        </select>
      </div>
    </div>

    <div v-if="loading" class="rounded-lg border border-border p-8 text-center text-sm text-muted-foreground">
      Loading ledger…
    </div>

    <div
      v-else-if="visibleRecords.length === 0"
      class="flex flex-col items-center gap-2 rounded-lg border border-dashed border-border p-10 text-center"
    >
      <Table2 class="h-6 w-6 text-muted-foreground" />
      <p class="text-sm font-medium">No costume records</p>
      <p class="text-sm text-muted-foreground">
        Costume records for participating students will appear here.
      </p>
    </div>

    <div v-else class="overflow-x-auto rounded-lg border border-border">
      <table class="w-full border-collapse text-sm">
        <thead>
          <tr class="border-b border-border bg-muted/40 text-left text-xs uppercase tracking-wide text-muted-foreground">
            <th class="px-3 py-2 font-medium">
              <button class="flex items-center gap-1 hover:text-foreground" @click="toggleSort('student')">
                Student
                <ArrowUp v-if="sortIcon('student') === 'asc'" class="h-3 w-3" />
                <ArrowDown v-else-if="sortIcon('student') === 'desc'" class="h-3 w-3" />
              </button>
            </th>
            <th class="px-3 py-2 font-medium">
              <button class="flex items-center gap-1 hover:text-foreground" @click="toggleSort('costumeSize')">
                Size
                <ArrowUp v-if="sortIcon('costumeSize') === 'asc'" class="h-3 w-3" />
                <ArrowDown v-else-if="sortIcon('costumeSize') === 'desc'" class="h-3 w-3" />
              </button>
            </th>
            <th class="px-3 py-2 font-medium">
              <button class="flex items-center gap-1 hover:text-foreground" @click="toggleSort('feeAmount')">
                Fee
                <ArrowUp v-if="sortIcon('feeAmount') === 'asc'" class="h-3 w-3" />
                <ArrowDown v-else-if="sortIcon('feeAmount') === 'desc'" class="h-3 w-3" />
              </button>
            </th>
            <th class="px-3 py-2 font-medium">
              <button class="flex items-center gap-1 hover:text-foreground" @click="toggleSort('isPaid')">
                Paid
                <ArrowUp v-if="sortIcon('isPaid') === 'asc'" class="h-3 w-3" />
                <ArrowDown v-else-if="sortIcon('isPaid') === 'desc'" class="h-3 w-3" />
              </button>
            </th>
            <th class="px-3 py-2 font-medium">
              <button class="flex items-center gap-1 hover:text-foreground" @click="toggleSort('orderStatus')">
                Order Status
                <ArrowUp v-if="sortIcon('orderStatus') === 'asc'" class="h-3 w-3" />
                <ArrowDown v-else-if="sortIcon('orderStatus') === 'desc'" class="h-3 w-3" />
              </button>
            </th>
            <th class="px-3 py-2 font-medium">Alteration Notes</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="rec in visibleRecords"
            :key="rec.id"
            class="border-b border-border last:border-0 hover:bg-accent/40"
          >
            <td class="px-3 py-1.5 font-medium">{{ studentName(rec.studentId) }}</td>
            <td class="px-3 py-1.5">
              <input
                v-model="rec.costumeSize"
                type="text"
                placeholder="—"
                class="w-20 rounded border border-transparent bg-transparent px-1 py-0.5 hover:border-border focus:border-border focus:outline-none"
                @change="saveRecord(rec)"
              />
            </td>
            <td class="px-3 py-1.5">
              <input
                :value="rec.feeAmount"
                type="number"
                step="0.01"
                min="0"
                class="w-24 rounded border border-transparent bg-transparent px-1 py-0.5 tabular-nums hover:border-border focus:border-border focus:outline-none"
                @change="onFeeInput(rec, ($event.target as HTMLInputElement).value)"
              />
            </td>
            <td class="px-3 py-1.5">
              <button
                :class="[
                  'rounded px-2 py-0.5 text-xs font-medium',
                  rec.isPaid
                    ? 'bg-emerald-500/15 text-emerald-700 dark:text-emerald-400'
                    : 'bg-muted text-muted-foreground',
                ]"
                @click="togglePaid(rec)"
              >
                {{ rec.isPaid ? 'Paid' : 'Unpaid' }}
              </button>
            </td>
            <td class="px-3 py-1.5">
              <select
                v-model="rec.orderStatus"
                class="rounded border border-transparent bg-transparent px-1 py-0.5 hover:border-border focus:border-border focus:outline-none"
                @change="saveRecord(rec)"
              >
                <option v-for="s in orderStatuses" :key="s" :value="s">{{ s }}</option>
              </select>
            </td>
            <td class="px-3 py-1.5">
              <div class="flex items-center gap-1">
                <input
                  v-model="rec.alterationNotes"
                  type="text"
                  placeholder="—"
                  class="w-full min-w-[8rem] rounded border border-transparent bg-transparent px-1 py-0.5 hover:border-border focus:border-border focus:outline-none"
                  @change="saveRecord(rec)"
                />
                <button
                  class="shrink-0 rounded p-1 text-muted-foreground hover:bg-destructive/10 hover:text-destructive"
                  aria-label="Delete costume record"
                  @click="deleteRecord(rec)"
                >
                  <Trash2 class="h-3.5 w-3.5" />
                </button>
              </div>
            </td>
          </tr>
        </tbody>
        <tfoot>
          <tr class="border-t border-border bg-muted/40 text-xs text-muted-foreground">
            <td class="px-3 py-2 font-medium">{{ visibleRecords.length }} students</td>
            <td class="px-3 py-2"></td>
            <td class="px-3 py-2 font-medium tabular-nums">{{ money(totalFee) }}</td>
            <td class="px-3 py-2 font-medium" colspan="3">{{ unpaidCount }} unpaid</td>
          </tr>
        </tfoot>
      </table>
    </div>
  </section>
</template>
