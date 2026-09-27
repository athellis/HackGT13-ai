import { ShoppingBag } from 'lucide-react'
import { Link } from 'react-router-dom'
import { CameraMirror } from '../components/CameraMirror'
import { Score } from '../components/Score'
import { useForme } from '../context/FormeContext'

export function TryOnPage() {
  const {
    selected, selectedSize, setSelectedSize, sceneImage, eventContext, selectedSizeFit,
    addToBag, toggleCompare, compareIds, isCheckingOut, setCheckoutMessage,
  } = useForme()

  if (!selected) {
    return (
      <section className="section page-section">
        <p className="loading-note">No product selected yet. <Link to="/browse">Browse recommendations</Link> first.</p>
      </section>
    )
  }

  return (
    <section className="section page-section">
      <div className="section-heading">
        <div>
          <p className="eyebrow">03 · Camera fitting room</p>
          <h2>See it on you, in the setting</h2>
        </div>
        <span className="step-label">Live overlay · photo try-on</span>
      </div>
      <div className="tryon-layout">
        <CameraMirror
          product={selected.product}
          size={selectedSize}
          environmentImage={sceneImage}
          environmentLabel={eventContext.scene_label || 'Your event'}
        />
        <div className="fit-panel">
          <div className="fit-product-heading">
            <div>
              <p className="product-brand">{selected.product.brand}</p>
              <h3>{selected.product.name}</h3>
            </div>
            <span className="price-large">${selected.product.price}</span>
          </div>
          <div className="score-row">
            <Score label="Body" score={selectedSizeFit?.body_fit ?? selected.body_fit} />
            <Score label="Style" score={selected.style_match} />
            <Score label="Occasion" score={selected.event_match} />
            <Score label="Environment" score={selected.env_match} />
          </div>
          <div className="size-choice">
            <div className="size-choice-heading">
              <span>Choose a size</span>
              <span>Recommended: <b>{selected.recommended_size}</b></span>
            </div>
            <div className="size-options">
              {selected.product.sizes.map((size) => (
                <button
                  type="button"
                  key={size.label}
                  onClick={() => { setSelectedSize(size.label); setCheckoutMessage('') }}
                  className={selectedSize === size.label ? 'size-option is-active' : 'size-option'}
                  aria-pressed={selectedSize === size.label}
                >
                  {size.label}
                </button>
              ))}
            </div>
          </div>
          <div className="area-fit-list">
            {Object.entries(selectedSizeFit?.areas ?? selected.areas).map(([area, score]) => (
              <div className="area-fit-row" key={area}>
                <span>{area}</span>
                <span className="area-meter"><i style={{ width: `${score}%` }} /></span>
                <b>{score >= 82 ? 'Good' : score >= 58 ? 'Close' : 'Review'}</b>
              </div>
            ))}
          </div>
          <div className="fit-explanation">
            <span>WHY IT WORKS</span>
            {selected.reasons.map((reason) => <p key={reason}>{reason}</p>)}
          </div>
          <div className="fit-actions">
            <button className="primary-button" type="button" onClick={addToBag} disabled={isCheckingOut}>
              Add to bag <ShoppingBag size={16} />
            </button>
            <button className="ghost-button" type="button" onClick={() => toggleCompare(selected.product.id)}>
              {compareIds.includes(selected.product.id) ? 'In compare' : 'Add to compare'}
            </button>
            <Link className="ghost-button" to="/bag">View bag</Link>
          </div>
        </div>
      </div>
    </section>
  )
}
