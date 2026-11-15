import copy
import unittest

from MatchEventSimulator import MatchEventSimulator
from MatchSettlement import settle_verified_match
from P2PMatchVerifier import P2PMatchVerifier
from SponsorRewardManager import (
    SponsorRewardManager,
    create_sample_loki_profile,
)


class MatchSettlementTests(unittest.TestCase):
    def setUp(self) -> None:
        self.home = {
            "team_name": "Loki FC",
            "overall_rating": 83,
            "players": [
                {"player_name": "Striker", "position": "ST", "stamina": 100.0}
            ],
        }
        self.away = {
            "team_name": "Rivals FC",
            "overall_rating": 78,
            "players": [
                {"player_name": "Forward", "position": "FW", "stamina": 100.0}
            ],
        }

    def test_seeded_peer_simulations_match_without_mutating_inputs(self) -> None:
        original_home = copy.deepcopy(self.home)
        original_away = copy.deepcopy(self.away)

        client_a = P2PMatchVerifier.run_deterministic_simulation(
            12345, self.home, self.away
        )
        client_b = P2PMatchVerifier.run_deterministic_simulation(
            12345, self.home, self.away
        )

        self.assertEqual(client_a, client_b)
        self.assertEqual(
            P2PMatchVerifier.calculate_match_hash(client_a),
            P2PMatchVerifier.calculate_match_hash(client_b),
        )
        self.assertEqual(self.home, original_home)
        self.assertEqual(self.away, original_away)

    def test_tampered_result_does_not_pay_sponsors(self) -> None:
        summary = MatchEventSimulator(
            {"team_name": "Loki FC", "overall_rating": 83},
            {"team_name": "Rivals FC", "overall_rating": 78},
        ).simulate_match()
        remote_hash = P2PMatchVerifier.calculate_match_hash(summary)
        summary["home_score"] += 1
        profile = create_sample_loki_profile()
        original_profile = copy.deepcopy(profile)

        result = settle_verified_match(summary, remote_hash, profile)

        self.assertFalse(result["verified"])
        self.assertIsNone(result["financial_invoice"])
        self.assertEqual(profile, original_profile)

    def test_verified_clean_sheet_pays_sample_loki_sponsors(self) -> None:
        summary = {
            "final_score": "2-0",
            "home_score": 2,
            "away_score": 0,
            "stats": {"home": {}, "away": {}},
            "timeline": [{"minute": 90, "type": "FULL_TIME", "side": "neutral"}],
        }
        remote_hash = P2PMatchVerifier.calculate_match_hash(summary)
        profile = create_sample_loki_profile()

        result = settle_verified_match(summary, remote_hash, profile)

        self.assertTrue(result["verified"])
        self.assertEqual(result["match_hash"], remote_hash)
        invoice = result["financial_invoice"]
        self.assertEqual(invoice["total_commercial_revenue"], 212_500_000)
        self.assertTrue(invoice["clean_sheet_achieved"])
        self.assertEqual(profile["club_funds"], 262_500_000)
        self.assertEqual(
            [line["clean_sheet_bonus"] for line in invoice["sponsor_breakdown"]],
            [3_750_000, 8_750_000, 30_000_000],
        )

    def test_expired_sponsors_are_skipped_and_contracts_expire_by_season(self) -> None:
        profile = {
            "club_funds": 100,
            "active_sponsors": [
                {"sponsor_name": "Expired", "payout": 50, "seasons_remaining": 0},
                {"sponsor_name": "Final Season", "payout": 20, "seasons_remaining": 1},
            ],
        }

        invoice = SponsorRewardManager.process_match_payouts(
            {"away_score": 1}, profile
        )
        expired = SponsorRewardManager.decrement_seasonal_contracts(profile)

        self.assertEqual(invoice["total_commercial_revenue"], 20)
        self.assertEqual(profile["club_funds"], 120)
        self.assertEqual(expired, ["Final Season"])
        self.assertEqual(profile["active_sponsors"][1]["seasons_remaining"], 0)

    def test_invalid_sponsor_data_does_not_partially_credit_balance(self) -> None:
        profile = {
            "club_funds": 100,
            "active_sponsors": [
                {"payout": 20, "seasons_remaining": 1},
                {"payout": -1, "seasons_remaining": 1},
            ],
        }

        with self.assertRaises(ValueError):
            SponsorRewardManager.process_match_payouts({"away_score": 0}, profile)

        self.assertEqual(profile["club_funds"], 100)


if __name__ == "__main__":
    unittest.main()
