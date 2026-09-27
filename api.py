import re

from fastapi import FastAPI
from fastapi import File
from fastapi import Request
from fastapi import UploadFile
from fastapi.staticfiles import StaticFiles
from pydantic import BaseModel

from parse_event import parse_event
from semantic_recommend import recommend_semantic
from environment_selector import select_environment
from environment_conditions import get_environment_conditions

from outfit_image_generator import (
    generate_outfit_image,
    OUTPUT_DIR,
)

from whisper_service import transcribe_upload


# =========================================================
# FASTAPI APP
# =========================================================

app = FastAPI(
    title="AI Fashion Assistant API",
    version="1.0",
)


# =========================================================
# SERVE GENERATED OUTFIT IMAGES
# =========================================================

app.mount(
    "/outfit-images",
    StaticFiles(
        directory=str(OUTPUT_DIR)
    ),
    name="outfit-images",
)


# =========================================================
# REQUEST MODEL
# =========================================================

class RecommendationRequest(BaseModel):
    text: str


# =========================================================
# BASIC HELPERS
# =========================================================

def normalize(value):
    if value is None:
        return ""

    return str(value).strip().lower()


def get_base_item_id(item_id):
    """
    Example:

    WOMEN-Dresses-id_00003250-01_2_side

    becomes:

    WOMEN-Dresses-id_00003250

    This prevents multiple photos/views of the exact
    same garment from becoming separate recommendations.
    """

    if not item_id:
        return None

    match = re.match(
        r"^(.*?-id_\d+)",
        item_id
    )

    if match:
        return match.group(1)

    return item_id


# =========================================================
# CONTEXT DETECTION
# =========================================================

def is_ski_context(event):
    occasion = normalize(
        event.get("occasion")
    )

    weather = normalize(
        event.get("weather")
    )

    text_values = {
        occasion,
        weather,
    }

    ski_words = {
        "ski",
        "skiing",
        "snowboarding",
        "snowboard",
    }

    return bool(
        text_values & ski_words
    )


def is_cold_context(event):
    occasion = normalize(
        event.get("occasion")
    )

    weather = normalize(
        event.get("weather")
    )

    cold_weather = {
        "cold",
        "snow",
        "snowy",
        "freezing",
        "winter",
        "icy",
    }

    cold_occasions = {
        "ski",
        "skiing",
        "snowboarding",
        "snowboard",
    }

    return (
        weather in cold_weather
        or occasion in cold_occasions
    )


def is_beach_context(event):
    occasion = normalize(
        event.get("occasion")
    )

    return occasion in {
        "beach",
        "pool",
        "pool party",
        "pool_party",
        "swimming",
    }


# =========================================================
# CLOTHING CONTEXT FILTERS
# =========================================================

def contains_any(text, phrases):
    text = normalize(text)

    return any(
        phrase in text
        for phrase in phrases
    )


def item_is_valid_for_context(
    item,
    event,
):
    """
    The semantic recommender is still responsible for
    actually finding/ranking fashion items.

    This function ONLY prevents blatantly impossible
    results such as sleeveless chiffon dresses for skiing.
    """

    category = normalize(
        item.get("category")
    )

    description = normalize(
        item.get("description")
    )


    # =====================================================
    # SKIING / SNOW SPORTS
    # =====================================================

    if is_ski_context(event):

        # These are the catalog categories that are
        # remotely appropriate for a snow/ski outfit.
        allowed_categories = {
            "jackets",
            "sweaters",
            "sweatshirts",
            "cardigans",
            "pants",
            "leggings",
            "denim",
            "shirts",
        }

        if category not in allowed_categories:
            return False


        # Even a "jacket" dataset image can describe
        # shorts underneath. Reject those combinations.
        obviously_wrong = {
            "sleeves cut off",
            "no sleeves",
            "sleeveless",
            "tank top",
            "tank shirt",
            "suspenders neckline",
            "shorts",
            "short skirt",
            "three-point pants",
            "three-point shorts",
        }

        if contains_any(
            description,
            obviously_wrong
        ):
            return False


        return True


    # =====================================================
    # GENERIC COLD WEATHER
    # =====================================================

    if is_cold_context(event):

        forbidden_categories = {
            "dresses",
            "skirts",
            "shorts",
            "rompers",
        }

        if category in forbidden_categories:
            return False


        obviously_cold_inappropriate = {
            "sleeves cut off",
            "no sleeves",
            "sleeveless",
            "tank top",
            "tank shirt",
            "three-point shorts",
        }

        if contains_any(
            description,
            obviously_cold_inappropriate
        ):
            return False


    # =====================================================
    # BEACH / POOL
    # =====================================================

    if is_beach_context(event):

        overly_heavy_categories = {
            "sweaters",
            "sweatshirts",
            "cardigans",
            "suiting",
        }

        if category in overly_heavy_categories:
            return False


    return True


