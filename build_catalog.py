from datasets import load_dataset
import json
import re

print("Loading dataset...")

ds = load_dataset("Marqo/deepfashion-multimodal")
data = ds["data"]

catalog = []


def extract_material(text):
    materials = [
        "cotton",
        "denim",
        "leather",
        "chiffon",
        "knit",
        "wool",
        "silk",
        "polyester",
        "linen"
    ]

    text_lower = text.lower()

    for material in materials:
        if material in text_lower:
            return material

    return None


def extract_pattern(text):
    patterns = [
        "plaid",
        "striped",
        "floral",
        "solid",
        "printed",
        "checkered",
        "polka dot"
    ]

    text_lower = text.lower()

    for pattern in patterns:
        if pattern in text_lower:
            return pattern

    return None


def extract_length(text):
    text_lower = text.lower()

    if "long length" in text_lower:
        return "long"

    if "short length" in text_lower:
        return "short"

    if "medium length" in text_lower:
        return "medium"

    return None


for i, item in enumerate(data):

    text = item["text"] or ""

    clean_item = {
        "id": item["item_ID"],
        "gender": item["category1"],
        "category": item["category2"],
        "subcategory": item["category3"],
        "description": text,

        "material": extract_material(text),
        "pattern": extract_pattern(text),
        "length": extract_length(text)
    }

    catalog.append(clean_item)


with open("fashion_catalog.json", "w") as f:
    json.dump(catalog, f, indent=2)


print(f"Done!")
print(f"Saved {len(catalog)} items to fashion_catalog.json")