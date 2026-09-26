from fastapi import FastAPI
from pydantic import BaseModel

from parse_event import parse_event
from semantic_recommend import recommend_semantic
from environment_selector import select_environment
from environment_conditions import (
    get_environment_conditions
)

app = FastAPI(
    title="AI Fashion Assistant API",
    version="1.0"
)


class RecommendationRequest(BaseModel):
    text: str


@app.get("/")
def root():
    return {
        "status": "ok",
        "message": "AI Fashion Assistant API is running"
    }


@app.post("/recommend")
def recommend(request: RecommendationRequest):

    user_text = request.text

    # ---------------------------------------
    # STEP 1: Parse user request
    # ---------------------------------------

    event = parse_event(
        user_text
    )


    # ---------------------------------------
    # STEP 2: Select immersive environment
    # ---------------------------------------

    environment = select_environment(
        occasion=event.get("occasion"),
        weather=event.get("weather"),
        location=event.get("location"),
        style_keywords=event.get(
            "style_keywords",
            []
        )
    )

    environment_data = (
    get_environment_conditions(
        event.get("location")
    )
)


    # ---------------------------------------
    # STEP 3: Get clothing recommendations
    # ---------------------------------------

    results = recommend_semantic(
        original_text=user_text,
        gender=event.get("gender"),
        occasion=event.get("occasion"),
        weather=event.get("weather"),
        formality=event.get("formality"),
        style_keywords=event.get(
            "style_keywords",
            []
        ),
        comfort_preferences=event.get(
            "comfort_preferences",
            []
        ),
        location=event.get("location"),
        month=event.get("month"),
        top_k=10
    )


    # ---------------------------------------
    # STEP 4: Clean output
    # ---------------------------------------

    recommendations = []

    for result in results:

        item = result["item"]

        recommendations.append({
            "id": item.get("id"),
            "score": result.get("score"),
            "category": item.get("category"),
            "gender": item.get("gender"),
            "material": item.get("material"),
            "pattern": item.get("pattern"),
            "length": item.get("length"),
            "description": item.get(
                "description"
            )
        })


    # ---------------------------------------
    # STEP 5: Return everything
    # ---------------------------------------

    return {
        "input": user_text,

        "context": {
            "gender": event.get("gender"),
            "occasion": event.get("occasion"),
            "weather": event.get("weather"),
            "formality": event.get("formality"),
            "style_keywords": event.get(
                "style_keywords",
                []
            ),
            "comfort_preferences": event.get(
                "comfort_preferences",
                []
            ),
            "location": event.get("location"),
            "month": event.get("month")
        },

        "environment": environment,

        "environment_data": environment_data,

        "recommendations": recommendations
    }