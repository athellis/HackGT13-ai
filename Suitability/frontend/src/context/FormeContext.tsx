import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react'
import {
  askAssistant,
  checkout,
  getRecommendations,
  swarmSearch,
  type Audience,
  type Recommendation,
  type ReferenceGarment,
} from '../api'
import {
  defaultReference,
  fitOptions,
  initialRequest,
  type ChatMessage,
} from '../formeConstants'

type FormeContextValue = {
  eventDescription: string
  setEventDescription: (value: string) => void
  body: typeof initialRequest.body
  setBody: React.Dispatch<React.SetStateAction<typeof initialRequest.body>>
  fitPreference: (typeof fitOptions)[number]
  setFitPreference: (value: (typeof fitOptions)[number]) => void
  stylePreferences: string[]
  toggleStyle: (style: string) => void
  audience: Audience
  setAudience: (value: Audience) => void
  maxPrice: number | ''
  setMaxPrice: (value: number | '') => void
  useReference: boolean
  setUseReference: (value: boolean) => void
  reference: ReferenceGarment
  setReference: React.Dispatch<React.SetStateAction<ReferenceGarment>>
  recommendations: Recommendation[]
  eventContext: Record<string, string>
  selectedProductId: string
  setSelectedProductId: (id: string) => void
  selectedSize: string
  setSelectedSize: (size: string) => void
  compareIds: string[]
  toggleCompare: (productId: string) => void
  sortBy: 'match' | 'price'
  setSortBy: React.Dispatch<React.SetStateAction<'match' | 'price'>>
  cart: { product_id: string; size: string; quantity: number }[]
  cartCount: number
  isLoading: boolean
  error: string
  setError: (value: string) => void
  checkoutMessage: string
  setCheckoutMessage: (value: string) => void
  isCheckingOut: boolean
  chat: ChatMessage[]
  chatInput: string
  setChatInput: (value: string) => void
  isChatting: boolean
  highlightIds: string[]
  agentNotes: string[]
  swarmQuery: string
  setSwarmQuery: (value: string) => void
  isSwarming: boolean
  selected: Recommendation | undefined
  selectedSizeFit: { body_fit: number; areas: Record<string, number> } | undefined
  displayedRecommendations: Recommendation[]
  compared: Recommendation[]
  sceneImage: string
  requestPayload: () => ReturnType<typeof buildPayload>
  findMyEdit: () => Promise<void>
  addToBag: () => void
  completeOrder: () => Promise<void>
  sendChat: (message: string) => Promise<void>
  runSwarm: (query?: string) => Promise<void>
}

function buildPayload(state: {
  eventDescription: string
  body: typeof initialRequest.body
  fitPreference: (typeof fitOptions)[number]
  stylePreferences: string[]
  audience: Audience
  maxPrice: number | ''
  useReference: boolean
  reference: ReferenceGarment
}) {
  return {
    event_description: state.eventDescription,
    body: state.body,
    fit_preference: state.fitPreference,
    style_preferences: state.stylePreferences,
    audience: state.audience,
    max_price: state.maxPrice === '' ? undefined : Number(state.maxPrice),
    reference: state.useReference ? state.reference : undefined,
  }
}

const FormeContext = createContext<FormeContextValue | null>(null)