# =========================================================
# CONTEXT BONUS
# =========================================================

def context_bonus(item, event):
    """
    Small additional preference AFTER the existing
    semantic recommender runs.

    This does not replace the semantic score.
    """

    category = normalize(
        item.get("category")
    )

    description = normalize(
        item.get("description")
    )

    bonus = 0.0


    # -----------------------------------------------------
    # SKIING
    # -----------------------------------------------------

    if is_ski_context(event):

        if category == "jackets":
            bonus += 0.40

        elif category in {
            "sweaters",
            "sweatshirts",
            "cardigans",
        }:
            bonus += 0.32

        elif category in {
            "pants",
            "leggings",
            "denim",
        }:
            bonus += 0.22

        elif category == "shirts":
            bonus += 0.10


        if "long sleeves" in description:
            bonus += 0.08

        if "long-sleeve" in description:
            bonus += 0.08

        if "outer clothing" in description:
            bonus += 0.12

        if "knitting fabric" in description:
            bonus += 0.08


    # -----------------------------------------------------
    # GENERAL COLD WEATHER
    # -----------------------------------------------------

    elif is_cold_context(event):

        if category in {
            "jackets",
            "sweaters",
            "sweatshirts",
            "cardigans",
        }:
            bonus += 0.25

        if category in {
            "pants",
            "leggings",
            "denim",
        }:
            bonus += 0.12


    # -----------------------------------------------------
    # BEACH / POOL
    # -----------------------------------------------------

    elif is_beach_context(event):

        if category in {
            "shorts",
            "tees",
            "blouses",
            "dresses",
            "skirts",
            "rompers",
        }:
            bonus += 0.18


    return bonus


# =========================================================
# PICK FINAL 3
# =========================================================

def choose_three_recommendations(
    results,
    event,
):
    """
    1. Remove obviously inappropriate clothes.
    2. Add small context bonuses.
    3. Remove duplicate garments.
    4. Return the top 3.
    """

    filtered = []


    for original_position, result in enumerate(
        results
    ):

        item = result.get(
            "item",
            {}
        )


        if not item_is_valid_for_context(
            item,
            event
        ):
            continue


        semantic_score = float(
            result.get(
                "score",
                0.0
            )
        )


        bonus = context_bonus(
            item,
            event
        )


        # Preserve some value from the existing recommender's
        # ordering too.
        order_bonus = max(
            0.0,
            0.10 - (
                original_position * 0.003
            )
        )


        combined_score = (
            semantic_score
            + bonus
            + order_bonus
        )


        filtered.append({
            "result":
                result,

            "combined_score":
                combined_score,
        })


    filtered.sort(
        key=lambda x:
            x["combined_score"],
        reverse=True,
    )


    selected = []

    seen_items = set()

    seen_categories = {}


    for entry in filtered:

        result = entry["result"]

        item = result.get(
            "item",
            {}
        )

        item_id = item.get(
            "id"
        )

        base_id = get_base_item_id(
            item_id
        )


        if not base_id:
            continue


        # Exact garment already selected.
        if base_id in seen_items:
            continue


        category = normalize(
            item.get("category")
        )


        # Don't let all three cards be the same category.
        category_count = (
            seen_categories.get(
                category,
                0
            )
        )

        if category_count >= 2:
            continue


        seen_items.add(
            base_id
        )

        seen_categories[category] = (
            category_count + 1
        )


        selected.append(
            result
        )


        if len(selected) == 3:
            break


    # =====================================================
    # FALLBACK
    #
    # This should rarely happen, but never return fewer
    # than 3 if usable candidates exist.
    # =====================================================

    if len(selected) < 3:

        for result in results:

            item = result.get(
                "item",
                {}
            )

            item_id = item.get(
                "id"
            )

            base_id = get_base_item_id(
                item_id
            )


            if not base_id:
                continue


            if base_id in seen_items:
                continue


            # For skiing, STILL refuse obviously
            # inappropriate categories in fallback.
            if is_ski_context(event):

                category = normalize(
                    item.get("category")
                )

                if category in {
                    "dresses",
                    "skirts",
                    "shorts",
                    "rompers",
                    "tees",
                    "blouses",
                    "graphic",
                }:
                    continue


            seen_items.add(
                base_id
            )

            selected.append(
                result
            )


            if len(selected) == 3:
                break


    return selected


# =========================================================
# ROOT
# =========================================================

@app.get("/")
def root():

    return {
        "status":
            "ok",

        "message":
            "AI Fashion Assistant API is running",
    }


# =========================================================
# WHISPER
# =========================================================

@app.post("/transcribe")
async def transcribe(
    file: UploadFile = File(...)
):

    text = await transcribe_upload(
        file
    )

    return {
        "text": text
    }


# =========================================================
# RECOMMEND
# =========================================================

