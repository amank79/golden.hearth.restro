import { Modal } from './Modal'

const KEYS: [string, string][] = [
  ['F2  or  /', 'Search dishes by name or short code'],
  ['Enter', 'Add the highlighted dish to the bill'],
  ['3*pbm', 'Type a number and * first to add 3 at once'],
  ['↑  ↓', 'Move through the search results'],
  ['1  2  or  H  F', 'Choose the size (Half / Full)'],
  ['F3', 'New dine-in bill (pick the table)'],
  ['F4', 'New takeaway bill'],
  ['F8', 'Take payment in cash'],
  ['F9', 'Print the bill'],
  ['Esc', 'Close a window or clear the search'],
  ['F1', 'Show this list'],
]

/** The counter keyboard shortcuts, opened with F1 or the Keys button. */
export function ShortcutsHelp({ onClose }: { onClose: () => void }) {
  return (
    <Modal title="Keyboard shortcuts" subtitle="Everything also works by tapping; the keys are only faster." onClose={onClose}>
      <div className="keys">
        {KEYS.map(([k, what]) => (
          <div key={k} style={{ display: 'contents' }}>
            <kbd>{k}</kbd>
            <span>{what}</span>
          </div>
        ))}
      </div>
      <div className="modal-actions">
        <button className="btn primary" onClick={onClose} autoFocus>Got it</button>
      </div>
    </Modal>
  )
}
