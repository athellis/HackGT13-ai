import { Link } from 'react-router-dom'
import { useForme } from '../context/FormeContext'

export function ComparePage() {
  const { compared, setSelectedProductId, setSelectedSize } = useForme()

  return (
    <section className="section page-section compare-section">
      <div className="section-heading">
        <div>
          <p className="eyebrow">04 · Fit comparison</p>
          <h2>Compare options side by side</h2>
        </div>
      </div>
      {compared.length < 2 ? (
        <p className="loading-note">
          Select at least two products with Compare on the <Link to="/browse">browse</Link> page.
        </p>
      ) : (
        <>
          <div className="compare-thumb-row">
            {compared.map((item) => (
              <button
                key={item.product.id}
                type="button"
                className="compare-thumb"
                onClick={() => {
                  setSelectedProductId(item.product.id)
                  setSelectedSize(item.recommended_size)
                }}
              >
                <img className="garment-only-img" src={item.product.image} alt={`${item.product.name} garment`} />
                <span>{item.product.name}</span>
              </button>
            ))}
          </div>
          <table className="compare-table">
            <thead>
              <tr>
                <th>Metric</th>
                {compared.map((item) => <th key={item.product.id}>{item.product.name}</th>)}
              </tr>
            </thead>
            <tbody>
              {(['Price', 'Body fit', 'Style', 'Occasion', 'Environment', 'Recommended size'] as const).map((metric) => (
                <tr key={metric}>
                  <td>{metric}</td>
                  {compared.map((item) => (
                    <td key={item.product.id}>
                      {metric === 'Price' && `$${item.product.price}`}
                      {metric === 'Body fit' && <strong>{item.body_fit}%</strong>}
                      {metric === 'Style' && `${item.style_match}%`}
                      {metric === 'Occasion' && `${item.event_match}%`}
                      {metric === 'Environment' && `${item.env_match}%`}
                      {metric === 'Recommended size' && item.recommended_size}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
          <div className="page-cta-row">
            <Link className="primary-button" to="/try-on">Open try-on</Link>
          </div>
        </>
      )}
    </section>
  )
}
