from uuid import uuid4

from dotenv import load_dotenv
from fastapi import FastAPI, File, HTTPException, UploadFile
from fastapi.middleware.cors import CORSMiddleware

from .catalog import CATALOG, ENVIRONMENTS
from .engine import answer_assistant, recommend
from .schemas import AssistantRequest, CheckoutRequest, RecommendationRequest, SwarmSearchRequest, TryOnRequest
from .swarm import run_swarm
from .tryon import generate_tryon
from .whisper_service import transcribe_bytes

load_dotenv()

app = FastAPI(title="Suitability Fit API", version="0.3.0")
app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://localhost:5173", "http://127.0.0.1:5173", "http://localhost:5174", "http://127.0.0.1:5174"],
    allow_credentials=False,
    allow_methods=["GET", "POST"],
    allow_headers=["Content-Type"],
)


def parse_event(description: str) -> dict[str, str]:
    text = description.casefold()
    weather = "cold" if any(word in text for word in ("winter", "snow", "cold")) else "warm"
    if any(word in text for word in ("rain", "wet", "storm")):
        weather = "mild"
    if any(word in text for word in ("wedding", "gala", "black tie")):
        formality = "formal"
    elif any(word in text for word in ("hike", "beach", "pool", "jog", "picnic", "casual")):
        formality = "casual"
    else:
        formality = "smart casual"
    if any(word in text for word in ("hike", "trail", "mountain")):
        activity = "outdoors"
    elif any(word in text for word in ("beach", "pool", "swim", "surf")):
        activity = "daytime"
    elif any(word in text for word in ("office", "work", "meeting")):
        activity = "work"
    elif any(word in text for word in ("dinner", "restaurant", "birthday", "wedding", "bar", "rooftop")):
        activity = "dinner"
    else:
        activity = "daytime"

    location = ""
    if "greece" in text:
        location = "Greece"
    elif any(word in text for word in ("mountain", "trail", "hike")):
        location = "Mountains"
    elif any(word in text for word in ("rooftop", "city", "nyc", "downtown")):
        location = "City"
    elif any(word in text for word in ("beach", "pool", "coast")):
        location = "Coast"

    if "winter" in text or "snow" in text:
        scene = "winter-city"
    elif "pool" in text:
        scene = "pool-party"
    elif any(word in text for word in ("rooftop", "bar")):
        scene = "rooftop-night"
    elif any(word in text for word in ("office", "work", "meeting")):
        scene = "office-day"
    elif location == "Greece" or ("dinner" in text and "beach" not in text):
        scene = "coastal-evening" if location == "Greece" else "city-evening"
    elif activity == "outdoors":
        scene = "mountain-trail"
    elif "beach" in text:
        scene = "coast-daylight"
    else:
        scene = "city-evening"

    environment = ENVIRONMENTS.get(scene, ENVIRONMENTS["city-evening"])
    return {
        "description": description,
        "location": location,
        "weather": weather,
        "formality": formality,
        "activity": activity,
        "scene": scene,
        "scene_label": environment["label"],
        "scene_image": environment["image"],
        "temp_f": str(environment["temp_f"]),
    }


def _eligible_products(request: RecommendationRequest | AssistantRequest | SwarmSearchRequest) -> list[dict]:
    return [
        product
        for product in CATALOG
        if request.audience == "all" or product["audience"] == request.audience
    ]


def _body_payload(body) -> dict[str, float]:
    payload = body.model_dump(exclude_none=True)
    if "sleeves" not in payload and "shoulders" in payload:
        payload["sleeves"] = round(float(payload["shoulders"]) + 7.5, 1)
    return payload


@app.get("/api/health")
def health() -> dict[str, str]:
    return {"status": "ok"}


@app.get("/api/products")
def products() -> list[dict]:
    return CATALOG


@app.get("/api/environments")
def environments() -> list[dict]:
    return list(ENVIRONMENTS.values())


@app.post("/api/recommendations")
def recommendations(request: RecommendationRequest) -> dict:
    event = parse_event(request.event_description)
    reference = request.reference.model_dump() if request.reference else None
    ranked = recommend(
        _eligible_products(request),
        _body_payload(request.body),
        event,
        request.fit_preference,
        request.style_preferences,
        reference=reference,
        max_price=request.max_price,
    )
    return {
        "event": event,
        "environment": ENVIRONMENTS.get(event["scene"]),
        "recommendations": ranked,
    }


@app.post("/api/assistant")
def assistant(request: AssistantRequest) -> dict:
    event = parse_event(request.event_description)
    reference = request.reference.model_dump() if request.reference else None
    ranked = recommend(
        _eligible_products(request),
        _body_payload(request.body),
        event,
        request.fit_preference,
        request.style_preferences,
        reference=reference,
        max_price=request.max_price,
    )
    answer = answer_assistant(request.message, ranked, event, request.fit_preference)
    highlighted = [
        item for item in ranked if item["product"]["id"] in set(answer["highlight_ids"])
    ] or ranked[:2]
    return {
        "event": event,
        "environment": ENVIRONMENTS.get(event["scene"]),
        "reply": answer["reply"],
        "action": answer["action"],
        "suggested_fit": answer["suggested_fit"],
        "highlight_ids": answer["highlight_ids"],
        "recommendations": ranked,
        "highlighted": highlighted,
    }


@app.post("/api/swarm-search")
def swarm_search(request: SwarmSearchRequest) -> dict:
    event = parse_event(request.event_description)
    reference = request.reference.model_dump() if request.reference else None
    result = run_swarm(
        request.query,
        _eligible_products(request),
        _body_payload(request.body),
        event,
        request.fit_preference,
        request.style_preferences,
        reference=reference,
        max_price=request.max_price,
    )
    return {
        "event": event,
        "environment": ENVIRONMENTS.get(event["scene"]),
        **result,
    }


@app.post("/api/transcribe")
async def transcribe(file: UploadFile = File(...)) -> dict:
    data = await file.read()
    if not data:
        raise HTTPException(status_code=400, detail="Empty audio upload")
    return transcribe_bytes(data, filename=file.filename or "audio.webm")


@app.post("/api/tryon")
def tryon(request: TryOnRequest) -> dict:
    product = next((entry for entry in CATALOG if entry["id"] == request.product_id), None)
    garment_url = request.garment_image_url or (product or {}).get("image")
    color = request.garment_color or (product or {}).get("overlay_color", "#6b7c85")
    name = request.garment_name or (product or {}).get("name", "garment")
    return generate_tryon(
        request.person_image_base64,
        garment_image_url=garment_url,
        garment_color=color,
        garment_name=name,
        size=request.size or "M",
        product_id=request.product_id,
    )


@app.post("/api/checkout")
def checkout(request: CheckoutRequest) -> dict:
    total = 0
    for item in request.items:
        product = next((entry for entry in CATALOG if entry["id"] == item.product_id), None)
        if product is None:
            raise HTTPException(status_code=404, detail=f"Unknown product: {item.product_id}")
        valid_sizes = {size["label"] for size in product["sizes"]}
        if item.size not in valid_sizes:
            raise HTTPException(status_code=400, detail=f"Unavailable size: {item.size}")
        total += product["price"] * item.quantity
    return {
        "order_id": f"SUIT-{uuid4().hex[:8].upper()}",
        "status": "confirmed",
        "total": total,
        "payment": "simulated_visa",
    }
