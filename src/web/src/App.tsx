import { useEffect, useState } from 'react'

// Placeholder shell. Screens (menu management, billing, bill history, settings) come next — see docs/PRD.md §4.
export default function App() {
  const [api, setApi] = useState('checking…')

  useEffect(() => {
    fetch('/api/health')
      .then((r) => (r.ok ? 'connected' : `error ${r.status}`))
      .catch(() => 'not reachable')
      .then(setApi)
  }, [])

  return (
    <main className="shell">
      <h1>Restaurant POS</h1>
      <p>Server: <strong>{api}</strong></p>
    </main>
  )
}
