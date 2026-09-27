export type GarmentSize = {
  label: string
  measurements: Record<string, number>
}

export type Audience = 'all' | 'women' | 'men' | 'unisex'

export type EnvProps = {
  waterproof: number
  windproof: number
  insulation: number
  breathability: number
  uv_protection: number
  temperature_min: number
  temperature_max: number
}

export type Product = {
  id: string
  audience: Exclude<Audience, 'all'>
  brand: string
  name: string
  category: string
  price: number
  color: string
  image: string
  overlay_color: string
  material: string
  style_tags: string[]
  formality: string[]
  weather_tags: string[]
  activity_tags: string[]
  env_props?: EnvProps
  sizes: GarmentSize[]
}

export type CrossBrand = {
  summary: string
  deltas: Record<string, number>
  notes: string[]
}

export type Recommendation = {
  product: Product
  body_fit: number
  style_match: number
  event_match: number
  env_match: number
  env_areas: Record<string, number>
  overall: number
  recommended_size: string
  areas: Record<string, number>
  size_fits: Record<string, { body_fit: number; areas: Record<string, number> }>
  reasons: string[]
  cross_brand?: CrossBrand | null
  agent_votes?: string[]
}

export type Environment = {
  id: string
  label: string
  image: string
  temp_f: number
  conditions: string[]
  needs: Record<string, number>
}

export type ReferenceGarment = {
  brand: string
  name: string
  label: string
  measurements: Record<string, number>
}

export type RecommendationRequest = {
  event_description: string
  body: { chest: number; waist: number; shoulders: number; sleeves?: number }
  fit_preference: 'fitted' | 'regular' | 'relaxed' | 'oversized'
  style_preferences: string[]
  audience: Audience
  max_price?: number
  reference?: ReferenceGarment
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(path, {
    ...init,
    headers: { 'Content-Type': 'application/json', ...init?.headers },
  })
  if (!response.ok) {
    const detail = await response.text()
    throw new Error(detail || `Request failed (${response.status})`)
  }
  return response.json() as Promise<T>
}

export function getRecommendations(payload: RecommendationRequest) {
  return request<{
    event: Record<string, string>
    environment?: Environment
    recommendations: Recommendation[]
  }>('/api/recommendations', { method: 'POST', body: JSON.stringify(payload) })
}

export function askAssistant(payload: RecommendationRequest & { message: string }) {
  return request<{
    event: Record<string, string>
    environment?: Environment
    reply: string
    action: string
    suggested_fit: string
    highlight_ids: string[]
    recommendations: Recommendation[]
    highlighted: Recommendation[]
  }>('/api/assistant', { method: 'POST', body: JSON.stringify(payload) })
}

export function swarmSearch(payload: RecommendationRequest & { query: string }) {
  return request<{
    event: Record<string, string>
    environment?: Environment
    recommendations: Recommendation[]
    highlight_ids: string[]
    agent_notes: string[]
    query: string
  }>('/api/swarm-search', { method: 'POST', body: JSON.stringify(payload) })
}

export function checkout(items: { product_id: string; size: string; quantity: number }[]) {
  return request<{ order_id: string; status: string; total: number; payment?: string }>(
    '/api/checkout',
    { method: 'POST', body: JSON.stringify({ items }) },
  )
}

function audioUploadName(blob: Blob): string {
  const type = (blob.type || '').toLowerCase()
  if (type.includes('wav')) return 'voice.wav'
  if (type.includes('mpeg') || type.includes('mp3')) return 'voice.mp3'
  if (type.includes('mp4') || type.includes('m4a') || type.includes('aac')) return 'voice.mp4'
  if (type.includes('ogg')) return 'voice.ogg'
  if (type.includes('webm')) return 'voice.webm'
  return 'voice.webm'
}

export async function transcribeAudio(blob: Blob) {
  const form = new FormData()
  form.append('file', blob, audioUploadName(blob))
  const response = await fetch('/api/transcribe', { method: 'POST', body: form })
  if (!response.ok) {
    const detail = await response.text()
    throw new Error(detail || `Transcription failed (${response.status})`)
  }
  return response.json() as Promise<{ text: string; provider: string; error?: string }>
}

export async function generateTryOn(payload: {
  person_image_base64: string
  product_id: string
  garment_image_url?: string
  garment_color?: string
  garment_name?: string
  size?: string
}) {
  return request<{
    image_base64: string
    provider: string
    privacy: string
    message?: string
  }>('/api/tryon', { method: 'POST', body: JSON.stringify(payload) })
}