@app.post("/recommend")
def recommend(
    request: RecommendationRequest,
    http_request: Request,
):

    user_text = request.text


    print("\n")
    print("=" * 60)
    print("USER REQUEST")
    print(user_text)
    print("=" * 60)


    # =====================================================
    # STEP 1
    # PARSE REQUEST
    # =====================================================

    event = parse_event(
        user_text
    )


    print(
        "\nPARSED EVENT:"
    )

    print(
        event
    )


    # =====================================================
    # STEP 2
    # SELECT VR ENVIRONMENT
    # =====================================================

    environment = select_environment(

        occasion=event.get(
            "occasion"
        ),

        weather=event.get(
            "weather"
        ),

        location=event.get(
            "location"
        ),

        style_keywords=event.get(
            "style_keywords",
            []
        ),
    )


    print(
        "\nENVIRONMENT:",
        environment
    )


    # =====================================================
    # STEP 3
    # WEATHER / ENVIRONMENT DATA
    # =====================================================

    environment_data = (
        get_environment_conditions(
            event.get(
                "location"
            )
        )
    )


    print(
        "\nENVIRONMENT DATA:"
    )

    print(
        environment_data
    )


    # =====================================================
    # STEP 4
    # RUN YOUR EXISTING RECOMMENDER
    #
    # IMPORTANT:
    # We request a larger candidate pool here.
    #
    # We are NOT saying these are the final 40.
    # We use them so the context sanity filter has enough
    # valid options to choose from.
    # =====================================================

    candidate_results = (
        recommend_semantic(

            original_text=
                user_text,

            gender=
                event.get(
                    "gender"
                ),

            occasion=
                event.get(
                    "occasion"
                ),

            weather=
                event.get(
                    "weather"
                ),

            formality=
                event.get(
                    "formality"
                ),

            style_keywords=
                event.get(
                    "style_keywords",
                    []
                ),

            comfort_preferences=
                event.get(
                    "comfort_preferences",
                    []
                ),

            location=
                event.get(
                    "location"
                ),

            month=
                event.get(
                    "month"
                ),

            top_k=24,
        )
    )


    print(
        "\nCANDIDATES RETURNED:",
        len(candidate_results)
    )


    # =====================================================
    # STEP 5
    # FINAL CONTEXT-SAFE TOP 3
    # =====================================================

    results = (
        choose_three_recommendations(
            candidate_results,
            event
        )
    )


    print(
        "\nFINAL RECOMMENDATIONS:"
    )


    for i, result in enumerate(
        results
    ):

        item = result.get(
            "item",
            {}
        )

        print(
            f"\n#{i + 1}"
        )

        print(
            "Category:",
            item.get(
                "category"
            )
        )

        print(
            "Description:",
            item.get(
                "description"
            )
        )


    # =====================================================
    # STEP 6
    # GENERATE THE THREE NICE OUTFIT IMAGES
    # =====================================================

    base_url = str(
        http_request.base_url
    ).rstrip("/")


    recommendations = []


    for index, result in enumerate(
        results
    ):

        item = result.get(
            "item",
            {}
        )


        item_id = item.get(
            "id"
        )


        description = item.get(
            "description",
            ""
        )


        print(
            "\nGenerating/reusing image for recommendation",
            index + 1
        )


        image_filename = (
            generate_outfit_image(

                item_id=
                    item_id,

                description=
                    description,

                category=
                    item.get(
                        "category"
                    ),

                material=
                    item.get(
                        "material"
                    ),

                pattern=
                    item.get(
                        "pattern"
                    ),

                length=
                    item.get(
                        "length"
                    ),
            )
        )


        image_url = (
            f"{base_url}"
            f"/outfit-images/"
            f"{image_filename}"
        )


        recommendations.append({

            "id":
                item_id,

            "score":
                result.get(
                    "score"
                ),

            "category":
                item.get(
                    "category"
                ),

            "gender":
                item.get(
                    "gender"
                ),

            "material":
                item.get(
                    "material"
                ),

            "pattern":
                item.get(
                    "pattern"
                ),

            "length":
                item.get(
                    "length"
                ),

            "description":
                description,

            "image_url":
                image_url,
        })


    # =====================================================
    # STEP 7
    # RESPONSE FOR UNITY
    # =====================================================

    return {

        "input":
            user_text,


        "context": {

            "gender":
                event.get(
                    "gender"
                ),

            "occasion":
                event.get(
                    "occasion"
                ),

            "weather":
                event.get(
                    "weather"
                ),

            "formality":
                event.get(
                    "formality"
                ),

            "style_keywords":
                event.get(
                    "style_keywords",
                    []
                ),

            "comfort_preferences":
                event.get(
                    "comfort_preferences",
                    []
                ),

            "location":
                event.get(
                    "location"
                ),

            "month":
                event.get(
                    "month"
                ),
        },


        "environment":
            environment,


        "environment_data":
            environment_data,


        "recommendations":
            recommendations,
    }