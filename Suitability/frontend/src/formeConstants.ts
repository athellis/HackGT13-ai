import type { Audience, ReferenceGarment } from './api'

export const fitOptions = ['fitted', 'regular', 'relaxed', 'oversized'] as const
export const styleOptions = ['minimal', 'coastal', 'modern', 'tailored', 'utility', 'polished']
export const audienceOptions: { value: Audience; label: string }[] = [
  { value: 'all', label: 'All' },
  { value: 'women', label: 'Women' },
  { value: 'men', label: 'Men' },
  { value: 'unisex', label: 'Unisex' },
]

export const initialRequest = {
  event_description: 'Birthday dinner by the water in Greece in July',
  body: { chest: 38, waist: 32, shoulders: 17, sleeves: 24.5 },
  fit_preference: 'relaxed' as const,
  style_preferences: ['minimal', 'coastal'],
  audience: 'all' as Audience,
}

export const defaultReference: ReferenceGarment = {
  brand: 'Brand A',
  name: 'Favorite tee',
  label: 'M',
  measurements: { chest: 42, waist: 40, shoulders: 17.5, sleeves: 24 },
}

export const quickPrompts = [
  'Show me something less formal',
  'Find a jacket under $150',
  'Which size feels more oversized?',
  'Why is the top pick better for the event?',
]

export type ChatMessage = { role: 'user' | 'bot'; text: string }
