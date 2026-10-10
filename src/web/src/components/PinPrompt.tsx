import { useEffect, useRef, useState, type FormEvent } from 'react'
import { api, errorText } from '../lib/api'
import { registerPinPrompt, setOwnerSession } from '../lib/owner'
import { Icon } from './Icon'
import { Modal } from './Modal'

const KEYS = ['1', '2', '3', '4', '5', '6', '7', '8', '9', 'back', '0', 'ok'] as const

/**
 * The owner PIN keypad. It opens by itself when the server says an action needs the PIN
 * (menu changes, discounts, cancelling bills, settings); the action continues once the PIN is right.
 */
export function PinPrompt() {
  const [open, setOpen] = useState(false)
  const [pin, setPin] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [checking, setChecking] = useState(false)
  const answer = useRef<((ok: boolean) => void) | null>(null)
  const input = useRef<HTMLInputElement>(null)

  useEffect(
    () =>
      registerPinPrompt(
        () =>
          new Promise<boolean>((resolve) => {
            answer.current = resolve
            setPin('')
            setError(null)
            setOpen(true)
          }),
      ),
    [],
  )

  const finish = (ok: boolean) => {
    setOpen(false)
    answer.current?.(ok)
    answer.current = null
  }

  const submit = async (e?: FormEvent) => {
    e?.preventDefault()
    if (pin.length < 4 || checking) return
    setChecking(true)
    try {
      const r = await api.post<{ token: string; expiresAt: string }>('/owner/unlock', { pin })
      setOwnerSession(r.token, r.expiresAt)
      finish(true)
    } catch (err) {
      setError(errorText(err))
      setPin('')
      input.current?.focus()
    } finally {
      setChecking(false)
    }
  }

  const press = (key: (typeof KEYS)[number]) => {
    setError(null)
    if (key === 'ok') void submit()
    else if (key === 'back') setPin((p) => p.slice(0, -1))
    else setPin((p) => (p.length < 6 ? p + key : p))
    input.current?.focus()
  }

  if (!open) return null
  return (
    <Modal title="Owner PIN" subtitle="Needed to change the menu, give a discount, cancel a bill or change settings." onClose={() => finish(false)}>
      <form onSubmit={submit} className="pin-form">
        <input
          ref={input}
          className="pin-input"
          type="password"
          inputMode="numeric"
          autoComplete="off"
          maxLength={6}
          value={pin}
          onChange={(e) => {
            setError(null)
            setPin(e.target.value.replace(/\D/g, '').slice(0, 6))
          }}
          aria-label="Owner PIN"
          autoFocus
        />
        <p className={error ? 'pin-error' : 'pin-hint'} role={error ? 'alert' : undefined}>
          {error ?? 'Type the 4 to 6 digit PIN, then press Enter.'}
        </p>
        <div className="keypad">
          {KEYS.map((k) => (
            <button
              key={k}
              type="button"
              className={k === 'ok' ? 'ok' : k === 'back' ? 'back' : ''}
              onClick={() => press(k)}
              disabled={k === 'ok' && (pin.length < 4 || checking)}
              aria-label={k === 'back' ? 'Delete last digit' : k === 'ok' ? 'Unlock' : k}
            >
              {k === 'back' ? <Icon name="close" /> : k === 'ok' ? <Icon name="check" /> : k}
            </button>
          ))}
        </div>
      </form>
    </Modal>
  )
}
