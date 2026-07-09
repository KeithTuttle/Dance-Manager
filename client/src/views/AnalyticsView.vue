<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { api } from '@/lib/api'
import { useStudioStore } from '@/stores/studio'
import type { AnalyticsResponse, MonthlyAttendancePoint } from '@/types/analytics'

const studioStore = useStudioStore()

const data = ref<AnalyticsResponse | null>(null)
const loading = ref(false)

async function load(studioId: number) {
  loading.value = true
  data.value = null
  try {
    const { data: res } = await api.get<AnalyticsResponse>('/analytics', {
      params: { studioId },
    })
    data.value = res
  } catch {
    // Postgres may not be configured (endpoint 500s) or no studio selected.
    // Fall back to an empty state rather than crashing.
    data.value = null
  } finally {
    loading.value = false
  }
}

watch(
  () => studioStore.selectedStudioId,
  (id) => {
    if (id != null) load(id)
    else data.value = null
  },
  { immediate: true },
)

// ---- Metric card formatting ---------------------------------------------
const currency = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  maximumFractionDigits: 2,
})

const estimatedPay = computed(() =>
  data.value ? currency.format(data.value.estimatedPay) : '—',
)
const avgAttendance = computed(() =>
  data.value ? `${data.value.avgAttendanceRate.toFixed(1)}%` : '—',
)
const totalSessions = computed(() => (data.value ? String(data.value.totalSessions) : '—'))

const payBasis = computed(() => {
  if (!data.value) return 'Based on studio pay rules'
  if (data.value.payType === 'Hourly') {
    return `Hourly · ${currency.format(data.value.payRate)}/hr · ${data.value.hoursPerSession}h per session`
  }
  return `Per headcount · ${currency.format(data.value.payRate)} per attendee`
})

// ---- Hand-rolled SVG line chart ------------------------------------------
// A fixed viewBox keeps the geometry simple; the <svg> scales responsively to
// its container width via width="100%" + preserveAspectRatio.
const VB_W = 720
const VB_H = 260
const PAD = { top: 16, right: 16, bottom: 32, left: 40 }
const plotW = VB_W - PAD.left - PAD.right
const plotH = VB_H - PAD.top - PAD.bottom

const trend = computed<MonthlyAttendancePoint[]>(() => data.value?.trend ?? [])
const hasTrend = computed(() => trend.value.some((p) => p.total > 0))

// Y axis is attendance rate 0–100%. Fixed gridlines for a clean look.
const yTicks = [0, 25, 50, 75, 100]

function xFor(index: number): number {
  const n = trend.value.length
  if (n <= 1) return PAD.left + plotW / 2
  return PAD.left + (index / (n - 1)) * plotW
}

function yFor(rate: number): number {
  return PAD.top + plotH - (Math.min(Math.max(rate, 0), 100) / 100) * plotH
}

const points = computed(() =>
  trend.value.map((p, i) => ({
    ...p,
    x: xFor(i),
    y: yFor(p.attendanceRate),
  })),
)

const linePath = computed(() => {
  if (points.value.length === 0) return ''
  return points.value
    .map((p, i) => `${i === 0 ? 'M' : 'L'}${p.x.toFixed(1)},${p.y.toFixed(1)}`)
    .join(' ')
})

// Show at most ~6 x-axis labels to avoid crowding.
const labelEvery = computed(() => Math.max(1, Math.ceil(trend.value.length / 6)))

// Hover tooltip state.
const hoverIndex = ref<number | null>(null)
const hoverPoint = computed(() =>
  hoverIndex.value != null ? points.value[hoverIndex.value] : null,
)
</script>

