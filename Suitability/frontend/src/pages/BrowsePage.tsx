import { ChevronDown } from 'lucide-react'
import { Link } from 'react-router-dom'
import { ProductCard } from '../components/ProductCard'
import { VoiceMicButton } from '../components/VoiceMicButton'
import { useForme } from '../context/FormeContext'

export function BrowsePage() {
  const {
    displayedRecommendations, selected, setSelectedProductId, setSelectedSize, setCheckoutMessage,
    compareIds, toggleCompare, sortBy, setSortBy, highlightIds, error, recommendations,
    swarmQuery, setSwarmQuery, runSwarm, isSwarming, agentNotes,
  } = useForme()

  return (
    <section className="section page-section" aria-labelledby="recommendation-title">
      <div className="section-heading">
        <div>
          <p className="eyebrow">02 · Recommendations</p>
          <h2 id="recommendation-title">Ranked for body, style, and setting</h2>
        </div>
      </div>
      <div className="swarm-bar">
        <div className="field-with-mic">
          <label className="field-label" htmlFor="swarm-query">
            Describe what you want — agent swarm searches in parallel
          </label>
          <VoiceMicButton
            label="Speak search"
            onTranscript={(text) => {
              setSwarmQuery(text)
              void runSwarm(text)
            }}
          />
        </div>
        <form className="swarm-form" onSubmit={(e) => { e.preventDefault(); void runSwarm() }}>
          <input
            id="swarm-query"
            value={swarmQuery}
            onChange={(e) => setSwarmQuery(e.target.value)}
            placeholder="e.g. jacket for cold weather"
            aria-label="Clothing search for agent swarm"
          />
          <button className="primary-button" type="submit" disabled={isSwarming || !swarmQuery.trim()}>
            {isSwarming ? 'Agents searching…' : 'Run swarm'}
          </button>
        </form>
        {!!agentNotes.length && (
          <ul className="agent-notes" aria-live="polite">
            {agentNotes.map((note) => <li key={note}>{note}</li>)}
          </ul>
        )}
      </div>
      <div className="toolbar-row">
        <button className="sort-button" type="button" onClick={() => setSortBy((c) => (c === 'match' ? 'price' : 'match'))}>
          {sortBy === 'match' ? 'Best match' : 'Price low to high'} <ChevronDown size={14} />
        </button>
        <Link className="compare-toggle" to="/compare">Compare selected ({compareIds.length}/3)</Link>
      </div>
      <div className="product-grid">
        {displayedRecommendations.map((item) => (
          <ProductCard
            key={item.product.id}
            item={item}
            selected={selected?.product.id === item.product.id}
            compared={compareIds.includes(item.product.id)}
            highlighted={highlightIds.includes(item.product.id)}
            onSelect={() => {
              setSelectedProductId(item.product.id)
              setSelectedSize(item.recommended_size)
              setCheckoutMessage('')
            }}
            onCompare={() => toggleCompare(item.product.id)}
          />
        ))}
        {!recommendations.length && !error && <p className="loading-note">Putting together your first edit…</p>}
        {!recommendations.length && error && <p className="loading-note">Start the API to see the seeded collection here.</p>}
      </div>
      {selected && (
        <div className="page-cta-row">
          <Link className="primary-button" to="/try-on">Try on {selected.product.name}</Link>
        </div>
      )}
    </section>
  )
}
