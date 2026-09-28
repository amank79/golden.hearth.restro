import type { ReactNode } from 'react'

interface Props {
  label: string
  hint?: ReactNode
  error?: string[]
  children: ReactNode
}

/** A labelled form field with an optional hint and error text. */
export function Field({ label, hint, error, children }: Props) {
  return (
    <label className={`field${error?.length ? ' has-error' : ''}`}>
      <span>{label}</span>
      {children}
      {error?.length ? <span className="err">{error[0]}</span> : hint ? <small>{hint}</small> : null}
    </label>
  )
}
