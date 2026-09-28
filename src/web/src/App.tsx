import { useCallback, useEffect, useState } from 'react'
import { Icon, type IconName } from './components/Icon'
import { Toaster } from './components/Toaster'
import { api, errorText } from './lib/api'
import type { Settings } from './lib/types'
import { MenuScreen } from './screens/menu/MenuScreen'
import { SettingsScreen } from './screens/SettingsScreen'

type ScreenId = 'menu' | 'settings'

const SCREENS: { id: ScreenId; label: string; icon: IconName; sub: string }[] = [
  { id: 'menu', label: 'Menu', icon: 'menu', sub: 'Dishes, prices and what is available today' },
  { id: 'settings', label: 'Settings', icon: 'settings', sub: 'Restaurant details, GST and bill footer' },
]

function currentScreen(): ScreenId {
  const id = window.location.hash.slice(1).split('/')[0]
  return (SCREENS.find((s) => s.id === id)?.id ?? SCREENS[0].id) as ScreenId
}

function useClock() {
  const [now, setNow] = useState(() => new Date())
  useEffect(() => {
    const t = window.setInterval(() => setNow(new Date()), 15000)
    return () => window.clearInterval(t)
  }, [])
  return now.toLocaleTimeString('en-IN', { hour: '2-digit', minute: '2-digit' })
}

export default function App() {
  const [screen, setScreen] = useState<ScreenId>(currentScreen)
  const [settings, setSettings] = useState<Settings | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const clock = useClock()

  const loadSettings = useCallback(() => {
    api.get<Settings>('/settings').then(setSettings, (e) => setLoadError(errorText(e)))
  }, [])

  useEffect(() => {
    loadSettings()
    const onHash = () => setScreen(currentScreen())
    window.addEventListener('hashchange', onHash)
    return () => window.removeEventListener('hashchange', onHash)
  }, [loadSettings])

  const meta = SCREENS.find((s) => s.id === screen)!

  return (
    <div className="app">
      <aside className="sidebar">
        <div className="brand">
          <div className="brand-logo">{(settings?.name || 'R').charAt(0).toUpperCase()}</div>
          <div style={{ minWidth: 0 }}>
            <b>{settings?.name ?? 'Restaurant POS'}</b>
            <small>Billing &amp; Menu</small>
          </div>
        </div>
        {SCREENS.map((s) => (
          <button key={s.id} className={`nav-btn${s.id === screen ? ' active' : ''}`} onClick={() => (window.location.hash = s.id)}>
            <Icon name={s.icon} />
            {s.label}
          </button>
        ))}
      </aside>

      <main>
        <header className="topbar">
          <div>
            <h1>{meta.label}</h1>
            <p>{meta.sub}</p>
          </div>
          <div className="right">
            <span className="pill ok" title="Runs on this laptop">Works without internet</span>
            <span className="clock">{clock}</span>
          </div>
        </header>
        <div className="view">
          {loadError ? (
            <div className="error-box">
              {loadError} <button className="btn small" onClick={() => { setLoadError(null); loadSettings() }}>Try again</button>
            </div>
          ) : !settings ? (
            <div className="empty">Loading…</div>
          ) : (
            <>
              {screen === 'menu' && <MenuScreen settings={settings} />}
              {screen === 'settings' && <SettingsScreen settings={settings} onSaved={setSettings} />}
            </>
          )}
        </div>
      </main>
      <Toaster />
    </div>
  )
}
