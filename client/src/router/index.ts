import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  {
    path: '/',
    name: 'dashboard',
    component: () => import('@/views/DashboardView.vue'),
    meta: { title: 'Dashboard' },
  },
  {
    path: '/roster',
    name: 'roster',
    component: () => import('@/views/RosterView.vue'),
    meta: { title: 'Roster & Progression' },
  },
  {
    path: '/attendance',
    name: 'attendance',
    component: () => import('@/views/AttendanceView.vue'),
    meta: { title: 'Attendance' },
  },
  {
    path: '/analytics',
    name: 'analytics',
    component: () => import('@/views/AnalyticsView.vue'),
    meta: { title: 'Analytics' },
  },
  {
    path: '/lesson-plans',
    name: 'lesson-plans',
    component: () => import('@/views/LessonPlansView.vue'),
    meta: { title: 'Lesson Plans' },
  },
  {
    path: '/choreography',
    name: 'choreography',
    component: () => import('@/views/ChoreographyView.vue'),
    meta: { title: 'Choreography' },
  },
  {
    path: '/recital',
    name: 'recital',
    component: () => import('@/views/RecitalView.vue'),
    meta: { title: 'Recital' },
  },
  {
    path: '/musical-planner',
    name: 'musical-planner',
    component: () => import('@/views/MusicalPlannerView.vue'),
    meta: { title: 'Musical Planner' },
  },
  {
    path: '/auditions',
    name: 'auditions',
    component: () => import('@/views/AuditionsView.vue'),
    meta: { title: 'Mock Auditions' },
  },
  {
    path: '/settings/studios',
    name: 'settings-studios',
    component: () => import('@/views/settings/StudiosView.vue'),
    meta: { title: 'Studios & Classes' },
  },
  {
    path: '/settings/account',
    name: 'settings-account',
    component: () => import('@/views/settings/AccountView.vue'),
    meta: { title: 'Account' },
  },
  {
    path: '/:pathMatch(.*)*',
    name: 'not-found',
    component: () => import('@/views/NotFoundView.vue'),
    meta: { title: 'Not Found' },
  },
]

export const router = createRouter({
  history: createWebHistory(),
  routes,
})

// Keep the browser tab title in sync with the active route.
const BASE_TITLE = 'DanceManager'
router.afterEach((to) => {
  const pageTitle = to.meta.title as string | undefined
  document.title = pageTitle ? `${pageTitle} · ${BASE_TITLE}` : BASE_TITLE
})
