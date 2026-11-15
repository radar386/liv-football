import unittest

from LeagueSystem import LeagueTier, MultiTierLeagueSystem


class MultiTierLeagueSystemTests(unittest.TestCase):
    def setUp(self) -> None:
        self.league = MultiTierLeagueSystem()
        self.league.build_two_tier_ecosystem(
            [
                {
                    "user_id": "peer-elite",
                    "active_club_name": "Peer Elite",
                    "team_ovr": 82,
                },
                {
                    "user_id": "peer-championship",
                    "active_club_name": "Peer Championship",
                    "team_ovr": 79,
                },
            ]
        )

    def test_builds_full_divisions_and_distributes_peers_by_rating(self) -> None:
        self.assertEqual(len(self.league.tier1_clubs), 20)
        self.assertEqual(len(self.league.tier2_clubs), 20)
        self.assertIn(
            "peer-elite", {club.club_id for club in self.league.tier1_clubs}
        )
        self.assertIn(
            "peer-championship",
            {club.club_id for club in self.league.tier2_clubs},
        )

    def test_standings_and_promotion_relegation_reset_the_season(self) -> None:
        tier2 = self.league.tier2_clubs
        self.league.record_match(tier2[0].club_id, tier2[1].club_id, 3, 0)
        self.league.record_match(tier2[0].club_id, tier2[2].club_id, 2, 0)
        self.assertEqual(
            self.league.standings(LeagueTier.TIER2_CHAMPIONSHIP)[0].club_id,
            tier2[0].club_id,
        )

        promoted, relegated = self.league.process_promotion_and_relegation()

        self.assertEqual(len(promoted), 3)
        self.assertEqual(len(relegated), 3)
        self.assertEqual(len(self.league.tier1_clubs), 20)
        self.assertEqual(len(self.league.tier2_clubs), 20)
        self.assertTrue(
            all(club.current_tier is LeagueTier.TIER1_ELITE for club in promoted)
        )
        self.assertTrue(
            all(club.current_tier is LeagueTier.TIER2_CHAMPIONSHIP for club in relegated)
        )
        self.assertTrue(
            all(club.points == 0 for club in self.league.tier1_clubs + self.league.tier2_clubs)
        )

    def test_rejects_malformed_match_results(self) -> None:
        home, away = self.league.tier1_clubs[:2]
        with self.assertRaises(ValueError):
            self.league.record_match(home.club_id, away.club_id, -1, 0)


if __name__ == "__main__":
    unittest.main()
