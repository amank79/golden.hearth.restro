import { useState } from 'react'
import { createPortal } from 'react-dom'
import { Modal } from './Modal'
import { toast } from './toast'
import { api, errorText } from '../lib/api'
import type { PrintOutput, PrintTextLine } from '../lib/types'

/** The bill line by line; large lines are drawn at twice the size, as the printer does. */
function ReceiptLines({ lines }: { lines: PrintTextLine[] }) {
  return lines.map((l, i) => (
    <div key={i} className={`rl${l.big ? ' big' : ''}${l.bold ? ' b' : ''}`}>{l.text}</div>
  ))
}

interface Props {
  output: PrintOutput
  /** True after a real print (bill numbered, print counted); false for a preview only. */
  printed: boolean
  onClose: () => void
}

/**
 * The 80 mm bill as the browser prints it (42 characters per line, large total). Shown for "View", when no bill printer is
 * chosen in Settings (then "Print with browser" prints this text), and when the bill printer could not print it.
 */
export function PrintPreview({ output, printed, onClose }: Props) {
  const [error, setError] = useState(output.printerError ?? null)
  const [sending, setSending] = useState(false)
  const title = !printed
    ? 'Print preview'
    : error
      ? `Bill ${output.bill.billNo} not printed`
      : output.duplicate ? 'Reprint (DUPLICATE)' : `Bill ${output.bill.billNo} printed`

  // Send the same copy to the bill printer again; it does not count as another print.
  const resend = async () => {
    setSending(true)
    try {
      const again = await api.post<PrintOutput>(`/bills/${output.bill.id}/print/resend`)
      if (again.printerError) {
        setError(again.printerError)
      } else {
        toast(`Bill ${again.bill.billNo} printed.`)
        onClose()
      }
    } catch (e) {
      setError(errorText(e))
    } finally {
      setSending(false)
    }
  }

  const subtitle = output.isDraft
    ? 'Draft: the bill gets its number when it is printed.'
    : !printed
      ? 'This is how the bill prints.'
      : error
        ? undefined
        : 'No bill printer is chosen in Settings. Use “Print with browser” to print on paper.'
  return (
    <Modal title={title} subtitle={subtitle} onClose={onClose}>
      {printed && error && (
        <p className="warn" role="alert" style={{ marginTop: 0 }}>
          <b>The bill printer did not print this bill.</b> {error} Check that the printer is on, has paper and its cable
          is in, then press “Try printer again”.
        </p>
      )}
      <div className="receipt" aria-label="Bill print preview"><ReceiptLines lines={output.lines} /></div>
      {/* Printed copy outside the app, so the browser prints only the bill (see @media print). */}
      {createPortal(<div className="print-only"><ReceiptLines lines={output.lines} /></div>, document.body)}
      <div className="modal-actions">
        {printed && error && (
          <button className="btn primary" onClick={() => void resend()} disabled={sending}>
            {sending ? 'Sending…' : 'Try printer again'}
          </button>
        )}
        {!output.isDraft && (
          <button className="btn dark" onClick={() => window.print()}>Print with browser</button>
        )}
        <button className={printed && error ? 'btn' : 'btn primary'} onClick={onClose} autoFocus={!(printed && error)}>Close <kbd>Esc</kbd></button>
      </div>
    </Modal>
  )
}
