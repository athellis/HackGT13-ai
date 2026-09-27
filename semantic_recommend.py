import json
import os

import numpy as np
from ollama import chat
from pydantic import BaseModel
from sentence_transformers import SentenceTransformer
from sklearn.metrics.pairwise import cosine_similarity


# ============================================================
# SETTINGS
# ============================================================

CATALOG_FILE = "fashion_catalog.json"
EMBEDDINGS_FILE = "fashion_embeddings.npy"

OLLAMA_MODEL = "qwen2.5:3b"

ALLOWED_CATEGORIES = [
    "blouses",
    "cardigans",
    "denim",
    "dresses",
    "graphic",
    "jackets",
    "leggings",
    "pants",
    "rompers",
    "shirts",
    "shorts",
    "skirts",
    "suiting",
    "sweaters",
    "sweatshirts",
    "tees"
]


# ============================================================
# STRUCTURED OUTPUT MODELS
# ============================================================

class SearchProfile(BaseModel):
    search_text: str
    preferred_categories: list[str]
    avoid_categories: list[str]
    reasoning_keywords: list[str]


class RankedCandidates(BaseModel):
    ranked_ids: list[str]


# ============================================================
# LOAD CATALOG
# ============================================================

with open(CATALOG_FILE, "r") as f:
    catalog = json.load(f)


# ============================================================
# LOAD SEMANTIC MODEL
# ============================================================

print("Loading semantic model...")

model = SentenceTransformer(
    "all-MiniLM-L6-v2"
)


# ============================================================
# BUILD SEARCHABLE TEXT FOR EACH CLOTHING ITEM
# ============================================================

def build_item_text(item):
    parts = []

    if item.get("gender"):
        parts.append(
            f"gender {item['gender']}"
        )

    if item.get("category"):
        parts.append(
            f"category {item['category']}"
        )

    if item.get("material"):
        parts.append(
            f"material {item['material']}"
        )

    if item.get("pattern"):
        parts.append(
            f"pattern {item['pattern']}"
        )

    if item.get("length"):
        parts.append(
            f"length {item['length']}"
        )

    if item.get("description"):
        parts.append(
            item["description"]
        )

    return ". ".join(parts)


item_texts = [
    build_item_text(item)
    for item in catalog
]


# ============================================================
# BUILD / LOAD CACHED EMBEDDINGS
# ============================================================

if os.path.exists(EMBEDDINGS_FILE):

    print("Loading saved clothing embeddings...")

    item_embeddings = np.load(
        EMBEDDINGS_FILE
    )

    if len(item_embeddings) != len(catalog):

        print(
            "Catalog changed. Rebuilding embeddings..."
        )

        item_embeddings = model.encode(
            item_texts,
            show_progress_bar=True,
            convert_to_numpy=True,
            normalize_embeddings=True
        )

        np.save(
            EMBEDDINGS_FILE,
            item_embeddings
        )

else:

    print(
        "Building clothing embeddings "
        "(only needed the first time)..."
    )

    item_embeddings = model.encode(
        item_texts,
        show_progress_bar=True,
        convert_to_numpy=True,
        normalize_embeddings=True
    )

    np.save(
        EMBEDDINGS_FILE,
        item_embeddings
    )

    print(
        f"Saved embeddings to {EMBEDDINGS_FILE}"
    )


# ============================================================
# CREATE SEARCH PROFILE
# ============================================================

