import unittest
from unittest.mock import patch

from MatchEventSimulator import MatchEventSimulator


class ZeroRandom:
    def random(self) -> float:
        return 0.0

    @staticmethod
    def choice(items):
        return items[0]


class MatchEventSimulatorTests(unittest.TestCase):
    def setUp(self) -> None:
        self.home = {
            "team_name": "Home FC",
            "overall_rating": 80,
            "players": [
                {"player_name": "Striker", "position": "ST", "stamina": 100.0},
                {"player_name": "Keeper", "position": "GK", "stamina": 100.0},
            ],
        }
        self.away = {
            "team_name": "Away FC",
            "overall_rating": 75,
            "players": [
                {"player_name": "Forward", "position": "FW", "stamina": 100.0},
            ],
        }

    def test_simulates_full_match_and_depletes_stamina(self) -> None:
        simulator = MatchEventSimulator(self.home, self.away, rng=ZeroRandom())
        with patch.object(MatchEventSimulator, "BASE_EVENT_CHANCE", 0.0):
            summary = simulator.simulate_match()

        self.assertEqual(summary["home_team_name"], "Home FC")
        self.assertEqual(summary["away_team_name"], "Away FC")
        self.assertEqual(summary["final_score"], "0-0")
        self.assertEqual(summary["timeline"][0]["type"], "KICKOFF")
        self.assertTrue(
            any(event["type"] == "HALF_TIME" for event in summary["timeline"])
        )
        self.assertEqual(summary["timeline"][-1]["type"], "FULL_TIME")
        self.assertAlmostEqual(self.home["players"][0]["stamina"], 28.0)
        self.assertEqual(simulator.current_minute, 90)

    def test_attacking_chance_can_record_goal_and_shot(self) -> None:
        simulator = MatchEventSimulator(self.home, self.away, rng=ZeroRandom())
        simulator.current_minute = 12

        simulator._process_attacking_chance()

        self.assertEqual(simulator.home_score, 1)
        self.assertEqual(simulator.stats["home"]["shots"], 1)
        self.assertEqual(simulator.stats["home"]["shots_on_target"], 1)
        goal = next(event for event in simulator.timeline_events if event["type"] == "GOAL")
        self.assertIn("Striker", goal["message"])

    def test_rejects_non_positive_combined_match_strength(self) -> None:
        simulator = MatchEventSimulator(
            {"overall_rating": 0},
            {"overall_rating": 0},
            rng=ZeroRandom(),
        )

        with self.assertRaises(ValueError):
            simulator._process_attacking_chance()


if __name__ == "__main__":
    unittest.main()
