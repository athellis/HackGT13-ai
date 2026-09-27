"""Multi-agent clothing search swarm — parallel roles over the catalog (+ optional LLM)."""

from __future__ import annotations

import os
from collections.abc import Mapping, Sequence
from concurrent.futures import ThreadPoolExecutor, as_completed
from typing import Any

from .engine import parse_assistant_intent, recommend, score_event, score_style


def _ollama_available() -> bool:
    return bool(os.getenv("OLLAMA_HOST") or os.getenv("OLLAMA_MODEL"))


def _optional_llm_rerank(query: str, candidates: Sequence[Mapping[str, Any]]) -> list[str] | None:
    host = os.getenv("OLLAMA_HOST", "").rstrip("/")
    model = os.getenv("OLLAMA_MODEL", "llama3.2")
    if not host:
        return None
    try:
        import httpx

        names = ", ".join(
            f"{item['product']['id']}:{item['product']['name']}" for item in candidates[:8]
        )
        prompt = (
            f"User wants: {query}\nCatalog candidates: {names}\n"
            "Reply with a comma-separated list of product ids best-first. No other text."
        )
        response = httpx.post(
            f"{host}/api/generate",
            json={"model": model, "prompt": prompt, "stream": False},
            timeout=8.0,
        )
        if response.status_code != 200:
            return None
        text = str(response.json().get("response") or "")
        ids = [part.strip() for part in text.replace("\n", ",").split(",") if part.strip()]
        valid = {item["product"]["id"] for item in candidates}
        ordered = [pid for pid in ids if pid in valid]
        return ordered or None
    except Exception:
        return None


def _query_tokens(query: str) -> list[str]:
    return [token for token in query.casefold().split() if len(token) > 3]


def _intent_boost(product: Mapping[str, Any], filters: Mapping[str, Any], tokens: Sequence[str]) -> float:
    boost = 0.0
    category = filters.get("category")
    if category and product.get("category") == category:
        boost += 35
    elif category:
        boost -= 20

    weather = filters.get("weather")
    if weather:
        weather_key = "mild" if weather == "rain" else weather
        tags = {str(tag).casefold() for tag in product.get("weather_tags", [])}
        if weather_key in tags:
            boost += 28
        else:
            boost -= 15
        if weather == "rain" and int((product.get("env_props") or {}).get("waterproof", 0)) >= 50:
            boost += 12

    color = filters.get("color")
    if color:
        color_blob = f"{product.get('color', '')} {product.get('overlay_color', '')}".casefold()
        if color in color_blob or (color == "black" and "ink" in color_blob):
            boost += 12

    hay = " ".join([
        str(product.get("name", "")),
        str(product.get("brand", "")),
        str(product.get("category", "")),
        " ".join(product.get("style_tags", [])),
        " ".join(product.get("weather_tags", [])),
        str(product.get("material", "")),
    ]).casefold()
    boost += sum(6 for token in tokens if token in hay)
    return boost


def agent_style_matcher(
    query: str,
    products: Sequence[Mapping[str, Any]],
    style_preferences: Sequence[str],
    filters: Mapping[str, Any],
) -> dict[str, Any]:
    wanted = list(style_preferences)
    if style := filters.get("style"):
        wanted.append(str(style))
    text = query.casefold()
    for tag in ("minimal", "coastal", "modern", "tailored", "utility", "polished", "relaxed"):
        if tag in text and tag not in wanted:
            wanted.append(tag)
    tokens = _query_tokens(query)
    scored = []
    for product in products:
        score = score_style(product, wanted) + _intent_boost(product, filters, tokens)
        scored.append({"product_id": product["id"], "score": max(0, min(100, round(score)))})
    scored.sort(key=lambda row: row["score"], reverse=True)
    label = filters.get("category") or filters.get("style") or (", ".join(wanted[:3]) if wanted else "catalog defaults")
    return {
        "agent": "style_matcher",
        "note": f"Style matcher weighted {label}.",
        "votes": scored[:8],
    }


def agent_occasion_matcher(
    query: str,
    products: Sequence[Mapping[str, Any]],
    event: Mapping[str, Any],
    filters: Mapping[str, Any],
) -> dict[str, Any]:
    event_overlay = dict(event)
    if formality := filters.get("formality"):
        event_overlay["formality"] = formality
    if weather := filters.get("weather"):
        event_overlay["weather"] = "mild" if weather == "rain" else weather
    tokens = _query_tokens(query)
    scored = []
    for product in products:
        score = score_event(product, event_overlay) + _intent_boost(product, filters, tokens)
        scored.append({"product_id": product["id"], "score": max(0, min(100, round(score)))})
    scored.sort(key=lambda row: row["score"], reverse=True)
    weather_note = event_overlay.get("weather", "mild")
    return {
        "agent": "occasion_matcher",
        "note": (
            f"Occasion matcher for {event_overlay.get('formality', 'smart casual')}"
            f" · {weather_note} weather."
        ),
        "votes": scored[:8],
    }


def agent_budget_filter(query: str, products: Sequence[Mapping[str, Any]], max_price: float | None, filters: Mapping[str, Any]) -> dict[str, Any]:
    ceiling = max_price
    if parsed := filters.get("max_price"):
        ceiling = float(parsed)
    tokens = _query_tokens(query)
    scored = []
    for product in products:
        price = float(product.get("price", 0))
        if ceiling is not None and price > ceiling:
            continue
        if ceiling:
            score = round(100 * (1 - price / (ceiling * 1.15)))
        else:
            score = round(max(40, 100 - price / 3))
        score += _intent_boost(product, filters, tokens)
        scored.append({"product_id": product["id"], "score": max(0, min(100, round(score)))})
    scored.sort(key=lambda row: row["score"], reverse=True)
    note = (
        f"Budget filter kept pieces at or under ${int(ceiling)}."
        if ceiling
        else "Budget filter ranked by relative value (no ceiling set)."
    )
    return {"agent": "budget_filter", "note": note, "votes": scored[:8]}