<template>
  <div class="mx-auto max-w-6xl px-4 py-6 sm:px-6 lg:px-8 lg:py-8">
    <header class="mb-6 border-b border-border pb-4">
      <div class="flex items-center justify-between">
        <h1 class="text-xl font-semibold tracking-tight">Analytics</h1>
        <span class="text-sm text-muted-foreground">
          {{ studioStore.selectedStudio?.name ?? 'No studio selected' }}
        </span>
      </div>
      <p class="mt-1 text-sm text-muted-foreground">
        Attendance trends and estimated pay for the selected studio.
      </p>
    </header>

    <!-- No studio selected -->
    <section
      v-if="studioStore.selectedStudioId == null"
      class="rounded-lg border border-dashed border-border p-8 text-sm text-muted-foreground"
    >
      Select a studio from the sidebar to view its analytics.
    </section>

    <template v-else>
      <!-- Metric cards -->
      <div class="mb-6 grid grid-cols-1 gap-4 sm:grid-cols-3">
        <div class="rounded-lg border border-border p-4">
          <p class="text-xs font-medium uppercase tracking-wide text-muted-foreground">
            Total Estimated Pay
          </p>
          <p class="mt-2 text-2xl font-semibold tracking-tight tabular-nums">
            {{ estimatedPay }}
          </p>
          <p class="mt-1 text-xs text-muted-foreground">{{ payBasis }}</p>
        </div>

        <div class="rounded-lg border border-border p-4">
          <p class="text-xs font-medium uppercase tracking-wide text-muted-foreground">
            Avg Monthly Attendance
          </p>
          <p class="mt-2 text-2xl font-semibold tracking-tight tabular-nums">
            {{ avgAttendance }}
          </p>
          <p class="mt-1 text-xs text-muted-foreground">Across months with recorded attendance</p>
        </div>

        <div class="rounded-lg border border-border p-4">
          <p class="text-xs font-medium uppercase tracking-wide text-muted-foreground">
            Total Sessions
          </p>
          <p class="mt-2 text-2xl font-semibold tracking-tight tabular-nums">
            {{ totalSessions }}
          </p>
          <p class="mt-1 text-xs text-muted-foreground">Scheduled sessions for this studio</p>
        </div>
      </div>

      <!-- Attendance trend chart -->
      <section class="rounded-lg border border-border p-4">
        <div class="mb-3 flex items-center justify-between">
          <h2 class="text-sm font-semibold tracking-tight">Monthly Attendance Trend</h2>
          <span class="text-xs text-muted-foreground">Attendance rate (%)</span>
        </div>

        <div class="relative">
          <svg
            :viewBox="`0 0 ${VB_W} ${VB_H}`"
            width="100%"
            preserveAspectRatio="xMidYMid meet"
            class="block h-auto w-full select-none"
            role="img"
            aria-label="Monthly attendance rate trend"
            @mouseleave="hoverIndex = null"
          >
            <!-- Y gridlines + labels -->
            <g>
              <template v-for="t in yTicks" :key="`y-${t}`">
                <line
                  :x1="PAD.left"
                  :x2="VB_W - PAD.right"
                  :y1="yFor(t)"
                  :y2="yFor(t)"
                  stroke="hsl(var(--border))"
                  stroke-width="1"
                />
                <text
                  :x="PAD.left - 8"
                  :y="yFor(t) + 3"
                  text-anchor="end"
                  class="fill-muted-foreground"
                  font-size="10"
                >
                  {{ t }}
                </text>
              </template>
            </g>

            <!-- X axis baseline -->
            <line
              :x1="PAD.left"
              :x2="VB_W - PAD.right"
              :y1="yFor(0)"
              :y2="yFor(0)"
              stroke="hsl(var(--border))"
              stroke-width="1"
            />

            <!-- X axis labels -->
            <g>
              <template v-for="(p, i) in points" :key="`x-${i}`">
                <text
                  v-if="i % labelEvery === 0 || i === points.length - 1"
                  :x="p.x"
                  :y="VB_H - PAD.bottom + 18"
                  text-anchor="middle"
                  class="fill-muted-foreground"
                  font-size="10"
                >
                  {{ p.month }}
                </text>
              </template>
            </g>

            <!-- Data line + markers (only when there is real data) -->
            <template v-if="hasTrend">
              <path
                :d="linePath"
                fill="none"
                stroke="hsl(var(--primary))"
                stroke-width="2"
                stroke-linejoin="round"
                stroke-linecap="round"
              />
              <circle
                v-for="(p, i) in points"
                :key="`pt-${i}`"
                :cx="p.x"
                :cy="p.y"
                :r="hoverIndex === i ? 4.5 : 3"
                fill="hsl(var(--background))"
                stroke="hsl(var(--primary))"
                stroke-width="2"
              />
              <!-- Hover hit-targets: transparent vertical bands over each point -->
              <rect
                v-for="(p, i) in points"
                :key="`hit-${i}`"
                :x="p.x - (plotW / Math.max(points.length, 1)) / 2"
                :y="PAD.top"
                :width="plotW / Math.max(points.length, 1)"
                :height="plotH"
                fill="transparent"
                @mouseenter="hoverIndex = i"
              />
            </template>
          </svg>

          <!-- Empty state overlay -->
          <div
            v-if="!hasTrend && !loading"
            class="pointer-events-none absolute inset-0 flex items-center justify-center"
          >
            <p class="text-sm text-muted-foreground">No attendance data yet for this studio.</p>
          </div>

          <!-- Hover tooltip -->
          <div
            v-if="hoverPoint"
            class="pointer-events-none absolute z-10 -translate-x-1/2 -translate-y-full rounded-md border border-border bg-popover px-2 py-1 text-xs shadow-md"
            :style="{
              left: `${(hoverPoint.x / VB_W) * 100}%`,
              top: `${(hoverPoint.y / VB_H) * 100}%`,
            }"
          >
            <p class="font-medium">{{ hoverPoint.month }}</p>
            <p class="text-muted-foreground">
              {{ hoverPoint.attendanceRate.toFixed(1) }}% · {{ hoverPoint.present }}/{{
                hoverPoint.total
              }}
              present
            </p>
          </div>
        </div>
      </section>
    </template>
  </div>
</template>
