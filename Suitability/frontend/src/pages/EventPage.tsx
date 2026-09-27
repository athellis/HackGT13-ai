import { ArrowRight, Check } from 'lucide-react'
import { Link, useNavigate } from 'react-router-dom'
import { VoiceMicButton } from '../components/VoiceMicButton'
import { audienceOptions, fitOptions, styleOptions } from '../formeConstants'
import { useForme } from '../context/FormeContext'

export function EventPage() {
  const navigate = useNavigate()
  const {
    eventDescription, setEventDescription, body, setBody, fitPreference, setFitPreference,
    stylePreferences, toggleStyle, audience, setAudience, maxPrice, setMaxPrice,
    useReference, setUseReference, reference, setReference, eventContext, isLoading, error, checkoutMessage, findMyEdit,
  } = useForme()

  async function handleFind() {
    await findMyEdit()
    navigate('/browse')
  }

  return (
    <section className="section page-section">
      <div className="section-heading">
        <div><p className="eyebrow">01 · Event & profile</p><h2>What are you dressing for?</h2></div>
        <span className="step-label">Profile</span>
      </div>
      <div className="edit-grid">
        <div>
          <div className="field-with-mic">
            <label className="field-label" htmlFor="event-description">Your event</label>
            <VoiceMicButton label="Speak event" onTranscript={(text) => setEventDescription((eventDescription && eventDescription !== text ? `${eventDescription} ${text}` : text).slice(0, 500))} />
          </div>
          <textarea id="event-description" value={eventDescription} maxLength={500} onChange={(e) => setEventDescription(e.target.value)} rows={3} />
          <div className="event-meta">
            <span>{eventContext.scene_label || 'Setting ready'}{eventContext.temp_f ? ` · ${eventContext.temp_f}°F` : ''}</span>
            <span>{eventDescription.length}/500</span>
          </div>
        </div>
        <div>
          <label className="field-label">Measurements <span>(in)</span></label>
          <div className="measurement-row">
            {(['chest', 'waist', 'shoulders', 'sleeves'] as const).map((m) => (
              <label className="measurement-field" key={m}>
                <span>{m}</span>
                <input type="number" min="1" max="100" step="0.5" value={body[m]} onChange={(e) => setBody((c) => ({ ...c, [m]: Number(e.target.value) }))} />
              </label>
            ))}
          </div>
        </div>
        <div>
          <label className="field-label">Audience</label>
          <div className="audience-row" role="group">
            {audienceOptions.map((o) => (
              <button key={o.value} className={audience === o.value ? 'is-active' : ''} type="button" onClick={() => setAudience(o.value)} aria-pressed={audience === o.value}>{o.label}</button>
            ))}
          </div>
        </div>
        <div>
          <label className="field-label">Preferred fit</label>
          <div className="segmented-control" role="group">
            {fitOptions.map((o) => (
              <button key={o} className={fitPreference === o ? 'is-active' : ''} type="button" onClick={() => setFitPreference(o)} aria-pressed={fitPreference === o}>{o}</button>
            ))}
          </div>
        </div>
        <div>
          <label className="field-label">Style</label>
          <div className="style-options">
            {styleOptions.map((style) => (
              <button key={style} className={stylePreferences.includes(style) ? 'style-chip is-active' : 'style-chip'} type="button" onClick={() => toggleStyle(style)} aria-pressed={stylePreferences.includes(style)}>
                {stylePreferences.includes(style) && <Check size={12} />}{style}
              </button>
            ))}
          </div>
        </div>
        <div>
          <label className="field-label" htmlFor="budget">Budget ceiling <span>(optional)</span></label>
          <div className="budget-field">
            <input id="budget" type="number" min="20" max="500" placeholder="e.g. 150" value={maxPrice} onChange={(e) => setMaxPrice(e.target.value ? Number(e.target.value) : '')} />
          </div>
        </div>
        <div style={{ gridColumn: '1 / -1' }}>
          <label className="field-label">
            <input type="checkbox" checked={useReference} onChange={(e) => setUseReference(e.target.checked)} style={{ marginRight: 8 }} />
            Cross-brand reference garment
          </label>
          {useReference && (
            <div className="ref-grid" style={{ marginTop: 10 }}>
              <label>Brand<input value={reference.brand} onChange={(e) => setReference((c) => ({ ...c, brand: e.target.value }))} /></label>
              <label>Name<input value={reference.name} onChange={(e) => setReference((c) => ({ ...c, name: e.target.value }))} /></label>
              <label>Size<input value={reference.label} onChange={(e) => setReference((c) => ({ ...c, label: e.target.value }))} /></label>
            </div>
          )}
        </div>
      </div>
      <div className="edit-actions">
        {error && <p className="inline-error" role="status">{error}</p>}
        {checkoutMessage && <p className="inline-success" role="status"><Check size={15} /> {checkoutMessage}</p>}
        <Link className="ghost-button" to="/browse">Skip to browse</Link>
        <button className="primary-button" type="button" onClick={() => void handleFind()} disabled={isLoading || !eventDescription.trim()}>
          {isLoading ? 'Finding your edit…' : 'Find my edit'} <ArrowRight size={17} />
        </button>
      </div>
    </section>
  )
}
