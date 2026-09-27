import { Link } from 'react-router-dom'
import { VoiceMicButton } from '../components/VoiceMicButton'
import { quickPrompts } from '../formeConstants'
import { useForme } from '../context/FormeContext'

export function AssistantPage() {
  const { chat, chatInput, setChatInput, sendChat, isChatting, highlightIds, recommendations } = useForme()

  return (
    <section className="section page-section">
      <div className="section-heading">
        <div>
          <p className="eyebrow">05 · Shopping assistant</p>
          <h2>Ask in plain language</h2>
        </div>
      </div>
      <div className="assistant-layout">
        <div>
          <div className="chat-log" aria-live="polite">
            {chat.map((message, index) => (
              <div key={`${message.role}-${index}`} className={`chat-bubble is-${message.role}`}>
                {message.text}
              </div>
            ))}
          </div>
          <form className="chat-form" onSubmit={(e) => { e.preventDefault(); void sendChat(chatInput) }}>
            <input
              value={chatInput}
              onChange={(e) => setChatInput(e.target.value)}
              placeholder="e.g. Find something more casual under $100"
              aria-label="Assistant message"
            />
            <VoiceMicButton
              label="Speak"
              onTranscript={(text) => {
                setChatInput(text)
              }}
            />
            <button className="primary-button" type="submit" disabled={isChatting || !chatInput.trim()}>
              {isChatting ? 'Thinking…' : 'Ask'}
            </button>
          </form>
          <div className="quick-prompts">
            {quickPrompts.map((prompt) => (
              <button key={prompt} type="button" onClick={() => void sendChat(prompt)}>{prompt}</button>
            ))}
          </div>
        </div>
        <div className="assistant-side">
          {(highlightIds.length
            ? recommendations.filter((item) => highlightIds.includes(item.product.id))
            : recommendations.slice(0, 2)
          ).map((item) => (
            <article key={item.product.id}>
              <img className="assistant-garment" src={item.product.image} alt={`${item.product.name} garment`} />
              <p className="product-brand">{item.product.brand}</p>
              <h3>{item.product.name}</h3>
              <p>{item.overall}% overall · size {item.recommended_size} · ${item.product.price}</p>
              <Link to="/try-on">Try on</Link>
            </article>
          ))}
        </div>
      </div>
    </section>
  )
}