def agent_brand_scout(query: str, products: Sequence[Mapping[str, Any]], filters: Mapping[str, Any]) -> dict[str, Any]:
    category = filters.get("category")
    color = filters.get("color")
    weather = filters.get("weather")
    tokens = _query_tokens(query)
    scored = []
    for product in products:
        score = 50 + _intent_boost(product, filters, tokens)
        scored.append({"product_id": product["id"], "score": max(0, min(100, round(score)))})
    scored.sort(key=lambda row: row["score"], reverse=True)
    bits = []
    if category:
        bits.append(f"{category}s")
    if weather:
        bits.append(f"{weather} weather")
    if color:
        bits.append(color)
    note = (
        f"Brand scout prioritized {', '.join(bits)}."
        if bits
        else "Brand scout scanned names, categories, and color cues in the catalog."
    )
    return {"agent": "brand_scout", "note": note, "votes": scored[:8]}


def run_swarm(
    query: str,
    products: Sequence[Mapping[str, Any]],
    body: Mapping[str, float],
    event: Mapping[str, Any],
    fit_preference: str,
    style_preferences: Sequence[str],
    reference: Mapping[str, Any] | None = None,
    max_price: float | None = None,
) -> dict[str, Any]:
    intent = parse_assistant_intent(query)
    filters = dict(intent.get("filters") or {})
    event_for_fit = dict(event)
    if weather := filters.get("weather"):
        event_for_fit["weather"] = "mild" if weather == "rain" else weather
    if formality := filters.get("formality"):
        event_for_fit["formality"] = formality
    if filters.get("max_price") is not None:
        max_price = float(filters["max_price"])

    agents = (
        lambda: agent_style_matcher(query, products, style_preferences, filters),
        lambda: agent_occasion_matcher(query, products, event_for_fit, filters),
        lambda: agent_budget_filter(query, products, max_price, filters),
        lambda: agent_brand_scout(query, products, filters),
    )
    reports: list[dict[str, Any]] = []
    with ThreadPoolExecutor(max_workers=4) as pool:
        futures = [pool.submit(fn) for fn in agents]
        for future in as_completed(futures):
            reports.append(future.result())

    tally: dict[str, list[float]] = {}
    for report in reports:
        for vote in report["votes"]:
            tally.setdefault(vote["product_id"], []).append(float(vote["score"]))
    merged_scores = {product_id: sum(scores) / len(scores) for product_id, scores in tally.items()}

    base = recommend(
        products, body, event_for_fit, fit_preference, style_preferences,
        reference=reference, max_price=max_price,
    )
    by_id = {item["product"]["id"]: item for item in base}
    tokens = _query_tokens(query)

    top_for_llm = sorted(
        [by_id[pid] for pid in merged_scores if pid in by_id],
        key=lambda item: merged_scores.get(item["product"]["id"], 0)
        + _intent_boost(item["product"], filters, tokens),
        reverse=True,
    )
    llm_order = _optional_llm_rerank(query, top_for_llm) if _ollama_available() else None

    ranked: list[dict[str, Any]] = []
    for item in base:
        pid = item["product"]["id"]
        swarm = merged_scores.get(pid, 50) + _intent_boost(item["product"], filters, tokens)
        blended = round(0.7 * min(100, swarm) + 0.3 * float(item["overall"]))
        enriched = dict(item)
        enriched["overall"] = max(0, min(100, blended))
        enriched["agent_votes"] = [
            f"{report['agent']}:{next((v['score'] for v in report['votes'] if v['product_id'] == pid), 0)}"
            for report in reports
        ]
        ranked.append(enriched)

    if llm_order:
        order_index = {pid: idx for idx, pid in enumerate(llm_order)}
        ranked.sort(key=lambda item: (order_index.get(item["product"]["id"], 99), -item["overall"]))
    else:
        ranked.sort(key=lambda item: item["overall"], reverse=True)

    # Hard preference: matching category/weather rise to the top when query asks for them.
    if filters.get("category") or filters.get("weather"):
        def _match_key(item: Mapping[str, Any]) -> tuple[int, int, float]:
            product = item["product"]
            cat_ok = 1 if not filters.get("category") or product.get("category") == filters.get("category") else 0
            weather = filters.get("weather")
            weather_ok = 1
            if weather:
                weather_key = "mild" if weather == "rain" else weather
                tags = {str(tag).casefold() for tag in product.get("weather_tags", [])}
                weather_ok = 1 if weather_key in tags else 0
            return (cat_ok, weather_ok, float(item["overall"]))

        ranked.sort(key=_match_key, reverse=True)

    notes = [report["note"] for report in reports]
    if filters.get("category") or filters.get("weather"):
        bits = [b for b in (filters.get("category"), filters.get("weather") and f"{filters['weather']} weather") if b]
        notes.append(f"Merged agent scores with query focus: {', '.join(bits)}.")
    elif llm_order:
        notes.append(f"Ollama ({os.getenv('OLLAMA_MODEL', 'llama3.2')}) re-ranked the shortlist.")
    else:
        notes.append("Merged parallel agent scores with body/event fit ranking.")

    return {
        "recommendations": ranked,
        "highlight_ids": [item["product"]["id"] for item in ranked[:3]],
        "agent_notes": notes,
        "query": query,
    }
