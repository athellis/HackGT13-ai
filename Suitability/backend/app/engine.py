from collections.abc import Mapping, Sequence
from typing import Any

from .catalog import ENVIRONMENTS

FIT_EASE = {
    "fitted": 0.04,
    "regular": 0.10,
    "relaxed": 0.16,
    "oversized": 0.24,
}

AREAS = ("chest", "waist", "shoulders", "sleeves")


def _area_score(body_measure: float, garment_measure: float, target_ease: float) -> int:
    if body_measure <= 0 or garment_measure <= 0:
        return 50
    ease = garment_measure / body_measure - 1
    distance = abs(ease - target_ease)
    return round(max(0, 100 * (1 - distance / 0.28)))


def score_body_fit(
    body: Mapping[str, float], garment: Mapping[str, float], preference: str
) -> tuple[int, dict[str, int]]:
    target_ease = FIT_EASE.get(preference, FIT_EASE["regular"])
    areas: dict[str, int] = {}
    for area in AREAS:
        body_value = body.get(area)
        garment_value = garment.get(area)
        if body_value is None or garment_value is None:
            continue
        ease = target_ease * 0.45 if area == "sleeves" else target_ease
        areas[area] = _area_score(float(body_value), float(garment_value), ease)
    if not areas:
        return 50, {}
    return round(sum(areas.values()) / len(areas)), areas


def score_style(product: Mapping[str, Any], preferences: Sequence[str]) -> int:
    product_tags = {tag.casefold() for tag in product.get("style_tags", [])}
    preference_tags = {tag.casefold() for tag in preferences}
    if not preference_tags:
        return 75
    if not product_tags:
        return 50
    overlap = len(product_tags & preference_tags)
    return round(55 + 45 * overlap / len(preference_tags))


def score_event(product: Mapping[str, Any], event: Mapping[str, Any]) -> int:
    checks = (
        ("formality", "formality"),
        ("weather", "weather_tags"),
        ("activity", "activity_tags"),
    )
    scores: list[int] = []
    for event_key, product_key in checks:
        expected = event.get(event_key)
        if not expected:
            continue
        available = product.get(product_key, [])
        if isinstance(available, str):
            available = [available]
        values = {value.casefold() for value in available}
        scores.append(100 if str(expected).casefold() in values else 35)
    return round(sum(scores) / len(scores)) if scores else 75


def score_environment(product: Mapping[str, Any], scene_id: str) -> tuple[int, dict[str, int]]:
    environment = ENVIRONMENTS.get(scene_id) or ENVIRONMENTS["city-evening"]
    props = product.get("env_props") or {}
    needs = environment.get("needs") or {}
    area_scores: dict[str, int] = {}
    for key, needed in needs.items():
        actual = int(props.get(key, 40))
        distance = abs(actual - int(needed))
        area_scores[key] = round(max(0, 100 - distance * 1.1))
    temp = environment.get("temp_f")
    tmin = props.get("temperature_min")
    tmax = props.get("temperature_max")
    if temp is not None and tmin is not None and tmax is not None:
        if tmin <= temp <= tmax:
            area_scores["temperature"] = 95
        else:
            overshoot = min(abs(temp - tmin), abs(temp - tmax))
            area_scores["temperature"] = round(max(20, 95 - overshoot * 3))
    if not area_scores:
        return 70, {}
    return round(sum(area_scores.values()) / len(area_scores)), area_scores


def compare_to_reference(
    product: Mapping[str, Any],
    size_label: str,
    reference: Mapping[str, Any] | None,
) -> dict[str, Any] | None:
    if not reference:
        return None
    size = next((entry for entry in product.get("sizes", []) if entry["label"] == size_label), None)
    if size is None:
        return None
    ref_measures = reference.get("measurements") or {}
    garment = size.get("measurements") or {}
    deltas: dict[str, float] = {}
    notes: list[str] = []
    for area in ("chest", "waist", "shoulders", "sleeves"):
        if area not in garment or area not in ref_measures:
            continue
        delta = round(float(garment[area]) - float(ref_measures[area]), 1)
        deltas[area] = delta
        if abs(delta) < 0.4:
            notes.append(f"{area.capitalize()} matches your {reference.get('label', 'reference')} closely.")
        elif delta > 0:
            notes.append(f"{area.capitalize()} runs about {delta}\" larger than your reference.")
        else:
            notes.append(f"{area.capitalize()} runs about {abs(delta)}\" smaller than your reference.")
    brand = product.get("brand", "This brand")
    summary = (
        f"{brand} {size_label} vs your {reference.get('brand', '')} "
        f"{reference.get('name', 'favorite')} ({reference.get('label', '')})."
    ).strip()
    return {"summary": summary, "deltas": deltas, "notes": notes[:4]}


