import { ArrowRight } from 'lucide-react'
import { Link } from 'react-router-dom'
import { useForme } from '../context/FormeContext'

export function HomePage() {
  const { sceneImage } = useForme()
  return (
    <section
      className="hero"
      style={{ backgroundImage: `url('${sceneImage}')` }}
      aria-label="Suitability immersive fit experience"
    >
      <div className="hero-inner">
        <div className="hero-panel">
          <h1 className="hero-brand">
            <img
              className="hero-logo"
              src="/logo.png"
              alt="Suitability"
              width={1024}
              height={1024}
              decoding="async"
              fetchPriority="high"
            />
          </h1>
          <p className="hero-tagline">Find for you. Style for you. Scenic match for you.</p>
          <p className="hero-copy">
            Describe the event, get body/style/setting scores, then check the look on your laptop or phone camera. No headset required.
          </p>
          <div className="hero-actions">
            <Link className="primary-button" to="/event">
              Build your edit <ArrowRight size={16} />
            </Link>
            <Link className="text-link" to="/try-on">
              Open camera try-on
            </Link>
          </div>
        </div>
      </div>
    </section>
  )
}
