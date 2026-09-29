import { createPortal } from 'react-dom'
import { Modal } from './Modal'
import type { PrintOutput } from '../lib/types'

interface Props {
  output: PrintOutput
  /** True after a real print (bill numbered, print counted); false for a preview only. */
  printed: boolean
  onClose: () => void
}

/**
 * The 80 mm bill exactly as the printer gets it (48 characters per line). Until the thermal printer is connected
 * on the laptop, "Print with browser" prints this text through the normal Windows print dialog.
 */
export function PrintPreview({ output, printed, onClose }: Props) {
  const title = !printed ? 'Print preview' : output.duplicate ? 'Reprint (DUPLICATE)' : `Bill ${output.bill.billNo} printed`
  return (
    <Modal
      title={title}
      subtitle={
        output.isDraft
          ? 'Draft: the bill gets its number when it is printed.'
          : 'The bill printer is not connected yet. Use “Print with browser” to print on paper.'
      }
      onClose={onClose}
    >
      <pre className="receipt" aria-label="Bill print preview">{output.text}</pre>
      {/* Printed copy outside the app, so the browser prints only the bill (see @media print). */}
      {createPortal(<pre className="print-only">{output.text}</pre>, document.body)}
      <div className="modal-actions">
        {!output.isDraft && (
          <button className="btn dark" onClick={() => window.print()}>Print with browser</button>
        )}
        <button className="btn primary" onClick={onClose} autoFocus>Close <kbd>Esc</kbd></button>
      </div>
    </Modal>
  )
}
