// Small typed wrapper around fetch for the local API. Errors carry a message the screen can show as-is.

export class ApiError extends Error {
  readonly status: number
  readonly fields: Record<string, string[]>

  constructor(status: number, message: string, fields: Record<string, string[]> = {}) {
    super(message)
    this.status = status
    this.fields = fields
  }
}

async function request<T>(method: string, url: string, body?: unknown): Promise<T> {
  let res: Response
  try {
    res = await fetch(`/api${url}`, {
      method,
      headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body),
    })
  } catch {
    throw new ApiError(0, 'The billing program is not running. Please restart it.')
  }
  if (res.status === 204) return undefined as T
  const isJson = res.headers.get('content-type')?.includes('json')
  const data = isJson ? await res.json() : await res.text()
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