export function FormeProvider({ children }: { children: ReactNode }) {
  const [eventDescription, setEventDescription] = useState(initialRequest.event_description)
  const [body, setBody] = useState(initialRequest.body)
  const [fitPreference, setFitPreference] = useState<(typeof fitOptions)[number]>(initialRequest.fit_preference)
  const [stylePreferences, setStylePreferences] = useState(initialRequest.style_preferences)
  const [audience, setAudience] = useState<Audience>('all')
  const [maxPrice, setMaxPrice] = useState<number | ''>('')
  const [useReference, setUseReference] = useState(false)
  const [reference, setReference] = useState(defaultReference)
  const [recommendations, setRecommendations] = useState<Recommendation[]>([])
  const [eventContext, setEventContext] = useState<Record<string, string>>({})
  const [selectedProductId, setSelectedProductId] = useState('')
  const [selectedSize, setSelectedSize] = useState('M')
  const [compareIds, setCompareIds] = useState<string[]>([])
  const [sortBy, setSortBy] = useState<'match' | 'price'>('match')
  const [cart, setCart] = useState<{ product_id: string; size: string; quantity: number }[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState('')
  const [checkoutMessage, setCheckoutMessage] = useState('')
  const [isCheckingOut, setIsCheckingOut] = useState(false)
  const [chat, setChat] = useState<ChatMessage[]>([
    { role: 'bot', text: 'Ask for a style shift, a budget, a size feel, or why one piece fits the event better.' },
  ])
  const [chatInput, setChatInput] = useState('')
  const [isChatting, setIsChatting] = useState(false)
  const [highlightIds, setHighlightIds] = useState<string[]>([])
  const [agentNotes, setAgentNotes] = useState<string[]>([])
  const [swarmQuery, setSwarmQuery] = useState('')
  const [isSwarming, setIsSwarming] = useState(false)

  const requestPayload = useCallback(
    () =>
      buildPayload({
        eventDescription, body, fitPreference, stylePreferences, audience, maxPrice, useReference, reference,
      }),
    [eventDescription, body, fitPreference, stylePreferences, audience, maxPrice, useReference, reference],
  )

  useEffect(() => {
    let active = true
    void getRecommendations(initialRequest)
      .then((response) => {
        if (!active) return
        setEventContext(response.event)
        setRecommendations(response.recommendations)
        setSelectedProductId(response.recommendations[0]?.product.id ?? '')
        setSelectedSize(response.recommendations[0]?.recommended_size ?? 'M')
      })
      .catch(() => {
        if (active) setError('The fit service is offline. Start the backend to load the demo catalog.')
      })
    return () => { active = false }
  }, [])

  const selected = useMemo(
    () => recommendations.find((item) => item.product.id === selectedProductId) ?? recommendations[0],
    [recommendations, selectedProductId],
  )
  const selectedSizeFit = useMemo(() => {
    if (!selected) return undefined
    return selected.size_fits[selectedSize] ?? { body_fit: selected.body_fit, areas: selected.areas }
  }, [selected, selectedSize])
  const displayedRecommendations = useMemo(
    () =>
      sortBy === 'price'
        ? [...recommendations].sort((a, b) => a.product.price - b.product.price)
        : recommendations,
    [recommendations, sortBy],
  )
  const compared = useMemo(
    () => recommendations.filter((item) => compareIds.includes(item.product.id)).slice(0, 3),
    [recommendations, compareIds],
  )
  const cartCount = useMemo(() => cart.reduce((t, i) => t + i.quantity, 0), [cart])
  const sceneImage =
    eventContext.scene_image ||
    'https://images.unsplash.com/photo-1533105079780-92b9be482077?auto=format&fit=crop&w=1800&q=88'

  const findMyEdit = useCallback(async () => {
    setIsLoading(true)
    setError('')
    setCheckoutMessage('')
    try {
      const result = await getRecommendations(requestPayload())
      setEventContext(result.event)
      setRecommendations(result.recommendations)
      setSelectedProductId(result.recommendations[0]?.product.id ?? '')
      setSelectedSize(result.recommendations[0]?.recommended_size ?? 'M')
      setHighlightIds([])
      setAgentNotes([])
    } catch {
      setError('Could not reach the fit service. Check that the backend is running and try again.')
    } finally {
      setIsLoading(false)
    }
  }, [requestPayload])

  const toggleStyle = useCallback((style: string) => {
    setStylePreferences((current) =>
      current.includes(style) ? current.filter((item) => item !== style) : [...current, style],
    )
  }, [])

  const toggleCompare = useCallback((productId: string) => {
    setCompareIds((current) => {
      if (current.includes(productId)) return current.filter((id) => id !== productId)
      if (current.length >= 3) return [...current.slice(1), productId]
      return [...current, productId]
    })
  }, [])

  const addToBag = useCallback(() => {
    if (!selected) return
    setCart((current) => {
      const existing = current.find((item) => item.product_id === selected.product.id && item.size === selectedSize)
      return existing
        ? current.map((item) => (item === existing ? { ...item, quantity: item.quantity + 1 } : item))
        : [...current, { product_id: selected.product.id, size: selectedSize, quantity: 1 }]
    })
    setCheckoutMessage(`${selected.product.name} (${selectedSize}) added to your bag.`)
    setError('')
  }, [selected, selectedSize])

  const completeOrder = useCallback(async () => {
    if (!cart.length) return
    setIsCheckingOut(true)
    setCheckoutMessage('')
    setError('')
    try {
      const result = await checkout(cart)
      setCheckoutMessage(`Order ${result.order_id} confirmed · $${result.total} · simulated Visa`)
      setCart([])
    } catch {
      setError('Checkout could not be completed. Please try again.')
    } finally {
      setIsCheckingOut(false)
    }
  }, [cart])

  const sendChat = useCallback(async (message: string) => {
    const trimmed = message.trim()
    if (!trimmed || isChatting) return
    setIsChatting(true)
    setChat((current) => [...current, { role: 'user', text: trimmed }])
    setChatInput('')
    try {
      const result = await askAssistant({ ...requestPayload(), message: trimmed })
      setEventContext(result.event)
      setRecommendations(result.recommendations)
      setHighlightIds(result.highlight_ids)
      if (result.suggested_fit && fitOptions.includes(result.suggested_fit as (typeof fitOptions)[number])) {
        setFitPreference(result.suggested_fit as (typeof fitOptions)[number])
      }
      if (result.highlight_ids[0]) {
        setSelectedProductId(result.highlight_ids[0])
        const match = result.recommendations.find((item) => item.product.id === result.highlight_ids[0])
        if (match) setSelectedSize(match.recommended_size)
      }
      setChat((current) => [...current, { role: 'bot', text: result.reply }])
    } catch {
      setChat((current) => [
        ...current,
        { role: 'bot', text: 'The assistant could not reach the fit service. Confirm the API is running.' },
      ])
    } finally {
      setIsChatting(false)
    }
  }, [isChatting, requestPayload])

  const runSwarm = useCallback(async (query?: string) => {
    const q = (query ?? swarmQuery).trim()
    if (!q || isSwarming) return
    setIsSwarming(true)
    setError('')
    try {
      const result = await swarmSearch({ ...requestPayload(), query: q })
      setEventContext(result.event)
      setRecommendations(result.recommendations)
      setHighlightIds(result.highlight_ids)
      setAgentNotes(result.agent_notes)
      setSwarmQuery(q)
      if (result.recommendations[0]) {
        setSelectedProductId(result.recommendations[0].product.id)
        setSelectedSize(result.recommendations[0].recommended_size)
      }
    } catch {
      setError('Agent swarm could not reach the fit service. Confirm the API is running.')
    } finally {
      setIsSwarming(false)
    }
  }, [swarmQuery, isSwarming, requestPayload])

  const value = useMemo<FormeContextValue>(() => ({
    eventDescription, setEventDescription, body, setBody, fitPreference, setFitPreference,
    stylePreferences, toggleStyle, audience, setAudience, maxPrice, setMaxPrice,
    useReference, setUseReference, reference, setReference, recommendations, eventContext,
    selectedProductId, setSelectedProductId, selectedSize, setSelectedSize, compareIds, toggleCompare,
    sortBy, setSortBy, cart, cartCount, isLoading, error, setError, checkoutMessage, setCheckoutMessage,
    isCheckingOut, chat, chatInput, setChatInput, isChatting, highlightIds, agentNotes, swarmQuery,
    setSwarmQuery, isSwarming, selected, selectedSizeFit, displayedRecommendations, compared, sceneImage,
    requestPayload, findMyEdit, addToBag, completeOrder, sendChat, runSwarm,
  }), [
    eventDescription, body, fitPreference, stylePreferences, toggleStyle, audience, maxPrice, useReference,
    reference, recommendations, eventContext, selectedProductId, selectedSize, compareIds, toggleCompare,
    sortBy, cart, cartCount, isLoading, error, checkoutMessage, isCheckingOut, chat, chatInput, isChatting,
    highlightIds, agentNotes, swarmQuery, isSwarming, selected, selectedSizeFit, displayedRecommendations,
    compared, sceneImage, requestPayload, findMyEdit, addToBag, completeOrder, sendChat, runSwarm,
  ])

  return <FormeContext.Provider value={value}>{children}</FormeContext.Provider>
}

// eslint-disable-next-line react-refresh/only-export-components -- intentional context hook export
export function useForme() {
  const ctx = useContext(FormeContext)
  if (!ctx) throw new Error('useForme must be used within FormeProvider')
  return ctx
}
