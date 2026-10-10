// Owner PIN session for this window. The server decides what needs the PIN; when it answers
// "owner PIN needed", api.ts asks for it through the PIN prompt and repeats the request.
// Kept in memory only: reloading the page locks again.

export const TOKEN_HEADER = 'X-Owner-Token'
export const EXPIRES_HEADER = 'X-Owner-Expires'
export const PIN_REQUIRED_CODE = 'owner_pin_required'

let token: string | null = null
let expiresAt = 0
const listeners = new Set<() => void>()

const changed = () => listeners.forEach((l) => l())

/** The token while it is still valid, otherwise null. */
export function ownerToken(): string | null {
  return token && Date.now() < expiresAt ? token : null
}

export function ownerExpiresAt(): number | null {
  return ownerToken() ? expiresAt : null
}

export function setOwnerSession(newToken: string, expires: string) {
  token = newToken
  expiresAt = Date.parse(expires)
  changed()
}

/** The server moves the expiry forward on every protected action. */
export function extendOwnerSession(expires: string) {
  if (!token) return
  expiresAt = Date.parse(expires)
  changed()
}

export function clearOwnerSession() {
  if (!token) return
  token = null
  expiresAt = 0
  changed()
}

export function onOwnerChange(listener: () => void): () => void {
  listeners.add(listener)
  return () => {
    listeners.delete(listener)
  }
}

// The PIN prompt (components/PinPrompt) registers itself; requests wait for its answer.
type Ask = () => Promise<boolean>
let ask: Ask | null = null
let pending: Promise<boolean> | null = null

export function registerPinPrompt(fn: Ask): () => void {
  ask = fn
  return () => {
    if (ask === fn) ask = null
  }
}

/** Shows the PIN prompt (once, even if several requests need it) and resolves true when unlocked. */
export function askOwnerPin(): Promise<boolean> {
  if (!ask) return Promise.resolve(false)
  pending ??= ask().finally(() => {
    pending = null
  })
  return pending
}
