import { NavLink, Outlet } from 'react-router-dom'
import { ShoppingBag } from 'lucide-react'
import { useForme } from '../context/FormeContext'

const links = [
  { to: '/event', label: 'Event' },
  { to: '/browse', label: 'Browse' },
  { to: '/try-on', label: 'Try-on' },
  { to: '/compare', label: 'Compare' },
  { to: '/assistant', label: 'Assistant' },
]

export function Layout() {
  const { cartCount } = useForme()
  return (
    <div className="app-shell">
      <header className="topbar">
        <NavLink className="wordmark" to="/" aria-label="Suitability home">
          <img
            className="wordmark-logo"
            src="/logo.png"
            alt="Suitability"
            width={1024}
            height={1024}
            decoding="async"
          />
        </NavLink>
        <nav className="main-nav" aria-label="Main navigation">
          {links.map((link) => (
            <NavLink
              key={link.to}
              to={link.to}
              className={({ isActive }) => (isActive ? 'is-active' : undefined)}
            >
              {link.label}
            </NavLink>
          ))}
        </nav>
        <NavLink className="bag-button" to="/bag" aria-label={`Bag with ${cartCount} items`}>
          <ShoppingBag size={17} strokeWidth={1.8} />
          <span>Bag</span>
          <span className="bag-count">{cartCount}</span>
        </NavLink>
      </header>
      <main>
        <Outlet />
      </main>
      <footer className="footer-bar">
        <span>Suitability · camera try-on · voice search · agent swarm</span>
        <span>Find for you. Style for you. Scenic match for you.</span>
      </footer>
    </div>
  )
}