def recommend(
    products: Sequence[Mapping[str, Any]],
    body: Mapping[str, float],
    event: Mapping[str, Any],
    fit_preference: str,
    style_preferences: Sequence[str],
    reference: Mapping[str, Any] | None = None,
    max_price: float | None = None,
) -> list[dict[str, Any]]:
    results = []
    scene_id = str(event.get("scene") or "city-evening")
    for product in products:
        if max_price is not None and float(product.get("price", 0)) > max_price:
            continue
        best_size = None
        best_body_fit = -1
        best_areas: dict[str, int] = {}
        size_fits: dict[str, dict[str, Any]] = {}
        for size in product.get("sizes", []):
            body_fit, areas = score_body_fit(body, size.get("measurements", {}), fit_preference)
            size_fits[size["label"]] = {"body_fit": body_fit, "areas": areas}
            if body_fit > best_body_fit:
                best_size, best_body_fit, best_areas = size, body_fit, areas
        if best_size is None:
            continue

        style_match = score_style(product, style_preferences)
        event_match = score_event(product, event)
        env_match, env_areas = score_environment(product, scene_id)
        overall = round(
            0.4 * best_body_fit + 0.2 * style_match + 0.2 * event_match + 0.2 * env_match
        )
        cross_brand = compare_to_reference(product, best_size["label"], reference)
        results.append(
            {
                "product": dict(product),
                "body_fit": best_body_fit,
                "style_match": style_match,
                "event_match": event_match,
                "env_match": env_match,
                "env_areas": env_areas,
                "overall": overall,
                "recommended_size": best_size["label"],
                "areas": best_areas,
                "size_fits": size_fits,
                "cross_brand": cross_brand,
                "reasons": _explain(
                    best_body_fit, style_match, event_match, env_match, best_areas, env_areas
                ),
            }
        )
    return sorted(results, key=lambda result: result["overall"], reverse=True)


def _explain(
    body_fit: int,
    style_match: int,
    event_match: int,
    env_match: int,
    areas: Mapping[str, int],
    env_areas: Mapping[str, int],
) -> list[str]:
    strongest_area = max(areas, key=areas.get) if areas else "fit"
    reasons = [f"Strongest measurement match is at the {strongest_area}."]
    if body_fit < 65:
        reasons.append("Check the garment measurements before choosing a size.")
    if style_match >= 80:
        reasons.append("Matches several of your selected style preferences.")
    if event_match >= 80:
        reasons.append("Its event attributes suit the setting you described.")
    if env_match >= 80:
        strongest_env = max(env_areas, key=env_areas.get) if env_areas else "conditions"
        reasons.append(f"Strong environmental readiness for {strongest_env.replace('_', ' ')}.")
    elif env_match < 55:
        weakest = min(env_areas, key=env_areas.get) if env_areas else "conditions"
        reasons.append(f"May underperform on {weakest.replace('_', ' ')} for this setting.")
    return reasons


def parse_assistant_intent(message: str) -> dict[str, Any]:
    text = message.casefold()
    intent: dict[str, Any] = {"action": "clarify", "filters": {}}

    if any(word in text for word in ("compare", "versus", "vs", "difference")):
        intent["action"] = "compare"
    elif any(word in text for word in ("why", "explain", "reason")):
        intent["action"] = "explain"
    elif any(word in text for word in ("size", "oversized", "fitted", "relaxed", "larger", "smaller")):
        intent["action"] = "size_advice"
        if "oversized" in text or "larger" in text or "looser" in text:
            intent["filters"]["fit_preference"] = "oversized" if "oversized" in text else "relaxed"
        elif "fitted" in text or "smaller" in text or "tighter" in text:
            intent["filters"]["fit_preference"] = "fitted"
    elif any(word in text for word in ("casual", "formal", "less formal", "dressier")):
        intent["action"] = "restyle"
        if "less formal" in text or "casual" in text:
            intent["filters"]["formality"] = "casual"
        elif "formal" in text or "dressier" in text:
            intent["filters"]["formality"] = "formal"
    elif any(word in text for word in ("hot", "warm", "cold", "rain", "weather")):
        intent["action"] = "weather"
        if "hot" in text or "warm" in text:
            intent["filters"]["weather"] = "warm"
        elif "cold" in text:
            intent["filters"]["weather"] = "cold"
        elif "rain" in text:
            intent["filters"]["weather"] = "rain"
    elif any(word in text for word in ("find", "show", "recommend", "jacket", "shirt", "under")):
        intent["action"] = "search"

    if "under" in text or "$" in text:
        digits = "".join(ch if ch.isdigit() else " " for ch in text).split()
        if digits:
            intent["filters"]["max_price"] = float(digits[0])
    if "black" in text or "ink" in text:
        intent["filters"]["color"] = "black"
    if "jacket" in text:
        intent["filters"]["category"] = "jacket"
    if "shirt" in text or "blouse" in text or "polo" in text:
        intent["filters"]["category"] = "shirt"
    if "minimal" in text:
        intent["filters"]["style"] = "minimal"
    if "utility" in text:
        intent["filters"]["style"] = "utility"
    return intent


