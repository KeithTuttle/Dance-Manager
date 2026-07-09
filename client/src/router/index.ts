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
    path: '/auditions',
    name: 'auditions',
    component: () => import('@/views/AuditionsView.vue'),
    meta: { title: 'Mock Auditions' },
  },
]

export const router = createRouter({
  history: createWebHistory(),
  routes,
})
