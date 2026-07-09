import axios from 'axios'

/**
 * Shared axios instance. Requests go to `/api/*` and are proxied to the
 * .NET backend by Vite in dev (see vite.config.ts).
 */
export const api = axios.create({
  baseURL: '/api',
  headers: { 'Content-Type': 'application/json' },
})
