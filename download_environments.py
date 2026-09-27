import json
import urllib.request
from pathlib import Path


UNITY_ENV_ROOT = Path(
    "/Users/siri/clothrec/ImmersiveShopping/Assets/Environments"
)

USER_AGENT = "ImmersiveShoppingHackathon/1.0"


ENVIRONMENTS = {
    "rooftop_bar": "rooftop_night",
    "bar": "warm_bar",
    "restaurant": "warm_restaurant_night",
    "pool_party": "indoor_pool",
    "snow": "snowy_hillside",
    "forest": "rainforest_trail"
}


def get_json(url):
    request = urllib.request.Request(
        url,
        headers={
            "User-Agent": USER_AGENT
        }
    )

    with urllib.request.urlopen(request) as response:
        return json.loads(response.read().decode())


def download_file(url, destination):
    print(
        f"Downloading:\n{url}\n→ {destination}"
    )

    request = urllib.request.Request(
        url,
        headers={
            "User-Agent": USER_AGENT
        }
    )

    with urllib.request.urlopen(request) as response:
        data = response.read()

    destination.parent.mkdir(
        parents=True,
        exist_ok=True
    )

    with open(destination, "wb") as f:
        f.write(data)


for environment_id, polyhaven_id in ENVIRONMENTS.items():

    print(
        "\n=============================="
    )
    print(
        f"ENVIRONMENT: {environment_id}"
    )
    print(
        f"POLY HAVEN: {polyhaven_id}"
    )

    files = get_json(
        f"https://api.polyhaven.com/files/{polyhaven_id}"
    )

    try:
        hdri_url = (
            files["hdri"]["1k"]["hdr"]["url"]
        )
    except KeyError:
        print(
            f"No 1K HDR found for {polyhaven_id}"
        )
        continue

    environment_folder = (
        UNITY_ENV_ROOT
        / environment_id
        / "Textures"
    )

    destination = (
        environment_folder
        / f"{polyhaven_id}_1k.hdr"
    )

    download_file(
        hdri_url,
        destination
    )

    print(
        f"Done: {environment_id}"
    )


print(
    "\nALL ENVIRONMENTS DOWNLOADED"
)