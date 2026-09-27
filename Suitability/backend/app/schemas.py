from typing import Literal

from pydantic import BaseModel, Field


class BodyMeasurements(BaseModel):
    chest: float = Field(gt=0, description="Body chest circumference in inches")
    waist: float = Field(gt=0, description="Body waist circumference in inches")
    shoulders: float = Field(gt=0, description="Shoulder width in inches")
    sleeves: float | None = Field(
        default=None,
        gt=0,
        description="Optional preferred sleeve length in inches",
    )


class ReferenceGarment(BaseModel):
    brand: str = "Favorite brand"
    name: str = "Favorite top"
    label: str = "M"
    measurements: dict[str, float] = Field(default_factory=dict)


class RecommendationRequest(BaseModel):
    event_description: str = Field(min_length=3, max_length=500)
    body: BodyMeasurements
    fit_preference: Literal["fitted", "regular", "relaxed", "oversized"] = "regular"
    style_preferences: list[str] = Field(default_factory=list, max_length=8)
    audience: Literal["all", "women", "men", "unisex"] = "all"
    max_price: float | None = Field(default=None, gt=0)
    reference: ReferenceGarment | None = None


class CartItem(BaseModel):
    product_id: str
    size: str
    quantity: int = Field(default=1, ge=1, le=10)


class CheckoutRequest(BaseModel):
    items: list[CartItem] = Field(min_length=1, max_length=20)


class AssistantRequest(BaseModel):
    message: str = Field(min_length=2, max_length=400)
    event_description: str = Field(min_length=3, max_length=500)
    body: BodyMeasurements
    fit_preference: Literal["fitted", "regular", "relaxed", "oversized"] = "regular"
    style_preferences: list[str] = Field(default_factory=list, max_length=8)
    audience: Literal["all", "women", "men", "unisex"] = "all"
    max_price: float | None = Field(default=None, gt=0)
    reference: ReferenceGarment | None = None


class SwarmSearchRequest(BaseModel):
    query: str = Field(min_length=2, max_length=400)
    event_description: str = Field(min_length=3, max_length=500)
    body: BodyMeasurements
    fit_preference: Literal["fitted", "regular", "relaxed", "oversized"] = "regular"
    style_preferences: list[str] = Field(default_factory=list, max_length=8)
    audience: Literal["all", "women", "men", "unisex"] = "all"
    max_price: float | None = Field(default=None, gt=0)
    reference: ReferenceGarment | None = None


class TryOnRequest(BaseModel):
    person_image_base64: str = Field(min_length=32)
    product_id: str
    garment_image_url: str | None = None
    garment_color: str | None = None
    garment_name: str | None = None
    size: str | None = "M"
