ENVIRONMENTS = {
    "rooftop_bar": {
        "keywords": [
            "bar",
            "rooftop",
            "nightlife",
            "club",
            "night out"
        ]
    },

    "restaurant": {
        "keywords": [
            "restaurant",
            "dinner",
            "lunch",
            "brunch",
            "date"
        ]
    },

    "beach": {
        "keywords": [
            "beach",
            "vacation",
            "ocean",
            "summer",
            "greece"
        ]
    },

    "pool_party": {
        "keywords": [
            "pool",
            "pool party",
            "swim"
        ]
    },

    "mountain_hike": {
        "keywords": [
            "hike",
            "hiking",
            "mountain",
            "trail"
        ]
    },

    "snow": {
        "keywords": [
            "snow",
            "snowy",
            "winter",
            "ski"
        ]
    },

    "city_day": {
        "keywords": [
            "city",
            "shopping",
            "casual",
            "day out"
        ]
    },

    "formal_event": {
        "keywords": [
            "wedding",
            "gala",
            "formal",
            "cocktail"
        ]
    }
}


def select_environment(
    occasion=None,
    weather=None,
    location=None,
    style_keywords=None
):
    text_parts = []

    if occasion:
        text_parts.append(occasion)

    if weather:
        text_parts.append(weather)

    if location:
        text_parts.append(location)

    if style_keywords:
        text_parts.extend(style_keywords)

    search_text = " ".join(text_parts).lower()

    best_environment = "city_day"
    best_score = 0

    for environment_name, data in ENVIRONMENTS.items():

        score = 0

        for keyword in data["keywords"]:
            if keyword in search_text:
                score += 1

        if score > best_score:
            best_score = score
            best_environment = environment_name

    return best_environment