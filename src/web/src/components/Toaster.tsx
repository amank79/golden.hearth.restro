import { useEffect, useState } from 'react'
import { onToast, type ToastKind } from './toast'

export function Toaster() {
  const [msg, setMsg] = useState<{ text: string; kind: ToastKind; id: number } | null>(null)

  useEffect(() => {
    let timer: number | undefined
    const off = onToast((text, kind) => {
      setMsg({ text, kind, id: Date.now() })
      window.clearTimeout(timer)
      timer = window.setTimeout(() => setMsg(null), kind === 'error' ? 5000 : 3000)
    })
    return () => {
      off()
      window.clearTimeout(timer)
    }
  }, [])

  return (
    <div className={`toast${msg ? ' show' : ''}${msg?.kind === 'error' ? ' error' : ''}`} role="status" aria-live="polite">
      {msg?.text}
    </div>
  )
}
