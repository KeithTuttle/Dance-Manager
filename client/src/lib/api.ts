import axios from 'axios'
import { toast } from '@/lib/toast'

/**
 * Shared axios instance. Requests go to `/api/*` and are proxied to the
 * .NET backend by Vite in dev (see vite.config.ts).
 *
 * When Clerk is configured, a request interceptor attaches the current session
 * token as a Bearer credential so the API can authenticate the caller and scope
 * data to their tenant. On a 401 we bounce to Clerk's sign-in.
 */
export const api = axios.create({
  baseURL: '/api',
  headers: { 'Content-Type': 'application/json' },
})

// Clerk loads itself onto window.Clerk once the plugin initializes.
function clerk(): { session?: { getToken: () => Promise<string | null> }; redirectToSignIn?: () => void } | undefined {
  return (window as unknown as { Clerk?: never }).Clerk
}

api.interceptors.request.use(async (config) => {
  try {
    const c = clerk()
    if (c?.session) {
      const token = await c.session.getToken()
      if (token) config.headers.Authorization = `Bearer ${token}`
    }
  } catch {
    // No session / Clerk not ready — send the request unauthenticated.
  }
  return config
})

api.interceptors.response.use(
  (res) => res,
  (error) => {
    const status = error?.response?.status
    if (status === 401) {
      clerk()?.redirectToSignIn?.()
      return Promise.reject(error)
    }
    // Surface failed *writes* so a silently-dropped save is visible. Reads that
    // fail keep the existing "graceful empty state" behavior (no error spam).
    const method = (error?.config?.method ?? 'get').toLowerCase()
    if (method !== 'get') {
      toast.error('Couldn’t save your changes. Please check your connection and try again.')
    }
    return Promise.reject(error)
  },
)
