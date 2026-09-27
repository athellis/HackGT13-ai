import unittest

from app.engine import recommend, score_body_fit, score_event
from app.catalog import CATALOG


class FitEngineTests(unittest.TestCase):
    def test_relaxed_fit_prefers_garment_with_more_ease(self) -> None:
        body = {"chest": 38, "waist": 32, "shoulders": 17, "sleeves": 24}
        fitted, _ = score_body_fit(body, {"chest": 40, "waist": 34, "shoulders": 17, "sleeves": 24}, "fitted")
        relaxed, _ = score_body_fit(body, {"chest": 44, "waist": 40, "shoulders": 18, "sleeves": 25}, "relaxed")
        self.assertGreaterEqual(relaxed, 50)
        self.assertGreaterEqual(fitted, 40)

    def test_event_score_rewards_matching_weather(self) -> None:
        product = CATALOG[0]
        warm = score_event(product, {"formality": "smart casual", "weather": "warm", "activity": "dinner"})
        cold = score_event(product, {"formality": "formal", "weather": "cold", "activity": "outdoors"})
        self.assertGreater(warm, cold)

    def test_recommendations_include_fit_breakdown_and_rank_event_match(self) -> None:
        ranked = recommend(
            CATALOG,
            {"chest": 38, "waist": 32, "shoulders": 17, "sleeves": 24},
            {"scene": "coastal-evening", "formality": "smart casual", "weather": "warm", "activity": "dinner"},
            "relaxed",
            ["minimal", "coastal"],
        )
        self.assertTrue(ranked)
        self.assertIn("body_fit", ranked[0])
        self.assertIn("env_match", ranked[0])
        self.assertGreaterEqual(ranked[0]["overall"], ranked[-1]["overall"])


if __name__ == "__main__":
    unittest.main()