def create_search_profile(
    gender=None,
    occasion=None,
    weather=None,
    formality=None,
    style_keywords=None,
    comfort_preferences=None,
    location=None,
    month=None
):

    context = {
        "gender": gender,
        "occasion": occasion,
        "weather": weather,
        "formality": formality,
        "style_keywords": style_keywords or [],
        "comfort_preferences":
            comfort_preferences or [],
        "location": location,
        "month": month
    }

    prompt = f"""
You are creating a clothing search profile for a fashion recommendation system.

SHOPPER CONTEXT:

{json.dumps(context, indent=2)}

AVAILABLE CLOTHING CATEGORIES:

{json.dumps(ALLOWED_CATEGORIES)}

Translate the shopper's intent into actual clothing characteristics.

IMPORTANT RULES:

- If the shopper wants to "look hot", interpret "hot" as attractive,
  sexy, stylish, or flattering.
- Do NOT interpret aesthetic words like "hot" or "cool" as weather.
- Do not invent gender if gender is null.
- If weather is null, do not invent weather requirements.
- If location is null, do not invent a destination.
- preferred_categories must ONLY contain values from AVAILABLE CLOTHING CATEGORIES.
- avoid_categories must ONLY contain values from AVAILABLE CLOTHING CATEGORIES.
- Do not put the same category in both preferred_categories and avoid_categories.
- Do not include duplicate categories.
- Do not include duplicate reasoning keywords.
- Do not over-restrict the result to one category unless absolutely necessary.
- search_text should describe actual clothing characteristics:
  silhouette, sleeve style, material, layering, fit, dressiness,
  aesthetic, and similar useful clothing properties.
- Do not simply repeat the event sentence.

Return a useful search profile.
"""

    response = chat(
        model=OLLAMA_MODEL,
        messages=[
            {
                "role": "user",
                "content": prompt
            }
        ],
        format=SearchProfile.model_json_schema(),
        options={
            "temperature": 0
        }
    )

    profile = SearchProfile.model_validate_json(
        response.message.content
    )

    profile_dict = profile.model_dump()

    # ========================================================
    # CLEAN PREFERRED CATEGORIES
    # ========================================================

    preferred = []

    for category in profile_dict[
        "preferred_categories"
    ]:

        if category not in ALLOWED_CATEGORIES:
            continue

        if category not in preferred:
            preferred.append(category)


    # ========================================================
    # CLEAN AVOID CATEGORIES
    # ========================================================

    avoid = []

    for category in profile_dict[
        "avoid_categories"
    ]:

        if category not in ALLOWED_CATEGORIES:
            continue

        # Never allow preferred + avoided contradiction
        if category in preferred:
            continue

        if category not in avoid:
            avoid.append(category)


    # ========================================================
    # CLEAN REASONING KEYWORDS
    # ========================================================

    keywords = []

    for keyword in profile_dict[
        "reasoning_keywords"
    ]:

        normalized_keyword = keyword.strip()

        if (
            normalized_keyword
            and normalized_keyword not in keywords
        ):
            keywords.append(
                normalized_keyword
            )


    profile_dict[
        "preferred_categories"
    ] = preferred

    profile_dict[
        "avoid_categories"
    ] = avoid

    profile_dict[
        "reasoning_keywords"
    ] = keywords

    return profile_dict


# ============================================================
# QWEN RERANKS ACTUAL CANDIDATES
# ============================================================

def rerank_candidates(
    candidates,
    search_profile,
    occasion=None,
    weather=None,
    formality=None,
    style_keywords=None,
    comfort_preferences=None,
    gender=None
):

    candidate_data = []

    for candidate in candidates:

        item = candidate["item"]

        description = (
            item.get("description") or ""
        )

        description = description[:350]

        candidate_data.append({
            "id": item["id"],
            "gender": item.get("gender"),
            "category": item.get("category"),
            "material": item.get("material"),
            "pattern": item.get("pattern"),
            "length": item.get("length"),
            "description": description
        })


    user_context = {
        "gender": gender,
        "occasion": occasion,
        "weather": weather,
        "formality": formality,
        "style_keywords":
            style_keywords or [],
        "comfort_preferences":
            comfort_preferences or []
    }


    prompt = f"""
You are ranking real clothing items for a shopper.

SHOPPER CONTEXT:

{json.dumps(user_context, indent=2)}

SEARCH PROFILE:

{json.dumps(search_profile, indent=2)}

CANDIDATES:

{json.dumps(candidate_data, indent=2)}

Rank these candidates from best to worst.

Consider:

- occasion
- desired aesthetic
- formality
- weather, only if supplied
- comfort preferences
- garment category
- material
- sleeve style
- fit / silhouette information
- garment description

IMPORTANT:

- If gender is null, do NOT penalize either men's or women's items.
- If gender is specified, only items matching that gender should rank well.
- Do not interpret aesthetic words like "hot" or "cool" as temperature.
- Do not invent facts that are not in the candidate description.
- Rank every candidate exactly once.
"""

    response = chat(
        model=OLLAMA_MODEL,
        messages=[
            {
                "role": "user",
                "content": prompt
            }
        ],
        format=RankedCandidates.model_json_schema(),
        options={
            "temperature": 0
        }
    )

    ranked = RankedCandidates.model_validate_json(
        response.message.content
    )

    lookup = {
        result["item"]["id"]: result
        for result in candidates
    }

    reranked = []

    for item_id in ranked.ranked_ids:

        if item_id in lookup:
            reranked.append(
                lookup[item_id]
            )

    # Add anything Qwen omitted
    existing_ids = {
        result["item"]["id"]
        for result in reranked
    }

    for result in candidates:

        if (
            result["item"]["id"]
            not in existing_ids
        ):
            reranked.append(
                result
            )

    return reranked


