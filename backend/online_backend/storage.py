"""SQLite schema, small migrations and the helpers every endpoint shares."""
from __future__ import annotations

import json
import random
import re
import sqlite3
from pathlib import Path

from .packs import club_ratings

START_FUNDS = 50_000_000
NEW_CLUB_NAME = "New Club"

SCHEMA = """
CREATE TABLE IF NOT EXISTS league_clubs (
    club_id TEXT PRIMARY KEY, club_name TEXT NOT NULL, tier TEXT NOT NULL,
    is_ai INTEGER NOT NULL DEFAULT 0, overall_rating INTEGER NOT NULL DEFAULT 60
);
CREATE TABLE IF NOT EXISTS profiles (
    player_id TEXT PRIMARY KEY, username TEXT, team_name TEXT NOT NULL, crest_id TEXT NOT NULL DEFAULT '',
    club_id TEXT NOT NULL, club_funds INTEGER NOT NULL DEFAULT 0,
    owned_packs TEXT NOT NULL DEFAULT '[0,0,0,0,0]', coach_id TEXT NOT NULL DEFAULT '',
    sponsor_id TEXT NOT NULL DEFAULT '', sponsor_seasons_left INTEGER NOT NULL DEFAULT 0,
    active_sponsors TEXT NOT NULL DEFAULT '[]', formation_choice TEXT NOT NULL DEFAULT '',
    tactic TEXT NOT NULL DEFAULT 'Balanced', lineup_ids TEXT NOT NULL DEFAULT '[]',
    achievements TEXT NOT NULL DEFAULT '[]', player_statuses TEXT NOT NULL DEFAULT '[]',
    potentials TEXT NOT NULL DEFAULT '[]', profile_season INTEGER NOT NULL DEFAULT 1
);
CREATE TABLE IF NOT EXISTS club_squad (
    owner_player_id TEXT NOT NULL, player_id TEXT NOT NULL, player_name TEXT NOT NULL,
    position TEXT NOT NULL, rating INTEGER NOT NULL, has_rating INTEGER NOT NULL DEFAULT 1,
    PRIMARY KEY (owner_player_id, player_id)
);
CREATE TABLE IF NOT EXISTS squad_contracts (
    owner_player_id TEXT NOT NULL, player_id TEXT NOT NULL, seasons_left INTEGER NOT NULL,
    PRIMARY KEY (owner_player_id, player_id)
);
CREATE TABLE IF NOT EXISTS league_seasons (
    tier TEXT PRIMARY KEY, season_number INTEGER NOT NULL DEFAULT 1, next_matchday INTEGER NOT NULL DEFAULT 0
);
CREATE TABLE IF NOT EXISTS league_fixtures (
    tier TEXT NOT NULL, season_number INTEGER NOT NULL, matchday INTEGER NOT NULL,
    home_club_id TEXT NOT NULL, away_club_id TEXT NOT NULL, played INTEGER NOT NULL DEFAULT 0,
    home_goals INTEGER, away_goals INTEGER
);
CREATE TABLE IF NOT EXISTS matches (
    match_id TEXT PRIMARY KEY, home_club_id TEXT NOT NULL, away_club_id TEXT NOT NULL,
    summary_json TEXT NOT NULL, invoice_json TEXT NOT NULL, created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE TABLE IF NOT EXISTS admin_audit_log (
    id INTEGER PRIMARY KEY AUTOINCREMENT, actor_player_id TEXT NOT NULL, target_player_id TEXT,
    action TEXT NOT NULL, details_json TEXT NOT NULL DEFAULT '{}', created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);
"""

# Columns added after the first release: existing databases get them on connect.
MIGRATIONS = {
    "matches": {"season_number": "INTEGER", "matchday": "INTEGER", "player_id": "TEXT"},
    "profiles": {
        "coach_id": "TEXT NOT NULL DEFAULT ''", "lineup_ids": "TEXT NOT NULL DEFAULT '[]'",
        "achievements": "TEXT NOT NULL DEFAULT '[]'", "owned_packs": "TEXT NOT NULL DEFAULT '[0,0,0,0,0]'",
        "player_statuses": "TEXT NOT NULL DEFAULT '[]'", "potentials": "TEXT NOT NULL DEFAULT '[]'",
        "profile_season": "INTEGER NOT NULL DEFAULT 1",
    },
}


def connect_database(path: str | Path) -> sqlite3.Connection:
    connection = sqlite3.connect(str(path), timeout=30)
    connection.row_factory = sqlite3.Row
    connection.execute("PRAGMA foreign_keys = ON")
    connection.executescript(SCHEMA)
    for table, columns in MIGRATIONS.items():
        have = {row["name"] for row in connection.execute(f"PRAGMA table_info({table})")}
        for name, ddl in columns.items():
            if name not in have:
                connection.execute(f"ALTER TABLE {table} ADD COLUMN {name} {ddl}")
    _seed_ai_clubs(connection)
    connection.commit()
    return connection


def _seed_ai_clubs(connection: sqlite3.Connection) -> None:
    if connection.execute("SELECT 1 FROM league_clubs WHERE is_ai = 1 LIMIT 1").fetchone():
        return
    ranked = sorted(club_ratings().items(), key=lambda kv: (-kv[1], kv[0]))[:40]
    for index, (name, rating) in enumerate(ranked):
        slug = re.sub(r"[^a-z0-9]+", "_", name.lower()).strip("_")
        connection.execute(
            "INSERT INTO league_clubs (club_id, club_name, tier, is_ai, overall_rating) VALUES (?, ?, ?, 1, ?)",
            ("ai_" + slug, name, "tier1" if index < 20 else "tier2", rating),
        )


