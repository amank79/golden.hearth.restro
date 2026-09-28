// Tiny global toast: call toast('Saved') from anywhere; <Toaster /> shows it.

type Listener = (message: string, kind: ToastKind) => void
export type ToastKind = 'info' | 'error'

const listeners = new Set<Listener>()

export function toast(message: string, kind: ToastKind = 'info') {
  listeners.forEach((l) => l(message, kind))
}

export function onToast(listener: Listener): () => void {
  listeners.add(listener)
  return () => {
    listeners.delete(listener)
  }
}
