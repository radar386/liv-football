import json
import sqlite3
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from fastapi.testclient import TestClient

from online_backend.app import create_app
from online_backend.auth import get_authenticated_player_id
from online_backend.default_profile_seed import (
    DEFAULT_PROFILES,
    seed_default_profiles,
)
from online_backend.storage import connect_database, ensure_player_profile
from online_backend.packs import PLAYERS


class OnlineBackendTests(unittest.TestCase):
    def setUp(self) -> None:
        self.temp_directory = tempfile.TemporaryDirectory()
        database_path = Path(self.temp_directory.name) / "football.sqlite3"
        self.app = create_app(database_path)
        self.app.dependency_overrides[get_authenticated_player_id] = (
            lambda: "player-one"
        )
        self.client = TestClient(self.app)

    def tearDown(self) -> None:
        self.client.close()
        self.temp_directory.cleanup()

    def test_profile_is_created_and_renamed_persistently(self) -> None:
        initial = self.client.get("/api/v1/profile")
        self.assertEqual(initial.status_code, 200)
        self.assertEqual(initial.json()["team_name"], "New Club")
        self.assertEqual(initial.json()["club_funds"], 50_000_000)
        self.assertEqual(initial.json()["tier"], "tier2")
        self.assertEqual(initial.json()["owned_packs"], [0, 0, 0, 0, 0])

        updated = self.client.patch(
            "/api/v1/profile", json={"team_name": "Loki FC"}
        )
        self.assertEqual(updated.status_code, 200)
        self.assertEqual(updated.json()["team_name"], "Loki FC")
        self.assertEqual(
            self.client.get("/api/v1/profile").json()["team_name"], "Loki FC"
        )

    def test_pack_opening_is_server_authoritative_and_awards_contracts(self) -> None:
        catalog_response = self.client.get("/api/v1/packs")
        self.assertEqual(catalog_response.status_code, 200)
        packs = catalog_response.json()["types"]
        self.assertEqual(
            [pack["type_id"] for pack in packs],
            ["standard", "premium", "elite", "world_class", "superstar"],
        )
        self.assertEqual(catalog_response.json()["duplicate_refund_percent"], 30)
        world_class = packs[3]
        self.assertEqual(world_class["min_rating"], 100)
        self.assertEqual(world_class["price"], 25_000_000)
        self.assertEqual(world_class["chances"], [0, 0, 80, 20])
        self.assertEqual(world_class["guarantee_min_rating"], 105)
        superstar = packs[4]
        self.assertEqual(superstar["min_rating"], 110)
        self.assertEqual(superstar["price"], 100_000_000)
        self.assertEqual(superstar["chances"], [0, 0, 0, 100])
        self.assertEqual(superstar["guarantee_min_rating"], 110)
        self.assertTrue(all(pack["owned_count"] == 0 for pack in packs))

        opened = self.client.post(
            "/api/v1/packs/open",
            json={
                "pack_type_id": "standard",
                "count": 10,
                "use_owned": False,
                "player_id": "client-cannot-pick-player",
                "rating": 185,
            },
        )
        self.assertEqual(opened.status_code, 200)
        result = opened.json()
        self.assertEqual(len(result["pulls"]), 10)
        self.assertTrue(any(pull["rating"] >= 80 for pull in result["pulls"]))
        self.assertEqual(result["club_funds"] >= 30_000_000, True)
        self.assertEqual(result["overall_rating"] >= 60, True)
        for pull in result["pulls"]:
            if not pull["duplicate"]:
                self.assertGreaterEqual(pull["seasons"], 1)
                self.assertLessEqual(pull["seasons"], 4)

        connection = connect_database(self.app.state.database_path)
        roster = connection.execute(
            """
            SELECT s.player_id, s.player_name, s.position, s.rating,
                   c.seasons_left
            FROM club_squad AS s
            JOIN squad_contracts AS c
              ON c.owner_player_id = s.owner_player_id
             AND c.player_id = s.player_id
            WHERE s.owner_player_id = ?
            """,
            ("player-one",),
        ).fetchall()
        connection.close()
        catalog_by_id = {
            player["id"]: player
            for player in PLAYERS
        }
        self.assertTrue(roster)
        for row in roster:
            player = catalog_by_id[row["player_id"]]
            self.assertEqual(row["player_name"], player["name"])
            self.assertEqual(row["position"], player["position"])
            self.assertEqual(row["rating"], player["rating"])
            self.assertGreaterEqual(row["seasons_left"], 1)
            self.assertLessEqual(row["seasons_left"], 4)

        too_expensive = self.client.post(
            "/api/v1/packs/open",
            json={"pack_type_id": "elite", "count": 10},
        )
        self.assertEqual(too_expensive.status_code, 409)
        invalid_count = self.client.post(
            "/api/v1/packs/open",
            json={"pack_type_id": "standard", "count": 2},
        )
        self.assertEqual(invalid_count.status_code, 422)

    def test_admin_can_grant_and_audit_packs_for_free_opening(self) -> None:
        self.client.get("/api/v1/profile")
        with patch.dict("os.environ", {"ADMIN_PLAYER_IDS": "player-one"}):
            granted = self.client.post(
                "/api/v1/admin/profiles/player-one/packs",
                json={"pack_type_id": "standard", "count": 2},
            )
            self.assertEqual(granted.status_code, 200)
            self.assertEqual(granted.json()["count"], 2)

            before_funds = self.client.get("/api/v1/profile").json()["club_funds"]
            opened = self.client.post(
                "/api/v1/packs/open",
                json={
                    "pack_type_id": "standard",
                    "count": 1,
                    "use_owned": True,
                },
            )
            self.assertEqual(opened.status_code, 200)
            self.assertTrue(opened.json()["use_owned"])
            self.assertEqual(opened.json()["owned_packs"][0]["count"], 1)
            self.assertGreaterEqual(opened.json()["club_funds"], before_funds)

            connection = connect_database(self.app.state.database_path)
            audit = connection.execute(
                """
                SELECT action, details_json FROM admin_audit_log
                WHERE actor_player_id = ? AND target_player_id = ?
                """,
                ("player-one", "player-one"),
            ).fetchone()
            connection.close()
            self.assertEqual(audit["action"], "grant_packs")
            self.assertEqual(
                json.loads(audit["details_json"]),
                {"pack_type_id": "standard", "count": 2},
            )

    def test_pack_purchase_rejects_unknown_type_and_unowned_packs(self) -> None:
        unknown_type = self.client.post(
            "/api/v1/packs/open",
            json={"pack_type_id": "client-defined", "count": 1},
        )
        self.assertEqual(unknown_type.status_code, 404)

        unowned = self.client.post(
            "/api/v1/packs/open",
            json={
                "pack_type_id": "premium",
                "count": 1,
                "use_owned": True,
            },
        )
        self.assertEqual(unowned.status_code, 409)
        self.assertEqual(
            self.client.get("/api/v1/profile").json()["club_funds"],
            50_000_000,
        )

    def test_every_ten_pack_guarantee_and_minimum_rating_is_enforced(self) -> None:
        self.client.get("/api/v1/profile")
        connection = connect_database(self.app.state.database_path)
        connection.execute(
            "UPDATE profiles SET club_funds = ? WHERE player_id = ?",
            (5_000_000_000, "player-one"),
        )
        connection.commit()
        connection.close()

        expectations = (
            ("standard", 0, 80),
            ("premium", 80, 95),
            ("elite", 95, 105),
            ("world_class", 100, 105),
            ("superstar", 110, 110),
        )
        for pack_type_id, min_rating, guaranteed_rating in expectations:
            response = self.client.post(
                "/api/v1/packs/open",
                json={"pack_type_id": pack_type_id, "count": 10},
            )
            self.assertEqual(response.status_code, 200)
            pulls = response.json()["pulls"]
            self.assertEqual(len(pulls), 10)
            self.assertTrue(all(pull["rating"] >= min_rating for pull in pulls))
            self.assertTrue(
                any(pull["rating"] >= guaranteed_rating for pull in pulls)
            )

    def test_duplicate_pack_pull_refunds_catalog_value_share(self) -> None:
        self.client.get("/api/v1/profile")
        connection = connect_database(self.app.state.database_path)
        connection.executemany(
            """
            INSERT INTO club_squad
                (owner_player_id, player_id, player_name, position, rating, has_rating)
            VALUES (?, ?, ?, ?, ?, 1)
            """,
            [
                (
                    "player-one",
                    player["id"],
                    player["name"],
                    player["position"],
                    player["rating"],
                )
                for player in PLAYERS
            ],
        )
        connection.commit()
        connection.close()

        response = self.client.post(
            "/api/v1/packs/open",
            json={"pack_type_id": "standard", "count": 1},
        )
        self.assertEqual(response.status_code, 200)
        pull = response.json()["pulls"][0]
        self.assertTrue(pull["duplicate"])
        self.assertEqual(pull["seasons"], 0)
        self.assertEqual(pull["refund"], pull["market_value"] * 30 // 100)
        self.assertEqual(response.json()["club_funds"], 48_000_000 + pull["refund"])

    def test_default_profile_seed_requires_unique_account_mapping_and_is_not_admin(self) -> None:
        mapping = {
            username: "seeded-" + str(index)
            for index, (username, _, _) in enumerate(DEFAULT_PROFILES)
        }
        seeded_count = seed_default_profiles(
            self.app.state.database_path, mapping
        )
        self.assertEqual(seeded_count, len(DEFAULT_PROFILES))

        connection = connect_database(self.app.state.database_path)
        rows = connection.execute(
            """
            SELECT p.player_id, p.username, p.team_name, p.crest_id,
                   p.club_funds, p.owned_packs, p.coach_id, p.sponsor_id,
                   p.formation_choice, p.tactic, p.lineup_ids, p.achievements,
                   c.tier, c.is_ai
            FROM profiles AS p
            JOIN league_clubs AS c ON c.club_id = p.club_id
            WHERE p.username IS NOT NULL
            """
        ).fetchall()
        connection.close()
        self.assertEqual(len(rows), len(DEFAULT_PROFILES))
        self.assertTrue(all(row["club_funds"] == 0 for row in rows))
        self.assertTrue(
            all(json.loads(row["owned_packs"]) == [0, 0, 0, 0, 0] for row in rows)
        )
        self.assertTrue(all(row["coach_id"] == "" for row in rows))
        self.assertTrue(all(row["sponsor_id"] == "" for row in rows))
        self.assertTrue(all(row["formation_choice"] == "" for row in rows))
        self.assertTrue(all(row["tactic"] == "Balanced" for row in rows))
        self.assertTrue(all(row["lineup_ids"] == "[]" for row in rows))
        self.assertTrue(all(row["achievements"] == "[]" for row in rows))
        self.assertTrue(all(row["tier"] == "tier2" and row["is_ai"] == 0 for row in rows))
        expected_profiles = {
            username: (club_name, crest_id)
            for username, club_name, crest_id in DEFAULT_PROFILES
        }
        self.assertEqual(
            {
                row["username"]: (row["team_name"], row["crest_id"])
                for row in rows
            },
            expected_profiles,
        )
        self.assertEqual(
            {row["player_id"] for row in rows},
            set(mapping.values()),
        )

        with patch.dict(
            "os.environ",
            {"ADMIN_PLAYER_IDS": "seeded-7,seeded-9"},
        ):
            self.app.dependency_overrides[get_authenticated_player_id] = (
                lambda: "seeded-7"
            )
            self.assertTrue(
                self.client.get("/api/v1/admin/status").json()["is_admin"]
            )
            seeded_profile = self.client.get("/api/v1/profile").json()
            self.assertEqual(seeded_profile["username"], "F1FC")
            self.assertEqual(seeded_profile["team_name"], "F1FC")
            self.assertEqual(seeded_profile["crest_id"], "f1fc")
        with patch.dict("os.environ", {"ADMIN_PLAYER_IDS": "unrelated-admin"}):
            self.app.dependency_overrides[get_authenticated_player_id] = (
                lambda: "seeded-7"
            )
            self.assertFalse(
                self.client.get("/api/v1/admin/status").json()["is_admin"]
            )
        self.app.dependency_overrides[get_authenticated_player_id] = (
            lambda: "player-one"
        )

        with self.assertRaisesRegex(ValueError, "missing usernames"):
            seed_default_profiles(
                self.app.state.database_path,
                {username: "different-" + str(index)
                 for index, (username, _, _) in enumerate(DEFAULT_PROFILES[:-1])},
            )
        with self.assertRaisesRegex(ValueError, "distinct Unity player ID"):
            seed_default_profiles(
                self.app.state.database_path,
                {username: "duplicate-id" for username, _, _ in DEFAULT_PROFILES},
            )
        with self.assertRaisesRegex(ValueError, "Refusing to overwrite"):
            seed_default_profiles(self.app.state.database_path, mapping)

    def test_existing_database_schema_is_migrated_for_league_history(self) -> None:
        legacy_path = Path(self.temp_directory.name) / "legacy.sqlite3"
        legacy_connection = sqlite3.connect(legacy_path)
        try:
            legacy_connection.execute(
                """
                CREATE TABLE matches (
                    match_id TEXT PRIMARY KEY,
                    home_club_id TEXT NOT NULL,
                    away_club_id TEXT NOT NULL,
                    summary_json TEXT NOT NULL,
                    invoice_json TEXT NOT NULL,
                    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                )
                """
            )
        finally:
            legacy_connection.close()

        connection = connect_database(legacy_path)
        columns = {
            row["name"] for row in connection.execute("PRAGMA table_info(matches)")
        }
        connection.close()
        self.assertIn("season_number", columns)
        self.assertIn("matchday", columns)
        connection = connect_database(legacy_path)
        profile_columns = {
            row["name"] for row in connection.execute("PRAGMA table_info(profiles)")
        }
        connection.close()
        self.assertIn("coach_id", profile_columns)
        self.assertIn("lineup_ids", profile_columns)
        self.assertIn("achievements", profile_columns)
        self.assertIn("owned_packs", profile_columns)

    def test_admin_tools_require_allowlisted_identity_and_audit_changes(self) -> None:
        connection = connect_database(self.app.state.database_path)
        target = ensure_player_profile(connection, "player-two")
        target_club_id = target["club_id"]
        connection.commit()
        connection.close()

        with patch.dict("os.environ", {"ADMIN_PLAYER_IDS": "player-one"}):
            self.assertTrue(self.client.get("/api/v1/admin/status").json()["is_admin"])
            profiles = self.client.get(
                "/api/v1/admin/profiles", params={"query": "player-two"}
            )
            self.assertEqual(profiles.status_code, 200)
            self.assertEqual(profiles.json()[0]["player_id"], "player-two")
            catalog = self.client.get("/api/v1/admin/catalog")
            self.assertEqual(catalog.status_code, 200)
            self.assertGreaterEqual(len(catalog.json()["coaches"]), 40)

            increased = self.client.post(
                "/api/v1/admin/profiles/player-two/money/change",
                json={"amount": 25},
            )
            self.assertEqual(increased.status_code, 200)
            self.assertEqual(increased.json()["club_funds"], 50_000_025)

            added = self.client.post(
                "/api/v1/admin/profiles/player-two/squad",
                json={
                    "player_id": "player_admin_added",
                    "player_name": "Admin Added",
                    "position": "CM",
                    "rating": 90,
                },
            )
            self.assertEqual(added.status_code, 200)
            self.assertGreaterEqual(added.json()["contract_seasons"], 1)
            self.assertLessEqual(added.json()["contract_seasons"], 4)
            self.assertEqual(added.json()["overall_rating"], 90)
            admin_profile = self.client.get(
                "/api/v1/admin/profiles/player-two"
            ).json()
            self.assertEqual(admin_profile["squad"][0]["id"], "player_admin_added")

            duplicate = self.client.post(
                "/api/v1/admin/profiles/player-two/squad",
                json={
                    "player_id": "player_admin_added",
                    "player_name": "Admin Added",
                    "position": "CM",
                    "rating": 90,
                },
            )
            self.assertEqual(duplicate.status_code, 409)

            self.assertEqual(
                self.client.put(
                    "/api/v1/admin/profiles/player-two/coach",
                    json={"coach_id": "coach_pep_guardiola"},
                ).json()["coach_id"],
                "coach_pep_guardiola",
            )
            sponsor = self.client.put(
                "/api/v1/admin/profiles/player-two/sponsor",
                json={"sponsor_id": "SP02"},
            )
            self.assertEqual(sponsor.json()["sponsor_seasons_left"], 3)
            granted_packs = self.client.post(
                "/api/v1/admin/profiles/player-two/packs",
                json={"pack_type_id": "premium", "count": 3},
            )
            self.assertEqual(granted_packs.status_code, 200)
            self.assertEqual(granted_packs.json()["count"], 3)
            admin_profile = self.client.get(
                "/api/v1/admin/profiles/player-two"
            ).json()
            self.assertEqual(admin_profile["owned_packs"], [0, 3, 0, 0, 0])

            connection = connect_database(self.app.state.database_path)
            connection.execute(
                "UPDATE profiles SET lineup_ids = ?, player_statuses = ?, "
                "potentials = ? WHERE player_id = ?",
                (
                    '["player_admin_added"]',
                    '[{"playerId":"player_admin_added","matchesRemaining":2,"reason":"Injury"}]',
                    '[{"playerId":"player_admin_added","potential":110}]',
                    "player-two",
                ),
            )
            connection.commit()
            connection.close()
            removed = self.client.delete(
                "/api/v1/admin/profiles/player-two/squad/player_admin_added"
            )
            self.assertEqual(removed.status_code, 200)
            connection = connect_database(self.app.state.database_path)
            cleaned = connection.execute(
                "SELECT lineup_ids, player_statuses, potentials "
                "FROM profiles WHERE player_id = ?",
                ("player-two",),
            ).fetchone()
            audit_count = connection.execute(
                "SELECT COUNT(*) AS count FROM admin_audit_log "
                "WHERE target_player_id = ?",
                ("player-two",),
            ).fetchone()["count"]
            connection.close()
            self.assertEqual(cleaned["lineup_ids"], "[]")
            self.assertEqual(cleaned["player_statuses"], "[]")
            self.assertEqual(cleaned["potentials"], "[]")
            self.assertGreaterEqual(audit_count, 5)

            reset = self.client.post(
                "/api/v1/admin/profiles/player-two/reset"
            )
            self.assertEqual(reset.status_code, 200)
            connection = connect_database(self.app.state.database_path)
            reset_profile = connection.execute(
                "SELECT club_id, team_name, coach_id, sponsor_id, "
                "active_sponsors, club_funds, owned_packs "
                "FROM profiles WHERE player_id = ?",
                ("player-two",),
            ).fetchone()
            connection.close()
            self.assertEqual(reset_profile["club_id"], target_club_id)
            self.assertEqual(reset_profile["team_name"], "New Club")
            self.assertEqual(reset_profile["coach_id"], "")
            self.assertEqual(reset_profile["sponsor_id"], "")
            self.assertEqual(reset_profile["active_sponsors"], "[]")
            self.assertEqual(reset_profile["club_funds"], 0)
            self.assertEqual(
                json.loads(reset_profile["owned_packs"]),
                [0, 0, 0, 0, 0],
            )

            self.assertEqual(
                self.client.put(
                    "/api/v1/admin/profiles/player-two/money",
                    json={"amount": -50},
                ).json()["club_funds"],
                0,
            )

            restarted = self.client.post("/api/v1/admin/league/restart")
            self.assertEqual(restarted.status_code, 200)
            self.assertEqual(restarted.json()["season"], 1)
            connection = connect_database(self.app.state.database_path)
            states = connection.execute(
                "SELECT tier, season_number, next_matchday "
                "FROM league_seasons ORDER BY tier"
            ).fetchall()
            connection.close()
            self.assertEqual(len(states), 2)
            self.assertTrue(
                all(
                    row["season_number"] == 1 and row["next_matchday"] == 0
                    for row in states
                )
            )

    def test_admin_endpoints_reject_non_allowlisted_players(self) -> None:
        with patch.dict("os.environ", {"ADMIN_PLAYER_IDS": "someone-else"}):
            self.assertFalse(self.client.get("/api/v1/admin/status").json()["is_admin"])
            response = self.client.post(
                "/api/v1/admin/profiles/player-two/money/change",
                json={"amount": 10},
            )
            self.assertEqual(response.status_code, 403)
            grant = self.client.post(
                "/api/v1/admin/profiles/player-two/packs",
                json={"pack_type_id": "standard", "count": 1},
            )
            self.assertEqual(grant.status_code, 403)

    def test_admin_mutations_fail_when_allowlist_is_not_configured(self) -> None:
        with patch.dict("os.environ", {}, clear=True):
            response = self.client.post(
                "/api/v1/admin/profiles/player-two/money/change",
                json={"amount": 10},
            )
            self.assertEqual(response.status_code, 503)

    def test_admin_bought_player_contract_expires_with_profile_cleanup(self) -> None:
        connection = connect_database(self.app.state.database_path)
        target = ensure_player_profile(connection, "player-two")
        connection.execute(
            "UPDATE league_clubs SET tier = 'tier1' "
            "WHERE tier = 'tier2' AND is_ai = 1 "
            "AND club_id != (SELECT club_id FROM league_clubs "
            "WHERE tier = 'tier2' AND is_ai = 1 ORDER BY club_id LIMIT 1)"
        )
        connection.commit()
        connection.close()

        with patch.dict("os.environ", {"ADMIN_PLAYER_IDS": "player-one"}):
            added = self.client.post(
                "/api/v1/admin/profiles/player-two/squad",
                json={
                    "player_id": "short_contract",
                    "player_name": "Short Contract",
                    "position": "ST",
                    "rating": 80,
                },
            )
            self.assertEqual(added.status_code, 200)
            connection = connect_database(self.app.state.database_path)
            connection.execute(
                "UPDATE squad_contracts SET seasons_left = 1 "
                "WHERE owner_player_id = ? AND player_id = ?",
                ("player-two", "short_contract"),
            )
            connection.execute(
                "UPDATE profiles SET lineup_ids = ?, player_statuses = ?, "
                "potentials = ? WHERE player_id = ?",
                (
                    '["short_contract"]',
                    '[{"playerId":"short_contract","matchesRemaining":2,"reason":"Injury"}]',
                    '[{"playerId":"short_contract","potential":100}]',
                    "player-two",
                ),
            )
            connection.commit()
            connection.close()

            schedule = self.client.get("/api/v1/league/fixtures").json()
            self.assertEqual(schedule["total_matchdays"], 3)
            for matchday in range(schedule["total_matchdays"]):
                response = self.client.post(
                    "/api/v1/league/matchdays/next",
                    json={"expected_matchday": matchday},
                )
                self.assertEqual(response.status_code, 200)
            rollover = self.client.post(
                "/api/v1/league/next-season",
                json={"expected_season": schedule["season"]},
            )
            self.assertEqual(rollover.status_code, 200)
            self.assertEqual(
                rollover.json()["expired_players"]["player-two"],
                ["Short Contract"],
            )

        connection = connect_database(self.app.state.database_path)
        remaining_player = connection.execute(
            "SELECT COUNT(*) AS count FROM club_squad WHERE owner_player_id = ?",
            ("player-two",),
        ).fetchone()["count"]
        remaining_profile = connection.execute(
            "SELECT lineup_ids, player_statuses, potentials, profile_season "
            "FROM profiles WHERE player_id = ?",
            ("player-two",),
        ).fetchone()
        connection.close()
        self.assertEqual(remaining_player, 0)
        self.assertEqual(remaining_profile["lineup_ids"], "[]")
        self.assertEqual(remaining_profile["player_statuses"], "[]")
        self.assertEqual(remaining_profile["potentials"], "[]")
        self.assertEqual(remaining_profile["profile_season"], 2)

    def test_free_choice_match_is_standings_neutral_and_kept_in_history(self) -> None:
        opponent = self.client.get("/api/v1/opponents").json()[0]
        profile = self.client.get("/api/v1/profile").json()
        connection = connect_database(self.app.state.database_path)
        connection.execute(
            "UPDATE profiles SET active_sponsors = ? WHERE player_id = ?",
            (
                '[{"sponsor_name":"Test Sponsor","payout":5000000,'
                '"seasons_remaining":2}]',
                "player-one",
            ),
        )
        connection.commit()
        connection.close()
        before = self.client.get("/api/v1/league/standings").json()
        response = self.client.post(
            "/api/v1/matches", json={"opponent_id": opponent["opponent_id"]}
        )

        self.assertEqual(response.status_code, 201)
        result = response.json()
        self.assertEqual(result["match"]["home_team_name"], "New Club")
        self.assertEqual(result["match"]["away_team_name"], opponent["team_name"])
        self.assertEqual(result["club_funds"], profile["club_funds"])
        self.assertEqual(result["financial_invoice"]["total_commercial_revenue"], 0)
        self.assertEqual(
            self.client.get("/api/v1/profile").json()["club_funds"],
            profile["club_funds"],
        )

        history = self.client.get("/api/v1/matches").json()
        self.assertEqual(len(history), 1)
        self.assertEqual(history[0]["match_id"], result["match_id"])

        standings = self.client.get("/api/v1/league/standings").json()
        self.assertEqual(standings["season"], before["season"])
        self.assertEqual(standings["standings"], before["standings"])

    def test_round_robin_schedule_advances_once_per_matchday(self) -> None:
        schedule = self.client.get("/api/v1/league/fixtures").json()
        self.assertEqual(schedule["season"], 1)
        self.assertEqual(schedule["total_matchdays"], 21)
        self.assertEqual(len(schedule["fixtures"]), 210)
        self.assertFalse(schedule["finished"])

        pairs = [
            frozenset((fixture["home_club_id"], fixture["away_club_id"]))
            for fixture in schedule["fixtures"]
        ]
        self.assertEqual(len(set(pairs)), len(pairs))
        participant_ids = {
            club_id for fixture in schedule["fixtures"]
            for club_id in (fixture["home_club_id"], fixture["away_club_id"])
        }
        self.assertEqual(len(participant_ids), 21)
        for matchday in range(schedule["total_matchdays"]):
            round_fixtures = [
                fixture for fixture in schedule["fixtures"]
                if fixture["matchday"] == matchday
            ]
            appearances = [
                club_id for fixture in round_fixtures
                for club_id in (fixture["home_club_id"], fixture["away_club_id"])
            ]
            self.assertEqual(len(round_fixtures), 10)
            self.assertEqual(len(appearances), len(set(appearances)))
            self.assertEqual(len(appearances), 20)

        response = self.client.post(
            "/api/v1/league/matchdays/next",
            json={"expected_matchday": 0},
        )
        self.assertEqual(response.status_code, 200)
        result = response.json()
        self.assertEqual(result["matchday"], 0)
        self.assertEqual(result["next_matchday"], 1)
        self.assertEqual(len(result["played"]), 10)
        self.assertEqual(
            result["club_funds"],
            self.client.get("/api/v1/profile").json()["club_funds"],
        )

        stale = self.client.post(
            "/api/v1/league/matchdays/next",
            json={"expected_matchday": 0},
        )
        self.assertEqual(stale.status_code, 409)

        updated = self.client.get("/api/v1/league/fixtures").json()
        self.assertEqual(updated["next_matchday"], 1)
        self.assertEqual(
            sum(fixture["played"] for fixture in updated["fixtures"]), 10
        )

    def test_completed_season_pays_prizes_and_starts_fresh_schedule(self) -> None:
        self.client.get("/api/v1/profile")
        connection = connect_database(self.app.state.database_path)
        ai_club = connection.execute(
            "SELECT club_id FROM league_clubs "
            "WHERE tier = 'tier2' AND is_ai = 1 ORDER BY club_id LIMIT 1"
        ).fetchone()
        connection.execute(
            "UPDATE league_clubs SET tier = 'tier1' "
            "WHERE tier = 'tier2' AND is_ai = 1 "
            "AND club_id != ?",
            (ai_club["club_id"],),
        )
        connection.commit()
        connection.close()

        schedule = self.client.get("/api/v1/league/fixtures").json()
        self.assertEqual(schedule["total_matchdays"], 1)
        played = self.client.post(
            "/api/v1/league/matchdays/next",
            json={"expected_matchday": 0},
        )
        self.assertEqual(played.status_code, 200)
        self.assertTrue(played.json()["finished"])

        before = self.client.get("/api/v1/profile").json()["club_funds"]
        standings = self.client.get("/api/v1/league/standings").json()["standings"]
        user_position = next(
            row["position"]
            for row in standings
            if row["team_name"] == "New Club"
        )
        response = self.client.post(
            "/api/v1/league/next-season",
            json={"expected_season": 1},
        )
        self.assertEqual(response.status_code, 200)
        result = response.json()
        self.assertEqual(result["season"], 2)
        self.assertEqual(result["next_matchday"], 0)
        self.assertEqual(result["total_matchdays"], 1)
        expected_prize = (200_000_000, 120_000_000)[user_position - 1]
        after = self.client.get("/api/v1/profile").json()["club_funds"]
        self.assertEqual(after - before, expected_prize)
        retry = self.client.post(
            "/api/v1/league/next-season",
            json={"expected_season": 1},
        )
        self.assertEqual(retry.status_code, 409)
        self.assertEqual(
            self.client.get("/api/v1/profile").json()["club_funds"], after
        )
        fresh_table = self.client.get("/api/v1/league/standings").json()
        self.assertTrue(all(row["points"] == 0 for row in fresh_table["standings"]))

    def test_rejects_non_ai_opponent(self) -> None:
        response = self.client.post(
            "/api/v1/matches", json={"opponent_id": "some-other-player"}
        )
        self.assertEqual(response.status_code, 404)
        self.assertEqual(self.client.get("/api/v1/matches").json(), [])

    def test_health_endpoint_does_not_require_authentication(self) -> None:
        self.app.dependency_overrides.clear()
        response = self.client.get("/health")
        self.assertEqual(response.status_code, 200)
        self.assertEqual(response.json(), {"status": "ok"})


if __name__ == "__main__":
    unittest.main()