def ensure_player_profile(connection: sqlite3.Connection, player_id: str) -> sqlite3.Row:
    row = connection.execute("SELECT * FROM profiles WHERE player_id = ?", (player_id,)).fetchone()
    if row is None:
        club_id = "human_" + player_id
        connection.execute(
            "INSERT OR IGNORE INTO league_clubs (club_id, club_name, tier, is_ai, overall_rating) VALUES (?, ?, 'tier2', 0, 0)",
            (club_id, NEW_CLUB_NAME),
        )
        connection.execute(
            "INSERT INTO profiles (player_id, team_name, club_id, club_funds) VALUES (?, ?, ?, ?)",
            (player_id, NEW_CLUB_NAME, club_id, START_FUNDS),
        )
        connection.commit()
        row = connection.execute("SELECT * FROM profiles WHERE player_id = ?", (player_id,)).fetchone()
    return row


# ---------- squad helpers ----------

def squad_rows(connection, player_id) -> list[sqlite3.Row]:
    return connection.execute(
        "SELECT * FROM club_squad WHERE owner_player_id = ? ORDER BY rating DESC, player_id", (player_id,)
    ).fetchall()


def overall_rating(connection, player_id) -> int:
    ratings = [r["rating"] for r in squad_rows(connection, player_id)][:11]
    return round(sum(ratings) / len(ratings)) if ratings else 0


def refresh_club_rating(connection, player_id) -> int:
    rating = overall_rating(connection, player_id)
    connection.execute(
        "UPDATE league_clubs SET overall_rating = ? WHERE club_id = (SELECT club_id FROM profiles WHERE player_id = ?)",
        (rating, player_id),
    )
    return rating


def owns_player(connection, owner, player_id) -> bool:
    return connection.execute(
        "SELECT 1 FROM club_squad WHERE owner_player_id = ? AND player_id = ?", (owner, player_id)
    ).fetchone() is not None


def add_player(connection, owner, player_id, name, position, rating, seasons=None) -> int:
    seasons = seasons or random.randint(1, 4)
    connection.execute(
        "INSERT INTO club_squad (owner_player_id, player_id, player_name, position, rating, has_rating) VALUES (?, ?, ?, ?, ?, 1)",
        (owner, player_id, name, position, rating),
    )
    connection.execute(
        "INSERT INTO squad_contracts (owner_player_id, player_id, seasons_left) VALUES (?, ?, ?)", (owner, player_id, seasons)
    )
    refresh_club_rating(connection, owner)
    return seasons


def remove_player(connection, owner, player_id) -> None:
    """Remove a player and every trace of him from the saved lineup, statuses and potentials."""
    connection.execute("DELETE FROM club_squad WHERE owner_player_id = ? AND player_id = ?", (owner, player_id))
    connection.execute("DELETE FROM squad_contracts WHERE owner_player_id = ? AND player_id = ?", (owner, player_id))
    row = connection.execute(
        "SELECT lineup_ids, player_statuses, potentials FROM profiles WHERE player_id = ?", (owner,)
    ).fetchone()
    if row is not None:
        lineup = [i for i in json.loads(row["lineup_ids"] or "[]") if i != player_id]
        statuses = [s for s in json.loads(row["player_statuses"] or "[]") if s.get("playerId") != player_id]
        potentials = [s for s in json.loads(row["potentials"] or "[]") if s.get("playerId") != player_id]
        connection.execute(
            "UPDATE profiles SET lineup_ids = ?, player_statuses = ?, potentials = ? WHERE player_id = ?",
            (json.dumps(lineup), json.dumps(statuses), json.dumps(potentials), owner),
        )
    refresh_club_rating(connection, owner)


def profile_dict(connection, row, include_squad=False) -> dict:
    club = connection.execute("SELECT * FROM league_clubs WHERE club_id = ?", (row["club_id"],)).fetchone()
    data = {
        "player_id": row["player_id"], "username": row["username"], "team_name": row["team_name"],
        "crest_id": row["crest_id"], "club_id": row["club_id"], "club_funds": row["club_funds"],
        "owned_packs": json.loads(row["owned_packs"]), "coach_id": row["coach_id"],
        "sponsor_id": row["sponsor_id"], "sponsor_seasons_left": row["sponsor_seasons_left"],
        "active_sponsors": json.loads(row["active_sponsors"]), "formation_choice": row["formation_choice"],
        "tactic": row["tactic"], "lineup_ids": json.loads(row["lineup_ids"]),
        "achievements": json.loads(row["achievements"]), "tier": club["tier"] if club else "tier2",
        "overall_rating": overall_rating(connection, row["player_id"]), "season": row["profile_season"],
    }
    if include_squad:
        contracts = {
            c["player_id"]: c["seasons_left"]
            for c in connection.execute("SELECT * FROM squad_contracts WHERE owner_player_id = ?", (row["player_id"],))
        }
        data["squad"] = [
            {"id": s["player_id"], "name": s["player_name"], "position": s["position"], "rating": s["rating"],
             "seasons_left": contracts.get(s["player_id"], 0)}
            for s in squad_rows(connection, row["player_id"])
        ]
    return data
