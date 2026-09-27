export type LandmarkPoint = { x: number; y: number; visibility?: number }

export type CoverTransform = {
  scale: number
  offsetX: number
  offsetY: number
  drawWidth: number
  drawHeight: number
}

export type MappedLandmark = { x: number; y: number; visibility: number }

export function getCoverTransform(
  sourceWidth: number,
  sourceHeight: number,
  destWidth: number,
  destHeight: number,
): CoverTransform {
  const scale = Math.max(destWidth / sourceWidth, destHeight / sourceHeight)
  const drawWidth = sourceWidth * scale
  const drawHeight = sourceHeight * scale
  return {
    scale,
    offsetX: (destWidth - drawWidth) / 2,
    offsetY: (destHeight - drawHeight) / 2,
    drawWidth,
    drawHeight,
  }
}

export function landmarkToCanvas(
  landmark: LandmarkPoint,
  sourceWidth: number,
  sourceHeight: number,
  destWidth: number,
  destHeight: number,
): MappedLandmark {
  const { scale, offsetX, offsetY } = getCoverTransform(sourceWidth, sourceHeight, destWidth, destHeight)
  return {
    x: landmark.x * sourceWidth * scale + offsetX,
    y: landmark.y * sourceHeight * scale + offsetY,
    visibility: landmark.visibility ?? 0,
  }
}

export function mapPoseToCanvas(
  landmarks: LandmarkPoint[],
  sourceWidth: number,
  sourceHeight: number,
  destWidth: number,
  destHeight: number,
) {
  return landmarks.map((landmark) =>
    landmarkToCanvas(landmark, sourceWidth, sourceHeight, destWidth, destHeight),
  )
}

export function drawVideoCover(
  context: CanvasRenderingContext2D,
  video: HTMLVideoElement,
  destWidth: number,
  destHeight: number,
) {
  const sourceWidth = video.videoWidth
  const sourceHeight = video.videoHeight
  if (sourceWidth <= 0 || sourceHeight <= 0) return null
  const transform = getCoverTransform(sourceWidth, sourceHeight, destWidth, destHeight)
  context.drawImage(video, transform.offsetX, transform.offsetY, transform.drawWidth, transform.drawHeight)
  return transform
}

export function sizeScaleFactor(size: string) {
  if (size === 'S') return 0.9
  if (size === 'L') return 1.1
  if (size === 'XL') return 1.16
  return 1
}

type OverlayOptions = {
  color: string
  scale?: number
  garmentImage?: CanvasImageSource | null
  opacity?: number
}

/**
 * Draw a clearly visible torso garment locked to pose landmarks.
 * Prefer garment image when available; always paint a solid silhouette underneath.
 */
export function drawGarmentOverlay(
  context: CanvasRenderingContext2D,
  landmarks: MappedLandmark[],
  colorOrOptions: string | OverlayOptions,
  legacyScale = 1,
) {
  const options: OverlayOptions =
    typeof colorOrOptions === 'string'
      ? { color: colorOrOptions, scale: legacyScale }
      : colorOrOptions
  const color = options.color
  const scale = options.scale ?? 1
  const opacity = options.opacity ?? 0.82

  const ls = landmarks[11]
  const rs = landmarks[12]
  const lh = landmarks[23]
  const rh = landmarks[24]
  const le = landmarks[13]
  const re = landmarks[14]
  if (!ls || !rs || !lh || !rh) return

  const midShoulderX = (ls.x + rs.x) / 2
  const midShoulderY = (ls.y + rs.y) / 2
  const midHipX = (lh.x + rh.x) / 2
  const midHipY = (lh.y + rh.y) / 2
  const shoulderW = Math.hypot(rs.x - ls.x, rs.y - ls.y) * scale
  if (shoulderW < 8) return

  const halfChest = shoulderW * 0.62
  const halfHem = shoulderW * 0.55
  const neckY = midShoulderY - shoulderW * 0.12
  const hemY = midHipY + shoulderW * 0.08

  context.save()
  context.globalAlpha = opacity

  // Torso body
  context.beginPath()
  context.moveTo(midShoulderX - halfChest * 0.22, neckY)
  context.lineTo(midShoulderX + halfChest * 0.22, neckY)
  context.lineTo(rs.x + halfChest * 0.18, midShoulderY)
  context.lineTo(midHipX + halfHem, hemY)
  context.lineTo(midHipX - halfHem, hemY)
  context.lineTo(ls.x - halfChest * 0.18, midShoulderY)
  context.closePath()
  context.fillStyle = color
  context.fill()
  context.strokeStyle = 'rgba(20,32,42,0.35)'
  context.lineWidth = 2
  context.stroke()

  // Sleeves toward elbows when visible
  const drawSleeve = (shoulder: MappedLandmark, elbow: MappedLandmark | undefined, outward: number) => {
    if (!elbow || (elbow.visibility ?? 0) < 0.2) return
    const width = shoulderW * 0.22
    context.beginPath()
    context.moveTo(shoulder.x, shoulder.y - width * 0.35)
    context.lineTo(shoulder.x + outward * width * 0.4, shoulder.y + width * 0.2)
    context.lineTo(elbow.x + outward * width * 0.35, elbow.y)
    context.lineTo(elbow.x - outward * width * 0.2, elbow.y + width * 0.15)
    context.lineTo(shoulder.x - outward * width * 0.15, shoulder.y + width * 0.55)
    context.closePath()
    context.fillStyle = color
    context.fill()
  }
  drawSleeve(ls, le, -1)
  drawSleeve(rs, re, 1)

  // Garment product image composited over torso
  const garment = options.garmentImage
  if (garment) {
    const boxW = halfChest * 2.05
    const boxH = Math.max(hemY - neckY, shoulderW * 1.35)
    const gx = midShoulderX - boxW / 2
    const gy = neckY - shoulderW * 0.05
    context.globalAlpha = Math.min(1, opacity + 0.08)
    try {
      context.drawImage(garment, gx, gy, boxW, boxH)
    } catch {
      /* cross-origin / decode issues — silhouette remains */
    }
  }

  context.restore()
}

/** Fallback when pose is unavailable — centered garment plate so clothing is still visible. */
export function drawCenteredGarmentFallback(
  context: CanvasRenderingContext2D,
  destWidth: number,
  destHeight: number,
  color: string,
  garmentImage?: CanvasImageSource | null,
) {
  const w = destWidth * 0.42
  const h = destHeight * 0.48
  const x = (destWidth - w) / 2
  const y = destHeight * 0.22
  context.save()
  context.globalAlpha = 0.78
  context.fillStyle = color
  context.beginPath()
  context.moveTo(x + w * 0.28, y)
  context.lineTo(x + w * 0.72, y)
  context.lineTo(x + w, y + h * 0.18)
  context.lineTo(x + w * 0.92, y + h)
  context.lineTo(x + w * 0.08, y + h)
  context.lineTo(x, y + h * 0.18)
  context.closePath()
  context.fill()
  if (garmentImage) {
    try {
      context.drawImage(garmentImage, x, y, w, h)
    } catch {
      /* ignore */
    }
  }
  context.restore()
}