def answer_assistant(
    message: str,
    recommendations: Sequence[Mapping[str, Any]],
    event: Mapping[str, Any],
    fit_preference: str,
) -> dict[str, Any]:
    intent = parse_assistant_intent(message)
    filters = intent.get("filters") or {}
    items = list(recommendations)

    if category := filters.get("category"):
        items = [item for item in items if item["product"].get("category") == category]
    if max_price := filters.get("max_price"):
        items = [item for item in items if float(item["product"].get("price", 0)) <= max_price]
    if color := filters.get("color"):
        items = [
            item
            for item in items
            if color in str(item["product"].get("color", "")).casefold()
            or color in str(item["product"].get("overlay_color", "")).casefold()
            or (color == "black" and "ink" in str(item["product"].get("color", "")).casefold())
        ]
    if style := filters.get("style"):
        items = [
            item
            for item in items
            if style in {tag.casefold() for tag in item["product"].get("style_tags", [])}
        ]
    if formality := filters.get("formality"):
        items = [
            item
            for item in items
            if formality in {value.casefold() for value in item["product"].get("formality", [])}
        ]
    if weather := filters.get("weather"):
        weather_key = "mild" if weather == "rain" else weather
        items = [
            item
            for item in items
            if weather_key in {value.casefold() for value in item["product"].get("weather_tags", [])}
            or (
                weather == "rain"
                and int((item["product"].get("env_props") or {}).get("waterproof", 0)) >= 50
            )
        ]

    action = intent["action"]
    product_ids = [item["product"]["id"] for item in items[:3]]

    if action == "compare" and len(recommendations) >= 2:
        left, right = recommendations[0], recommendations[1]
        reply = (
            f"{left['product']['name']} leads on body/event fit "
            f"({left['body_fit']}% / {left['event_match']}%), while "
            f"{right['product']['name']} scores {right['style_match']}% on style "
            f"at ${right['product']['price']}."
        )
        return {
            "reply": reply,
            "action": action,
            "highlight_ids": [left["product"]["id"], right["product"]["id"]],
            "suggested_fit": fit_preference,
        }

    if action == "explain" and recommendations:
        top = recommendations[0]
        reply = (
            f"{top['product']['name']} is ranked first because body fit is {top['body_fit']}%, "
            f"style {top['style_match']}%, event {top['event_match']}%, and environment "
            f"{top.get('env_match', 70)}% for {event.get('scene_label', 'your setting')}. "
            f"{' '.join(top.get('reasons', [])[:2])}"
        )
        return {
            "reply": reply,
            "action": action,
            "highlight_ids": [top["product"]["id"]],
            "suggested_fit": fit_preference,
        }

    if action == "size_advice" and recommendations:
        top = recommendations[0]
        suggested = filters.get("fit_preference", fit_preference)
        size = top["recommended_size"]
        reply = (
            f"For a {suggested} look on {top['product']['name']}, start with size {size}. "
            f"Area scores: "
            + ", ".join(f"{area} {score}%" for area, score in list(top.get("areas", {}).items())[:3])
            + "."
        )
        return {
            "reply": reply,
            "action": action,
            "highlight_ids": [top["product"]["id"]],
            "suggested_fit": suggested,
        }

    if items:
        names = ", ".join(item["product"]["name"] for item in items[:3])
        reply = f"Here is what fits your ask for {event.get('scene_label', 'this event')}: {names}."
        return {
            "reply": reply,
            "action": action,
            "highlight_ids": product_ids,
            "suggested_fit": filters.get("fit_preference", fit_preference),
        }

    return {
        "reply": (
            "I could not match that filter in the current edit. "
            "Try asking for a jacket under $150, something more casual, or a warmer-weather option."
        ),
        "action": "clarify",
        "highlight_ids": [item["product"]["id"] for item in recommendations[:2]],
        "suggested_fit": fit_preference,
    }
