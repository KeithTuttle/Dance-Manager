import { reactive } from 'vue'

export interface ConfirmOptions {
  title: string
  message?: string
  confirmText?: string
  cancelText?: string
  /** Style the confirm button as destructive (red). */
  destructive?: boolean
}

interface ConfirmState extends ConfirmOptions {
  open: boolean
  resolve?: (value: boolean) => void
}

const state = reactive<ConfirmState>({ open: false, title: '' })

/** Reactive confirm-dialog state (read by ConfirmDialog.vue). */
export function useConfirmState() {
  return state
}

/**
 * Promise-based confirmation. Usage:
 *   if (!(await confirm({ title: 'Delete?', destructive: true }))) return
 */
export function confirm(options: ConfirmOptions): Promise<boolean> {
  return new Promise((resolve) => {
    state.open = true
    state.title = options.title
    state.message = options.message
    state.confirmText = options.confirmText ?? 'Confirm'
    state.cancelText = options.cancelText ?? 'Cancel'
    state.destructive = options.destructive ?? false
    state.resolve = resolve
  })
}

export function resolveConfirm(result: boolean) {
  state.open = false
  state.resolve?.(result)
  state.resolve = undefined
}
