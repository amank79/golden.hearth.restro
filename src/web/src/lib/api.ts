// Small typed wrapper around fetch for the local API. Errors carry a message the screen can show as-is.

import { askOwnerPin, clearOwnerSession, EXPIRES_HEADER, extendOwnerSession, ownerToken, PIN_REQUIRED_CODE, TOKEN_HEADER } from './owner'

export class ApiError extends Error {
  readonly status: number
  readonly fields: Record<string, string[]>

  constructor(status: number, message: string, fields: Record<string, string[]> = {}) {
    super(message)
    this.status = status
    this.fields = fields
  }
}

async function request<T>(method: string, url: string, body?: unknown, retried = false): Promise<T> {
  let res: Response
  const headers: Record<string, string> = {}
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  const token = ownerToken()
  if (token) headers[TOKEN_HEADER] = token
  try {
    res = await fetch(`/api${url}`, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  } catch {
    throw new ApiError(0, 'The billing program is not running. Please restart it.')
  }
  const expires = res.headers.get(EXPIRES_HEADER)
  if (expires) extendOwnerSession(expires)
  if (res.status === 204) return undefined as T
  const isJson = res.headers.get('content-type')?.includes('json')
  const data = isJson ? await res.json() : await res.text()
  // Menu changes, discounts, cancelling and settings need the owner PIN: ask for it, then try once more.
  if (res.status === 403 && isJson && data.code === PIN_REQUIRED_CODE) {
    clearOwnerSession()
    if (!retried && (await askOwnerPin())) return request<T>(method, url, body, true)
    throw new ApiError(403, 'Not changed: the owner PIN is needed for this.')
  }
  if (!res.ok) {
    const fields: Record<string, string[]> = (isJson && data.errors) || {}
    const firstField = Object.values(fields)[0]?.[0]
    const message = firstField ?? (isJson ? data.detail ?? data.title : null) ?? `Something went wrong (${res.status}).`
    throw new ApiError(res.status, message, fields)
  }
  return data as T
}

export const api = {
  get: <T>(url: string) => request<T>('GET', url),
  post: <T>(url: string, body?: unknown) => request<T>('POST', url, body ?? {}),
  put: <T>(url: string, body: unknown) => request<T>('PUT', url, body),
  del: <T>(url: string) => request<T>('DELETE', url),
}

export function errorText(e: unknown): string {
  return e instanceof Error ? e.message : String(e)
}
