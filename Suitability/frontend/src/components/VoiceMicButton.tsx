import { useEffect, useRef, useState } from 'react'
import { LoaderCircle, Mic, MicOff } from 'lucide-react'
import { transcribeAudio } from '../api'

type MicState = 'idle' | 'listening' | 'transcribing' | 'error'

type Props = {
  onTranscript: (text: string) => void
  label?: string
  className?: string
}

type SpeechRecognitionResultLike = {
  isFinal?: boolean
  0?: { transcript?: string }
}

type SpeechRecognitionEventLike = {
  resultIndex: number
  results: ArrayLike<SpeechRecognitionResultLike> & { length: number }
}

type SpeechRecognitionLike = {
  continuous: boolean
  interimResults: boolean
  lang: string
  onresult: ((event: SpeechRecognitionEventLike) => void) | null
  onerror: ((event: { error?: string }) => void) | null
  onend: (() => void) | null
  start: () => void
  stop: () => void
  abort: () => void
}

type AsrPipeline = (
  audio: Float32Array,
  options?: { chunk_length_s?: number; stride_length_s?: number },
) => Promise<{ text?: string } | string>

let whisperPipelinePromise: Promise<AsrPipeline> | null = null

function getSpeechRecognition(): (new () => SpeechRecognitionLike) | null {
  const win = window as Window & {
    SpeechRecognition?: new () => SpeechRecognitionLike
    webkitSpeechRecognition?: new () => SpeechRecognitionLike
  }
  return win.SpeechRecognition || win.webkitSpeechRecognition || null
}

function pickRecorderMimeType(): string {
  if (typeof MediaRecorder === 'undefined') return ''
  const candidates = [
    'audio/webm;codecs=opus',
    'audio/webm',
    'audio/mp4',
    'audio/ogg;codecs=opus',
  ]
  return candidates.find((type) => MediaRecorder.isTypeSupported(type)) || ''
}

function micErrorMessage(error: unknown): string {
  const name = error instanceof DOMException ? error.name : ''
  const message = error instanceof Error ? error.message : ''
  if (name === 'NotAllowedError' || name === 'PermissionDeniedError') {
    return 'Microphone permission was blocked. Allow mic access and try again.'
  }
  if (name === 'NotFoundError' || name === 'DevicesNotFoundError') {
    return 'No microphone found. Plug one in or type instead.'
  }
  if (name === 'NotReadableError' || name === 'TrackStartError') {
    return 'Microphone is busy in another app. Close it and try again.'
  }
  if (name === 'SecurityError' || /secure|https|permission/i.test(message)) {
    return 'Voice input needs HTTPS or localhost.'
  }
  return message || 'Could not access the microphone.'
}

async function getWhisperPipeline(): Promise<AsrPipeline> {
  if (!whisperPipelinePromise) {
    whisperPipelinePromise = (async () => {
      const { pipeline, env } = await import('@xenova/transformers')
      env.allowLocalModels = false
      env.useBrowserCache = true
      return (await pipeline('automatic-speech-recognition', 'Xenova/whisper-tiny.en', {
        quantized: true,
      })) as AsrPipeline
    })().catch((error) => {
      whisperPipelinePromise = null
      throw error
    })
  }
  return whisperPipelinePromise
}

async function blobToFloat32Audio(blob: Blob): Promise<Float32Array> {
  const arrayBuffer = await blob.arrayBuffer()
  const AudioCtx = window.AudioContext || (window as Window & { webkitAudioContext?: typeof AudioContext }).webkitAudioContext
  if (!AudioCtx) throw new Error('Web Audio is not supported in this browser.')
  const audioCtx = new AudioCtx()
  try {
    if (audioCtx.state === 'suspended') await audioCtx.resume()
    const decoded = await audioCtx.decodeAudioData(arrayBuffer.slice(0))
    const channel = decoded.getChannelData(0)
    if (decoded.sampleRate === 16000) return new Float32Array(channel)
    const ratio = decoded.sampleRate / 16000
    const length = Math.max(1, Math.floor(channel.length / ratio))
    const samples = new Float32Array(length)
    for (let i = 0; i < length; i += 1) samples[i] = channel[Math.min(channel.length - 1, Math.floor(i * ratio))]
    return samples
  } finally {
    await audioCtx.close().catch(() => undefined)
  }
}

async function transcribeWithTransformers(blob: Blob): Promise<string | null> {
  const samples = await blobToFloat32Audio(blob)
  if (samples.length < 1600) return null
  const transcriber = await getWhisperPipeline()
  const result = await transcriber(samples, { chunk_length_s: 20, stride_length_s: 5 })
  const text = typeof result === 'string' ? result : result?.text || ''
  return text.trim() || null
}