# ============================================================
# MAIN RECOMMENDER
# ============================================================

def recommend_semantic(
    original_text=None,
    gender=None,
    occasion=None,
    weather=None,
    formality=None,
    style_keywords=None,
    comfort_preferences=None,
    location=None,
    month=None,
    top_k=10
):

    # --------------------------------------------------------
    # STEP 1:
    # Create clothing-specific search profile
    # --------------------------------------------------------

    print(
        "\nCreating fashion search profile..."
    )

    profile = create_search_profile(
        gender=gender,
        occasion=occasion,
        weather=weather,
        formality=formality,
        style_keywords=style_keywords,
        comfort_preferences=comfort_preferences,
        location=location,
        month=month
    )


    print(
        "\nFASHION SEARCH PROFILE:"
    )

    print(
        json.dumps(
            profile,
            indent=2
        )
    )


    search_text = profile.get(
        "search_text",
        ""
    )

    preferred_categories = set(
        profile.get(
            "preferred_categories",
            []
        )
    )

    avoid_categories = set(
        profile.get(
            "avoid_categories",
            []
        )
    )


    # --------------------------------------------------------
    # STEP 2:
    # Embed clothing search description
    # --------------------------------------------------------

    query_embedding = model.encode(
        [search_text],
        convert_to_numpy=True,
        normalize_embeddings=True
    )


    similarities = cosine_similarity(
        query_embedding,
        item_embeddings
    )[0]


    # --------------------------------------------------------
    # STEP 3:
    # Score entire catalog
    # --------------------------------------------------------

    scored_results = []


    for i, item in enumerate(catalog):

        # Hard gender filter only when explicit
        if gender is not None:

            if item.get("gender") != gender:
                continue


        category = item.get(
            "category"
        )


        if category in avoid_categories:
            continue


        score = float(
            similarities[i]
        )


        # Small bonus for preferred categories
        if category in preferred_categories:
            score += 0.08


        scored_results.append({
            "score": score,
            "item": item
        })


    scored_results.sort(
        key=lambda result:
            result["score"],
        reverse=True
    )


    # --------------------------------------------------------
    # STEP 4:
    # Build varied candidate pool
    # --------------------------------------------------------

    candidates = []

    category_counts = {}

    seen_descriptions = set()


    for result in scored_results:

        item = result["item"]

        category = item.get(
            "category"
        )

        description = (
            item.get("description")
            or ""
        ).strip().lower()


        if description in seen_descriptions:
            continue


        count = category_counts.get(
            category,
            0
        )

        if count >= 4:
            continue


        candidates.append(
            result
        )


        seen_descriptions.add(
            description
        )


        category_counts[category] = (
            count + 1
        )


        if len(candidates) >= 24:
            break


    # --------------------------------------------------------
    # STEP 5:
    # Qwen reranks actual candidates
    # --------------------------------------------------------

    print(
        "\nReranking best candidates..."
    )


    candidates = rerank_candidates(
        candidates=candidates,
        search_profile=profile,
        occasion=occasion,
        weather=weather,
        formality=formality,
        style_keywords=style_keywords,
        comfort_preferences=comfort_preferences,
        gender=gender
    )


    # --------------------------------------------------------
    # STEP 6:
    # Final diversity pass
    # --------------------------------------------------------

    final_results = []

    category_counts = {}


    for result in candidates:

        category = result["item"].get(
            "category"
        )


        count = category_counts.get(
            category,
            0
        )


        # Maximum 2 final items per category
        if count >= 2:
            continue


        final_results.append(
            result
        )


        category_counts[category] = (
            count + 1
        )


        if len(final_results) >= top_k:
            break


    return final_results