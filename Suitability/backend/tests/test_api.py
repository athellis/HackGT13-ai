import unittest

from fastapi.testclient import TestClient

from app.main import app


class ApiTests(unittest.TestCase):
    def setUp(self) -> None:
        self.client = TestClient(app)

    def test_recommendations_parse_event_and_return_size_fit(self) -> None:
        response = self.client.post(
            "/api/recommendations",
            json={
                "event_description": "Birthday dinner in Greece in July",
                "body": {"chest": 38, "waist": 32, "shoulders": 17},
                "fit_preference": "relaxed",
                "style_preferences": ["minimal", "coastal"],
            },
        )
        self.assertEqual(response.status_code, 200)
        payload = response.json()
        self.assertEqual(payload["event"]["location"], "Greece")
        self.assertTrue(payload["recommendations"])
        self.assertIn("recommended_size", payload["recommendations"][0])
        self.assertIn("M", payload["recommendations"][0]["size_fits"])

    def test_checkout_confirms_valid_demo_order(self) -> None:
        response = self.client.post(
            "/api/checkout",
            json={"items": [{"product_id": "coast-linen-shirt", "size": "M", "quantity": 1}]},
        )
        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.json()["status"], "confirmed")
        self.assertEqual(response.json()["total"], 88)

    def test_audience_filter_limits_products_and_all_keeps_catalog(self) -> None:
        request = {
            "event_description": "Dinner in July",
            "body": {"chest": 38, "waist": 32, "shoulders": 17},
            "fit_preference": "relaxed",
            "style_preferences": ["minimal"],
        }
        for audience in ("women", "men", "unisex"):
            response = self.client.post("/api/recommendations", json={**request, "audience": audience})
            self.assertEqual(response.status_code, 200)
            products = [item["product"] for item in response.json()["recommendations"]]
            self.assertTrue(products)
            self.assertTrue(all(product["audience"] == audience for product in products))
        all_response = self.client.post("/api/recommendations", json={**request, "audience": "all"})
        all_products = [item["product"] for item in all_response.json()["recommendations"]]
        self.assertEqual(len(all_products), 6)

    def test_assistant_returns_reply_and_highlights(self) -> None:
        response = self.client.post(
            "/api/assistant",
            json={
                "message": "Find me a jacket under $150 for hot weather",
                "event_description": "Birthday dinner in Greece in July",
                "body": {"chest": 38, "waist": 32, "shoulders": 17},
                "fit_preference": "relaxed",
                "style_preferences": ["minimal"],
            },
        )
        self.assertEqual(response.status_code, 200)
        payload = response.json()
        self.assertTrue(payload["reply"])
        self.assertIn("recommendations", payload)

    def test_swarm_search_returns_ranked_results_and_notes(self) -> None:
        response = self.client.post(
            "/api/swarm-search",
            json={
                "query": "airy linen for a coastal dinner under $120",
                "event_description": "Birthday dinner in Greece in July",
                "body": {"chest": 38, "waist": 32, "shoulders": 17},
                "fit_preference": "relaxed",
                "style_preferences": ["minimal", "coastal"],
            },
        )
        self.assertEqual(response.status_code, 200)
        payload = response.json()
        self.assertTrue(payload["recommendations"])
        self.assertTrue(payload["agent_notes"])
        self.assertTrue(payload["highlight_ids"])
        self.assertTrue(all("/garments/" in item["product"]["image"] for item in payload["recommendations"]))

    def test_swarm_search_prioritizes_cold_weather_jackets(self) -> None:
        response = self.client.post(
            "/api/swarm-search",
            json={
                "query": "find me a jacket that I can wear in the cold weather",
                "event_description": "Birthday dinner by the water in Greece in July",
                "body": {"chest": 38, "waist": 32, "shoulders": 17},
                "fit_preference": "relaxed",
                "style_preferences": ["minimal", "coastal"],
            },
        )
        self.assertEqual(response.status_code, 200)
        payload = response.json()
        top = payload["recommendations"][0]["product"]
        self.assertEqual(top["category"], "jacket")
        self.assertIn("cold", top["weather_tags"])
        notes = " ".join(payload["agent_notes"]).casefold()
        self.assertTrue("jacket" in notes or "cold" in notes)

    def test_tryon_fallback_returns_image_without_api_keys(self) -> None:
        tiny = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg=="
        response = self.client.post(
            "/api/tryon",
            json={"person_image_base64": tiny, "product_id": "coast-linen-shirt", "size": "M"},
        )
        self.assertEqual(response.status_code, 200)
        payload = response.json()
        self.assertTrue(payload["image_base64"])
        self.assertIn(payload["provider"], {"composite-fallback", "openai-images", "replicate-idm-vton"})

    def test_transcribe_without_key_returns_provider_none(self) -> None:
        response = self.client.post(
            "/api/transcribe",
            files={"file": ("sample.webm", b"not-real-audio", "audio/webm")},
        )
        self.assertEqual(response.status_code, 200)
        self.assertIn(response.json().get("provider"), {"none", "openai-whisper"})


if __name__ == "__main__":
    unittest.main()
