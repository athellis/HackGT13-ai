import requests


GEOCODING_URL = (
    "https://geocoding-api.open-meteo.com/v1/search"
)

WEATHER_URL = (
    "https://api.open-meteo.com/v1/forecast"
)


def empty_conditions():
    return {
        "temperature": None,
        "humidity": None,
        "wind": None,
        "gustiness": None,
        "precipitation": None,
        "water": None,
        "altitude": None,
        "mud": None,
        "snow": None,
        "wind_direction": None
    }


def geocode_location(location):
    if not location:
        return None

    response = requests.get(
        GEOCODING_URL,
        params={
            "name": location,
            "count": 1,
            "language": "en",
            "format": "json"
        },
        timeout=10
    )

    response.raise_for_status()

    data = response.json()

    results = data.get("results", [])

    if not results:
        return None

    result = results[0]

    return {
        "latitude": result["latitude"],
        "longitude": result["longitude"],
        "name": result.get("name"),
        "country": result.get("country")
    }


def get_environment_conditions(location):
    conditions = empty_conditions()

    coordinates = geocode_location(
        location
    )

    if coordinates is None:
        return conditions

    response = requests.get(
        WEATHER_URL,
        params={
            "latitude":
                coordinates["latitude"],

            "longitude":
                coordinates["longitude"],

            "current": (
                "temperature_2m,"
                "relative_humidity_2m,"
                "precipitation,"
                "rain,"
                "snowfall,"
                "wind_speed_10m,"
                "wind_direction_10m,"
                "wind_gusts_10m"
            ),

            "timezone": "auto"
        },
        timeout=10
    )

    response.raise_for_status()

    data = response.json()

    current = data.get(
        "current",
        {}
    )


    temperature = current.get(
        "temperature_2m"
    )

    humidity = current.get(
        "relative_humidity_2m"
    )

    precipitation = current.get(
        "precipitation",
        0
    ) or 0

    rain = current.get(
        "rain",
        0
    ) or 0

    snowfall = current.get(
        "snowfall",
        0
    ) or 0

    wind = current.get(
        "wind_speed_10m"
    )

    gusts = current.get(
        "wind_gusts_10m"
    )

    wind_direction = current.get(
        "wind_direction_10m"
    )

    altitude = data.get(
        "elevation"
    )


    # -----------------------------------------
    # DERIVED ENVIRONMENT VALUES
    #
    # These are calculated automatically rather
    # than manually entering them per scene.
    # -----------------------------------------

    water = min(
        1.0,
        precipitation / 10.0
    )

    mud = min(
        1.0,
        rain / 8.0
    )

    snow = min(
        1.0,
        snowfall / 5.0
    )


    return {
        "temperature": temperature,

        "humidity": humidity,

        "wind": wind,

        "gustiness": gusts,

        "precipitation": precipitation,

        "water": water,

        "altitude": altitude,

        "mud": mud,

        "snow": snow,

        "wind_direction": wind_direction,

        "resolved_location": (
            coordinates["name"]
        ),

        "resolved_country": (
            coordinates["country"]
        )
    }


if __name__ == "__main__":
    print(
        get_environment_conditions(
            "Athens, Greece"
        )
    )