export function VoiceMicButton({ onTranscript, label = 'Speak', className = '' }: Props) {
  const [state, setState] = useState<MicState>('idle')
  const [message, setMessage] = useState('')
  const mediaRecorderRef = useRef<MediaRecorder | null>(null)
  const chunksRef = useRef<Blob[]>([])
  const recognitionRef = useRef<SpeechRecognitionLike | null>(null)
  const streamRef = useRef<MediaStream | null>(null)
  const modeRef = useRef<'recorder' | 'speech' | null>(null)
  const stopTimerRef = useRef<number | null>(null)
  const cancelledRef = useRef(false)
  const finalTextRef = useRef('')
  const interimTextRef = useRef('')
  const stoppingRef = useRef(false)
  const restartingRef = useRef(false)

  function clearStopTimer() {
    if (stopTimerRef.current != null) {
      window.clearTimeout(stopTimerRef.current)
      stopTimerRef.current = null
    }
  }

  function cleanupStream() {
    streamRef.current?.getTracks().forEach((track) => track.stop())
    streamRef.current = null
  }

  useEffect(
    () => () => {
      cancelledRef.current = true
      clearStopTimer()
      try {
        if (mediaRecorderRef.current?.state === 'recording') mediaRecorderRef.current.stop()
      } catch {
        /* ignore */
      }
      try {
        recognitionRef.current?.abort()
      } catch {
        /* ignore */
      }
      cleanupStream()
    },
    [],
  )

  function succeed(text: string) {
    if (cancelledRef.current) return
    onTranscript(text)
    setState('idle')
    setMessage('')
    modeRef.current = null
    stoppingRef.current = false
  }

  function fail(msg: string) {
    if (cancelledRef.current) return
    setState('error')
    setMessage(msg)
    modeRef.current = null
    stoppingRef.current = false
  }

  function currentSpeechText() {
    return (finalTextRef.current || interimTextRef.current).trim()
  }

  function startWebSpeech() {
    const SpeechRecognition = getSpeechRecognition()
    if (!SpeechRecognition) {
      fail('Browser speech is unavailable. Type instead, or try Chrome/Edge.')
      return
    }

    try {
      recognitionRef.current?.abort()
    } catch {
      /* ignore */
    }

    const recognition = new SpeechRecognition()
    recognition.continuous = true
    recognition.interimResults = true
    recognition.lang = 'en-US'
    modeRef.current = 'speech'
    finalTextRef.current = ''
    interimTextRef.current = ''
    stoppingRef.current = false
    restartingRef.current = false

    setState('listening')
    setMessage('Listening… click Speak again to stop')

    recognition.onresult = (event) => {
      let interim = ''
      for (let i = event.resultIndex; i < event.results.length; i += 1) {
        const result = event.results[i]
        const piece = result?.[0]?.transcript || ''
        if (!piece) continue
        if (result?.isFinal) {
          finalTextRef.current = `${finalTextRef.current} ${piece}`.replace(/\s+/g, ' ').trim()
        } else {
          interim += piece
        }
      }
      interimTextRef.current = interim.trim()
      const heard = currentSpeechText()
      if (heard) {
        setMessage(`Heard: “${heard.slice(0, 56)}${heard.length > 56 ? '…' : ''}” — click to stop`)
      }
    }

    recognition.onerror = (event) => {
      if (event.error === 'aborted' || event.error === 'no-speech') return
      if (event.error === 'not-allowed') {
        fail('Microphone permission was blocked. Allow mic access and try again.')
        return
      }
      if (event.error === 'network') {
        fail('Browser speech needs network access. Check connection or type instead.')
        return
      }
      // Keep listening on transient errors if we already heard something.
      if (currentSpeechText()) return
      fail(`Listening failed (${event.error || 'unknown'}). Try again or type instead.`)
    }

    recognition.onend = () => {
      if (recognitionRef.current === recognition) recognitionRef.current = null
      if (modeRef.current !== 'speech') return

      // Push-to-talk stop: use whatever we accumulated (final or interim).
      if (stoppingRef.current) {
        const text = currentSpeechText()
        if (text) {
          succeed(text)
          return
        }
        fail('No speech captured. Click Speak, talk, then click again to stop.')
        return
      }

      // Chrome often fires onend while still listening — restart unless user stopped.
      if (!restartingRef.current && !cancelledRef.current) {
        restartingRef.current = true
        recognitionRef.current = recognition
        try {
          recognition.start()
          restartingRef.current = false
          return
        } catch {
          restartingRef.current = false
        }
      }

      const text = currentSpeechText()
      if (text) {
        succeed(text)
        return
      }
      // Do not falsely error on a bare early onend — stay idle with a nudge.
      setState('idle')
      setMessage('Click Speak, talk, then click again to stop.')
      modeRef.current = null
    }

    recognitionRef.current = recognition
    try {
      recognition.start()
    } catch {
      fail('Could not start browser speech. Try again or type instead.')
    }
  }

  async function finishWithBlob(blob: Blob) {
    setState('transcribing')
    setMessage('Transcribing…')
    modeRef.current = null

    if (!blob.size) {
      fail('No audio captured. Try again or type instead.')
      return
    }

    try {
      const remote = await transcribeAudio(blob)
      if (remote.text?.trim()) {
        succeed(remote.text.trim())
        return
      }
    } catch (error) {
      console.warn('Server Whisper failed:', error)
    }

    try {
      setMessage('Loading Whisper…')
      const local = await transcribeWithTransformers(blob)
      if (local) {
        succeed(local)
        return
      }
    } catch (error) {
      console.warn('On-device Whisper failed:', error)
    }

    fail('Could not transcribe. Try Chrome/Edge Speak, or type instead.')
  }

  async function startRecorder() {
    if (!navigator.mediaDevices?.getUserMedia) {
      throw new DOMException('Microphone requires HTTPS or localhost.', 'SecurityError')
    }
    if (typeof MediaRecorder === 'undefined') {
      throw new Error('MediaRecorder is not supported in this browser.')
    }

    const stream = await navigator.mediaDevices.getUserMedia({
      audio: { echoCancellation: true, noiseSuppression: true, channelCount: 1 },
    })
    streamRef.current = stream
    chunksRef.current = []

    const mimeType = pickRecorderMimeType()
    const recorder = mimeType ? new MediaRecorder(stream, { mimeType }) : new MediaRecorder(stream)
    mediaRecorderRef.current = recorder
    modeRef.current = 'recorder'
    setState('listening')
    setMessage('Listening… click again to stop')

    recorder.ondataavailable = (event) => {
      if (event.data.size > 0) chunksRef.current.push(event.data)
    }
    recorder.onerror = () => {
      cleanupStream()
      fail('Recording failed. Try again or type instead.')
    }
    recorder.onstop = () => {
      clearStopTimer()
      cleanupStream()
      mediaRecorderRef.current = null
      const type = recorder.mimeType || mimeType || 'audio/webm'
      void finishWithBlob(new Blob(chunksRef.current, { type }))
      chunksRef.current = []
    }

    recorder.start(250)
    clearStopTimer()
    stopTimerRef.current = window.setTimeout(() => {
      if (mediaRecorderRef.current === recorder && recorder.state === 'recording') recorder.stop()
    }, 12000)
  }

  async function startListening() {
    cancelledRef.current = false
    setState('listening')
    setMessage('Starting…')

    // Primary: Web Speech (Chrome/Edge) — push-to-talk.
    if (getSpeechRecognition()) {
      startWebSpeech()
      return
    }

    // Fallback: MediaRecorder + Whisper when Web Speech is missing.
    try {
      await startRecorder()
    } catch (error) {
      cleanupStream()
      fail(micErrorMessage(error))
    }
  }

  function stopListening() {
    clearStopTimer()

    if (modeRef.current === 'speech' && recognitionRef.current) {
      stoppingRef.current = true
      const text = currentSpeechText()
      setMessage(text ? 'Finishing…' : 'Stopping…')
      try {
        recognitionRef.current.stop()
      } catch {
        if (text) succeed(text)
        else fail('No speech captured. Click Speak, talk, then click again to stop.')
      }
      return
    }

    if (modeRef.current === 'recorder' && mediaRecorderRef.current?.state === 'recording') {
      try {
        mediaRecorderRef.current.requestData()
      } catch {
        /* ignore */
      }
      mediaRecorderRef.current.stop()
      setState('transcribing')
      setMessage('Stopping…')
    }
  }

  const busy = state === 'transcribing'
  const listening = state === 'listening'

  return (
    <div className={`voice-mic ${className}`.trim()}>
      <button
        type="button"
        className={`voice-mic-button${listening ? ' is-listening' : ''}${state === 'error' ? ' is-error' : ''}`}
        onClick={() => (listening ? stopListening() : void startListening())}
        disabled={busy}
        aria-pressed={listening}
        aria-label={listening ? 'Stop listening' : label}
        title={label}
      >
        {busy ? (
          <LoaderCircle size={16} className="spin" />
        ) : listening ? (
          <MicOff size={16} />
        ) : (
          <Mic size={16} />
        )}
        <span>{listening ? 'Listening…' : busy ? 'Transcribing…' : label}</span>
      </button>
      {message ? (
        <p className={`voice-mic-message${state === 'error' ? ' is-error' : ''}`} role="status">
          {message}
        </p>
      ) : null}
    </div>
  )
}
