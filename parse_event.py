import json
import subprocess
import re


def normalize_occasion(occasion):
    if occasion is None:
        return None

    occasion = occasion.lower().strip()

    mappings = {
        "birthday dinner": "birthday",
        "birthday party": "birthday",
        "birthday": "birthday",

        "rooftop bar": "bar",
        "nightclub": "bar",
        "club": "bar",
        "bar": "bar",

        "baby shower": "baby_shower",

        "business dinner": "business",
        "office": "business",
        "work": "business",
        "business": "business",

        "hiking": "hiking",
        "hike": "hiking",

        "beach": "beach",

        "wedding": "wedding",

        "brunch": "restaurant",
        "lunch": "restaurant",
        "dinner": "restaurant",
        "cafe": "restaurant",
        "restaurant": "restaurant",

        "party": "party"
    }

    for phrase, normalized in mappings.items():
        if phrase in occasion:
            return normalized

    return occasion


def explicit_gender_from_text(text):
    text = text.lower()

    women_words = [
        "i'm a woman",
        "i am a woman",
        "for a woman",
        "women's",
        "womens",
        "female"
    ]

    men_words = [
        "i'm a man",
        "i am a man",
        "for a man",
        "men's",
        "mens",
        "male"
    ]

    for phrase in women_words:
        if phrase in text:
            return "women"

    for phrase in men_words:
        if phrase in text:
            return "men"

    return None


def has_explicit_weather_information(text):
    text = text.lower()

    weather_words = [
        "hot outside",
        "warm outside",
        "cold outside",
        "cool outside",
        "freezing",
        "snow",
        "snowing",
        "rain",
        "raining",
        "humid",
        "sunny",
        "weather",
        "winter",
        "summer",
        "spring",
        "fall",
        "autumn",
        "january",
        "february",
        "march",
        "april",
        "may",
        "june",
        "july",
        "august",
        "september",
        "october",
        "november",
        "december"
    ]

    return any(word in text for word in weather_words)


def parse_event(user_text):

    prompt = f"""
You are extracting information for a clothing recommendation system.

Return ONLY valid JSON.
No markdown.
No explanation.

Use exactly this format:

{{
  "gender": "men" | "women" | null,
  "occasion": string | null,
  "weather": "hot" | "warm" | "mild" | "cool" | "cold" | null,
  "formality": number,
  "style_keywords": [string],
  "comfort_preferences": [string],
  "location": string | null,
  "month": string | null
}}

VERY IMPORTANT RULES:

GENDER:
Only return "men" or "women" if the user EXPLICITLY states the gender
or explicitly asks for men's or women's clothing.

Do NOT guess gender from:
- voice
- event
- clothing style
- wording
- pronouns like "I"

If gender is not explicitly stated, return null.

WEATHER:
Weather means actual outdoor temperature or climate.

Only infer weather when there is actual evidence such as:
- explicit temperature/climate
- season
- month
- location + time of year

Do NOT treat aesthetic words as weather.

Examples:

"make me look hot"
means attractive/stylish.
It does NOT mean hot weather.

"that outfit is cool"
usually means stylish.
It does NOT mean cool weather.

"I don't want to be hot"
is a comfort preference.
It does NOT automatically mean the weather is hot.

STYLE:
Put aesthetic requests here.

Examples:
- hot
- sexy
- cool
- elegant
- trendy
- edgy
- sophisticated
- cute
- minimalist

COMFORT:
Put physical clothing preferences here.

Examples:
- comfortable
- breathable
- warm
- lightweight
- loose
- fitted
- not too hot

FORMALITY:
0.0 = extremely casual
0.2 = casual
0.4 = elevated casual
0.5 = smart casual
0.7 = dressy
0.85 = formal
1.0 = black tie

USER:
"{user_text}"
"""

    result = subprocess.run(
        ["ollama", "run", "qwen2.5:3b", prompt],
        capture_output=True,
        text=True
    )

    if result.returncode != 0:
        raise RuntimeError(
            "Ollama failed:\n" + result.stderr
        )

    raw_output = result.stdout.strip()

    raw_output = raw_output.replace("```json", "")
    raw_output = raw_output.replace("```", "")
    raw_output = raw_output.strip()

    parsed = json.loads(raw_output)

    # ---------------------------
    # NORMALIZE OCCASION
    # ---------------------------

    parsed["occasion"] = normalize_occasion(
        parsed.get("occasion")
    )

    NON_LOCATIONS = {
    "bar",
    "restaurant",
    "club",
    "nightclub",
    "office",
    "wedding",
    "party",
    "birthday",
    "birthday party",
    "baby shower"
    }

    location = parsed.get("location")

    if (
        location is not None
        and location.lower().strip() in NON_LOCATIONS
    ):
        parsed["location"] = None

    # ---------------------------
    # FORCE GENDER TO BE EXPLICIT
    # ---------------------------

    parsed["gender"] = explicit_gender_from_text(
        user_text
    )

    # ---------------------------
    # DON'T ALLOW RANDOM WEATHER
    # ---------------------------

    if not has_explicit_weather_information(user_text):
        parsed["weather"] = None

    # ---------------------------
    # DEFAULT EMPTY LISTS
    # ---------------------------

    if parsed.get("style_keywords") is None:
        parsed["style_keywords"] = []

    if parsed.get("comfort_preferences") is None:
        parsed["comfort_preferences"] = []

    return parsed


if __name__ == "__main__":

    test = (
        "I'm going to the bar tonight with my friends "
        "and I want something that'll make me look hot."
    )

    parsed = parse_event(test)

    print(json.dumps(parsed, indent=2))