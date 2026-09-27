import { ArrowRight, Check } from 'lucide-react'
import { Link } from 'react-router-dom'
import { useForme } from '../context/FormeContext'

export function BagPage() {
  const { cart, cartCount, recommendations, completeOrder, isCheckingOut, checkoutMessage, error } = useForme()

  const lines = cart.map((item) => {
    const product = recommendations.find((rec) => rec.product.id === item.product_id)?.product
    return {
      ...item,
      name: product?.name ?? item.product_id,
      brand: product?.brand ?? '',
      price: product?.price ?? 0,
      image: product?.image ?? '',
    }
  })
  const total = lines.reduce((sum, line) => sum + line.price * line.quantity, 0)

  return (
    <section className="section page-section">
      <div className="section-heading">
        <div>
          <p className="eyebrow">06 · Bag & checkout</p>
          <h2>Your bag</h2>
        </div>
        <span className="step-label">{cartCount} item{cartCount === 1 ? '' : 's'}</span>
      </div>
      {!lines.length ? (
        <p className="loading-note">
          Bag is empty. <Link to="/browse">Browse the edit</Link> or <Link to="/try-on">try something on</Link>.
        </p>
      ) : (
        <div className="bag-layout">
          <ul className="bag-list">
            {lines.map((line) => (
              <li key={`${line.product_id}-${line.size}`}>
                {line.image && <img className="bag-thumb garment-only-img" src={line.image} alt="" />}
                <div>
                  <p className="product-brand">{line.brand}</p>
                  <strong>{line.name}</strong>
                  <p>Size {line.size} · Qty {line.quantity}</p>
                </div>
                <span>${line.price * line.quantity}</span>
              </li>
            ))}
          </ul>
          <aside className="bag-summary">
            <p>Total <strong>${total}</strong></p>
            {error && <p className="inline-error" role="status">{error}</p>}
            {checkoutMessage && (
              <p className="inline-success" role="status"><Check size={15} /> {checkoutMessage}</p>
            )}
            <button
              className="primary-button"
              type="button"
              onClick={() => void completeOrder()}
              disabled={!cart.length || isCheckingOut}
            >
              {isCheckingOut ? 'Confirming…' : 'Checkout'} <ArrowRight size={16} />
            </button>
            <span className="step-label">Simulated Visa</span>
          </aside>
        </div>
      )}
    </section>
  )
}
