import tempfile
import unittest
from pathlib import Path

from fastapi.testclient import TestClient

from online_backend.app import create_app
from online_backend.auth import get_authenticated_player_id
from online_backend.packs import PLAYERS, ef_rating
from online_backend.sponsors import SPONSORS
from online_backend.storage import connect_database


class ExtrasTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.app = create_app(Path(self.tmp.name) / "f.sqlite3")
        self.app.dependency_overrides[get_authenticated_player_id] = lambda: "p1"
        self.client = TestClient(self.app)

    def tearDown(self):
        self.client.close()
        self.tmp.cleanup()

    def test_catalog_has_deep_top_end_and_many_sponsors(self):
        self.assertGreaterEqual(sum(p["rating"] >= 110 for p in PLAYERS), 12)
        self.assertGreaterEqual(len(SPONSORS), 100)
        self.assertEqual(len({s["id"] for s in SPONSORS}), len(SPONSORS))

    def test_efootball_packs_use_efootball_ratings_and_guarantees(self):
        self.client.get("/api/v1/profile")
        con = connect_database(self.app.state.database_path)
        con.execute("UPDATE profiles SET club_funds = 100000000000 WHERE player_id = 'p1'")
        con.commit(); con.close()
        types = self.client.get("/api/v1/packs").json()["efootball_types"]
        self.assertEqual([t["type_id"] for t in types], ["featured", "epic"])
        self.assertEqual(types[1]["price"], 600_000_000)
        for pack_id, low, guarantee in (("featured", 80, 90), ("epic", 90, 95)):
            for _ in range(5):
                pulls = self.client.post("/api/v1/packs/open", json={"pack_type_id": pack_id, "count": 10}).json()["pulls"]
                self.assertTrue(all(ef_rating(p["rating"]) >= low for p in pulls))
                self.assertTrue(any(ef_rating(p["rating"]) >= guarantee for p in pulls))
        self.assertEqual(self.client.post("/api/v1/packs/open",
                         json={"pack_type_id": "epic", "count": 1, "use_owned": True}).status_code, 409)

    def test_promotion_and_relegation_swap_clubs_between_tiers(self):
        self.client.get("/api/v1/profile")
        schedule = self.client.get("/api/v1/league/fixtures").json()
        for md in range(schedule["total_matchdays"]):
            self.assertEqual(self.client.post("/api/v1/league/matchdays/next", json={"expected_matchday": md}).status_code, 200)
        result = self.client.post("/api/v1/league/next-season", json={"expected_season": 1}).json()
        self.assertEqual(len(result["promoted"]), 3)
        self.assertEqual(len(result["relegated"]), 3)
        con = connect_database(self.app.state.database_path)
        counts = {r["tier"]: r["n"] for r in con.execute("SELECT tier, COUNT(*) AS n FROM league_clubs GROUP BY tier")}
        con.close()
        self.assertEqual(counts, {"tier1": 20, "tier2": 21})
        self.assertEqual(self.client.get("/api/v1/league/fixtures").json()["season"], 2)


if __name__ == "__main__":
    unittest.main()
