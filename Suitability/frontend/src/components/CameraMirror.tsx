import { useEffect, useRef, useState } from 'react'
import { Camera, CameraOff, LoaderCircle, RotateCcw, Sparkles } from 'lucide-react'
import { FilesetResolver, PoseLandmarker } from '@mediapipe/tasks-vision'
import { generateTryOn, type Product } from '../api'
import {
  drawCenteredGarmentFallback,
  drawGarmentOverlay,
  drawVideoCover,
  mapPoseToCanvas,
  sizeScaleFactor,
  type MappedLandmark,
} from './overlayGeometry'
import './CameraMirror.css'

type Props = {
  product: Product
  size: string
  environmentImage?: string
  environmentLabel?: string
}

type MirrorStatus = 'idle' | 'loading' | 'tracking' | 'searching' | 'error' | 'generating'

const taskVersion = '0.10.21'
const poseModelUrl =
  'https://storage.googleapis.com/mediapipe-models/pose_landmarker/pose_landmarker_lite/float16/1/pose_landmarker_lite.task'

export function CameraMirror({ product, size, environmentImage, environmentLabel }: Props) {
  const videoRef = useRef<HTMLVideoElement>(null)
  const canvasRef = useRef<HTMLCanvasElement>(null)
  const streamRef = useRef<MediaStream | null>(null)
  const landmarkerRef = useRef<PoseLandmarker | null>(null)
  const frameRef = useRef<number | null>(null)
  const lastInferenceRef = useRef(0)
  const lastMappedRef = useRef<MappedLandmark[] | null>(null)
  const garmentImgRef = useRef<HTMLImageElement | null>(null)
  const startupIdRef = useRef(0)
  const autoGenerateRef = useRef(false)
  const [cameraActive, setCameraActive] = useState(false)
  const [modelReady, setModelReady] = useState(false)
  const [facingMode, setFacingMode] = useState<'user' | 'environment'>('user')
  const [status, setStatus] = useState<MirrorStatus>('idle')
  const [message, setMessage] = useState('')
  const [tryOnImage, setTryOnImage] = useState<string | null>(null)
  const [privacyNote, setPrivacyNote] = useState(
    'Live fit overlay stays on-device. Photo try-on sends one frame to the API when you generate.',
  )
  const [provider, setProvider] = useState('')
  const [liveOverlay, setLiveOverlay] = useState(true)
  const [garmentReady, setGarmentReady] = useState(false)

  // Prefetch product garment art for the live overlay.
  useEffect(() => {
    setGarmentReady(false)
    const img = new Image()
    img.decoding = 'async'
    img.onload = () => {
      garmentImgRef.current = img
      setGarmentReady(true)
    }
    img.onerror = () => {
      garmentImgRef.current = null
      setGarmentReady(false)
    }
    img.src = product.image
  }, [product.image, product.id])

  useEffect(() => {
    if (!cameraActive || tryOnImage) return
    const video = videoRef.current
    const canvas = canvasRef.current
    const context = canvas?.getContext('2d')
    if (!video || !canvas || !context) return
    let running = true
    const mirrorPreview = facingMode === 'user'

    const draw = () => {
      if (!running) return
      if (video.readyState >= HTMLMediaElement.HAVE_CURRENT_DATA && video.videoWidth > 0) {
        const pixelRatio = window.devicePixelRatio || 1
        const displayWidth = Math.max(1, Math.round(canvas.clientWidth * pixelRatio))
        const displayHeight = Math.max(1, Math.round(canvas.clientHeight * pixelRatio))
        if (canvas.width !== displayWidth || canvas.height !== displayHeight) {
          canvas.width = displayWidth
          canvas.height = displayHeight
        }
        context.save()
        context.clearRect(0, 0, canvas.width, canvas.height)
        if (mirrorPreview) {
          context.translate(canvas.width, 0)
          context.scale(-1, 1)
        }
        drawVideoCover(context, video, canvas.width, canvas.height)

        const landmarker = landmarkerRef.current
        if (liveOverlay) {
          const timestamp = performance.now()
          if (modelReady && landmarker && timestamp - lastInferenceRef.current >= 33) {
            lastInferenceRef.current = timestamp
            try {
              const result = landmarker.detectForVideo(video, timestamp)
              const landmarks = result.landmarks[0]
              const shouldersVisible =
                landmarks &&
                (landmarks[11].visibility ?? 0) > 0.3 &&
                (landmarks[12].visibility ?? 0) > 0.3
              if (landmarks && shouldersVisible) {
                lastMappedRef.current = mapPoseToCanvas(
                  landmarks,
                  video.videoWidth,
                  video.videoHeight,
                  canvas.width,
                  canvas.height,
                )
                setStatus((c) => (c === 'generating' ? c : 'tracking'))
              } else {
                lastMappedRef.current = null
                setStatus((c) => (c === 'generating' ? c : 'searching'))
              }
            } catch {
              /* keep last mapped frame */
            }
          }

          const mapped = lastMappedRef.current
          if (mapped) {
            drawGarmentOverlay(context, mapped, {
              color: product.overlay_color,
              scale: sizeScaleFactor(size),
              garmentImage: garmentImgRef.current,
              opacity: 0.88,
            })
          } else {
            // Show clothing while finding frame / pose boots so the feed is never bare.
            drawCenteredGarmentFallback(
              context,
              canvas.width,
              canvas.height,
              product.overlay_color,
              garmentImgRef.current,
            )
          }
        }
        context.restore()
      }
      frameRef.current = requestAnimationFrame(draw)
    }
    frameRef.current = requestAnimationFrame(draw)
    return () => {
      running = false
      if (frameRef.current !== null) cancelAnimationFrame(frameRef.current)
    }
  }, [
    cameraActive,
    tryOnImage,
    facingMode,
    liveOverlay,
    modelReady,
    product.overlay_color,
    size,
    garmentReady,
  ])

  useEffect(
    () => () => {
      startupIdRef.current += 1
      streamRef.current?.getTracks().forEach((t) => t.stop())
      landmarkerRef.current?.close()
      if (frameRef.current !== null) cancelAnimationFrame(frameRef.current)
    },
    [],
  )

  async function startCamera(nextFacing: 'user' | 'environment' = facingMode) {
    const startupId = ++startupIdRef.current
    streamRef.current?.getTracks().forEach((t) => t.stop())
    streamRef.current = null
    landmarkerRef.current?.close()
    landmarkerRef.current = null
    lastMappedRef.current = null
    autoGenerateRef.current = false
    setCameraActive(false)
    setModelReady(false)
    setTryOnImage(null)
    setMessage('')
    setStatus('loading')
    setFacingMode(nextFacing)
    setLiveOverlay(true)
    try {
      if (!navigator.mediaDevices?.getUserMedia) throw new Error('Camera access requires HTTPS or localhost.')
      const stream = await navigator.mediaDevices.getUserMedia({
        audio: false,
        video: { facingMode: { ideal: nextFacing }, width: { ideal: 1280 }, height: { ideal: 960 } },
      })
      if (startupId !== startupIdRef.current) {
        stream.getTracks().forEach((t) => t.stop())
        return
      }
      streamRef.current = stream
      const video = videoRef.current
      if (!video) throw new Error('Camera preview element is unavailable.')
      video.srcObject = stream
      await video.play()
      if (startupId !== startupIdRef.current) return
      setCameraActive(true)
      setStatus('searching')
      setMessage(`Live overlay on for ${product.name}. Step back until shoulders are in frame.`)
      setPrivacyNote('Live fit overlay stays on-device. Photo try-on sends one frame when you generate.')
      try {
        const files = await FilesetResolver.forVisionTasks(
          `https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision@${taskVersion}/wasm`,
        )
        if (startupId !== startupIdRef.current) return
        let landmarker: PoseLandmarker
        try {
          landmarker = await PoseLandmarker.createFromOptions(files, {
            baseOptions: { modelAssetPath: poseModelUrl, delegate: 'GPU' },
            runningMode: 'VIDEO',
            numPoses: 1,
          })
        } catch {
          landmarker = await PoseLandmarker.createFromOptions(files, {
            baseOptions: { modelAssetPath: poseModelUrl, delegate: 'CPU' },
            runningMode: 'VIDEO',
            numPoses: 1,
          })
        }
        if (startupId !== startupIdRef.current) {
          landmarker.close()
          return
        }
        landmarkerRef.current = landmarker
        setModelReady(true)
      } catch (caught) {
        const detail = caught instanceof Error ? caught.message : 'pose init failed'
        setMessage(
          `Camera is live with a centered garment preview. Pose tracking failed (${detail}) — use Generate for photo try-on.`,
        )
      }
    } catch (caught) {
      if (startupId !== startupIdRef.current) return
      const detail = caught instanceof Error ? caught.message : 'Unknown startup error.'
      streamRef.current?.getTracks().forEach((t) => t.stop())
      streamRef.current = null
      setCameraActive(false)
      setStatus('error')
      setMessage(
        detail.includes('Permission') || detail.includes('denied')
          ? 'Camera permission was blocked. Allow access in browser settings, then retry.'
          : detail,
      )
    }
  }

  function stopCamera() {
    startupIdRef.current += 1
    streamRef.current?.getTracks().forEach((t) => t.stop())
    streamRef.current = null
    landmarkerRef.current?.close()
    landmarkerRef.current = null
    lastMappedRef.current = null
    setCameraActive(false)
    setModelReady(false)
    setTryOnImage(null)
    setStatus('idle')
    setMessage('')
  }

  async function captureAndGenerate() {
    const video = videoRef.current
    if (!video || video.videoWidth <= 0) {
      setMessage('Start the camera and stand in frame before generating a try-on.')
      setStatus('error')
      return
    }
    setStatus('generating')
    setMessage('Capturing frame and generating photo try-on…')
    try {
      const capture = document.createElement('canvas')
      const scale = Math.min(1, 768 / video.videoWidth)
      capture.width = Math.round(video.videoWidth * scale)
      capture.height = Math.round(video.videoHeight * scale)
      const ctx = capture.getContext('2d')
      if (!ctx) throw new Error('Could not capture frame')
      ctx.drawImage(video, 0, 0, capture.width, capture.height)
      const dataUrl = capture.toDataURL('image/jpeg', 0.92)
      const result = await generateTryOn({
        person_image_base64: dataUrl,
        product_id: product.id,
        garment_image_url: product.image.startsWith('http')
          ? product.image
          : `${window.location.origin}${product.image}`,
        garment_color: product.overlay_color,
        garment_name: product.name,
        size,
      })
      setTryOnImage(`data:image/png;base64,${result.image_base64}`)
      setProvider(result.provider)
      setPrivacyNote(result.privacy)
      setMessage(result.message || 'Photo try-on ready. Live overlay is paused while viewing result.')
      setStatus('tracking')
    } catch (caught) {
      const detail = caught instanceof Error ? caught.message : 'Try-on failed'
      setMessage(`Photo try-on failed: ${detail}. Live overlay remains on — check that the API is running.`)
      setStatus(liveOverlay ? 'tracking' : 'error')
    }
  }

  // Once tracking locks, offer one soft auto-generate prompt (does not block live overlay).
  useEffect(() => {
    if (!cameraActive || !modelReady || status !== 'tracking' || tryOnImage || autoGenerateRef.current) return
    autoGenerateRef.current = true
    const timer = window.setTimeout(() => {
      setMessage((current) =>
        current?.includes('Photo try-on')
          ? current
          : `Tracking ${product.name}. Tap “Generate photo try-on” for a realistic result, or keep the live overlay.`,
      )
    }, 1800)
    return () => window.clearTimeout(timer)
  }, [cameraActive, modelReady, status, tryOnImage, product.name])

  const statusLabel =
    status === 'loading'
      ? 'Starting camera'
      : status === 'generating'
        ? 'Generating photo try-on…'
        : tryOnImage
          ? `Photo try-on · ${provider || 'ready'} · size ${size}`
          : status === 'tracking'
            ? `Live fit · ${product.name} · size ${size}`
            : status === 'searching'
              ? 'Find shoulders in frame'
              : 'Preview mode'

  return (
    <div className="mirror-column">
      <div
        className="mirror-stage"
        style={
          environmentImage
            ? {
                backgroundImage: `linear-gradient(180deg, rgba(12,22,28,.55), rgba(12,22,28,.28) 40%, rgba(12,22,28,.72)), url('${environmentImage}')`,
              }
            : undefined
        }
      >
        <div className="mirror-env-chip">
          <span>SETTING</span>
          <strong>{environmentLabel || 'Your event'}</strong>
        </div>
        <div className="mirror-frame">
          <video ref={videoRef} className="mirror-video-source" playsInline muted aria-hidden="true" />
          {tryOnImage ? (
            <img className="mirror-tryon-result" src={tryOnImage} alt={`Generative try-on of ${product.name}`} />
          ) : (
            <canvas
              ref={canvasRef}
              className={cameraActive ? 'mirror-canvas' : 'mirror-canvas is-idle'}
              aria-label="Live camera preview with garment overlay"
            />
          )}
          {!cameraActive && !tryOnImage && (
            <img className="mirror-fallback" src={product.image} alt={`${product.name} garment`} />
          )}
          <div className="mirror-vignette" />
          {cameraActive && !tryOnImage && (
            <div className="mirror-garment-chip">
              <span>TRYING ON</span>
              <strong>{product.name}</strong>
            </div>
          )}
          <div className="mirror-status">
            <i className={status === 'tracking' || tryOnImage ? 'status-light is-live' : 'status-light'} />
            {statusLabel}
          </div>
          <span className="mirror-index">CAM · {tryOnImage ? 'TRY-ON' : cameraActive ? 'LIVE' : 'STILL'}</span>
          {cameraActive && status === 'searching' && !tryOnImage && (
            <p className="tracking-prompt">
              Step back until your shoulders
              <br />
              and hips are in frame
            </p>
          )}
        </div>
      </div>
      <div className="camera-controls">
        <p>
          <span className="privacy-dot" /> {privacyNote}
        </p>
        {cameraActive ? (
          <div className="camera-actions">
            <button
              className="camera-button"
              type="button"
              onClick={() => void captureAndGenerate()}
              disabled={status === 'generating' || status === 'loading'}
            >
              {status === 'generating' ? <LoaderCircle size={16} className="spin" /> : <Sparkles size={16} />}
              {status === 'generating'
                ? 'Generating…'
                : tryOnImage
                  ? 'Regenerate photo try-on'
                  : 'Generate photo try-on'}
            </button>
            {tryOnImage && (
              <button
                className="camera-button camera-button-muted"
                type="button"
                onClick={() => {
                  setTryOnImage(null)
                  setMessage('Back to live overlay.')
                }}
              >
                Back to live overlay
              </button>
            )}
            <button
              className="camera-button camera-button-muted"
              type="button"
              onClick={() => setLiveOverlay((c) => !c)}
              disabled={!!tryOnImage}
            >
              {liveOverlay ? 'Hide live overlay' : 'Show live overlay'}
            </button>
            <button
              className="camera-button camera-button-muted"
              type="button"
              onClick={() => void startCamera(facingMode === 'user' ? 'environment' : 'user')}
            >
              <RotateCcw size={15} /> Flip
            </button>
            <button className="camera-button camera-button-muted" type="button" onClick={stopCamera}>
              <CameraOff size={16} /> Stop
            </button>
          </div>
        ) : (
          <button
            className="camera-button"
            type="button"
            onClick={() => void startCamera()}
            disabled={status === 'loading'}
          >
            {status === 'loading' ? <LoaderCircle size={16} className="spin" /> : <Camera size={16} />}
            {status === 'loading' ? 'Starting…' : 'Start camera · live fit'}
          </button>
        )}
      </div>
      {message && (
        <div className="camera-message" role="status">
          <span>{message}</span>
          <button type="button" onClick={() => setMessage('')} aria-label="Dismiss">
            <RotateCcw size={14} />
          </button>
        </div>
      )}
    </div>
  )
}
