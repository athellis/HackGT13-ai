import type { Recommendation } from '../api'

type Props = {
  item: Recommendation
  selected?: boolean
  compared?: boolean
  highlighted?: boolean
  onSelect: () => void
  onCompare: () => void
}

export function ProductCard({ item, selected, compared, highlighted, onSelect, onCompare }: Props) {
  return (
    <article className={['product-card', selected ? 'is-selected' : '', compared ? 'is-compared' : '', highlighted ? 'is-selected' : ''].filter(Boolean).join(' ')}>
      <button className="product-select" type="button" onClick={onSelect} aria-pressed={!!selected}>
        <span className="product-photo garment-only">
          <img src={item.product.image} alt={`${item.product.name} garment preview`} loading="lazy" />
          <span className="product-fit-tag">{item.overall}% match</span>
        </span>
        <span className="product-info">
          <span className="product-brand">{item.product.brand}</span>
          <span className="product-name-price"><strong>{item.product.name}</strong><span>${item.product.price}</span></span>
          <span className="product-color">{item.product.color} · {item.product.material}</span>
          <span className="product-recommendation">Size <b>{item.recommended_size}</b></span>
        </span>
      </button>
      <button type="button" className="ghost-button compare-inline" onClick={onCompare}>
        {compared ? 'Compared' : 'Compare'}
      </button>
    </article>
  )
}
