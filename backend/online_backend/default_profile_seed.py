"""One-off seeding of the default club profiles (the clubs from the web game).

Each club is mapped to the Unity player id of its owner. Seeded clubs start empty (no money, packs,
coach or sponsor) and are NOT admins; admin rights come only from ADMIN_PLAYER_IDS.
"""
from __future__ import annotations

import json
from pathlib import Path

from .storage import connect_database, ensure_player_profile

# (username, club name, crest id). Index 7 is F1FC.
DEFAULT_PROFILES = [
    ("Sylford FC", "Sylford FC", "sylford_fc"),
    ("Umended FC", "Umended FC", "unmended_fc"),
    ("Black Steel Rovers", "Black Steel Rovers", "black_steel_rovers"),
    ("Nightmare FC", "Nightmare FC", "nightmare_fc"),
    ("Loki", "Loki", "loki"),
    ("Riat FC", "Riat FC", "riat_fc"),
    ("King Crimson", "King Crimson", "crimson"),
    ("F1FC", "F1FC", "f1fc"),
    ("JAI", "JAI", "jai"),
    ("Fantacy Club", "Fantacy Club", "fantacy"),
    ("Adams Club", "Adams Club", "adams"),
    ("Calc FC", "Calc FC", "calc_fc"),
    ("Manshine City", "Manshine City", "manshine_city"),
    ("Winnie FC", "Winnie FC", "winnie_fc"),
    ("Wron FC", "Wron FC", "wron_fc"),
    ("Sporty FC", "Sporty FC", "sporty_fc"),
    ("Dark Warriors", "Dark Warriors", "dark_warriors"),
    ("Bastard FC", "Bastard FC", "bastard_fc"),
    ("Galaxy 11", "Galaxy 11", "galaxy_11"),
    ("NLTF FC", "NLTF FC", "nltf_fc"),
    ("Salvator FC", "Salvator FC", "salvator_fc"),
]


def seed_default_profiles(database_path: str | Path, username_to_player_id: dict[str, str]) -> int:
    missing = [u for u, _, _ in DEFAULT_PROFILES if u not in username_to_player_id]
    if missing:
        raise ValueError("Mapping is missing usernames: " + ", ".join(missing))
    ids = [username_to_player_id[u] for u, _, _ in DEFAULT_PROFILES]
    if len(set(ids)) != len(ids) or any(not isinstance(i, str) or not i for i in ids):
        raise ValueError("Every username needs its own distinct Unity player ID.")

    connection = connect_database(database_path)
    try:
        marks = ",".join("?" * len(ids))
        existing = connection.execute(
            f"SELECT player_id FROM profiles WHERE player_id IN ({marks}) OR username IS NOT NULL", ids
        ).fetchall()
        if existing:
            raise ValueError("Refusing to overwrite existing profiles: " + ", ".join(r["player_id"] for r in existing))
        for username, club_name, crest_id in DEFAULT_PROFILES:
            profile = ensure_player_profile(connection, username_to_player_id[username])
            connection.execute(
                "UPDATE profiles SET username = ?, team_name = ?, crest_id = ?, club_funds = 0, owned_packs = ?, "
                "coach_id = '', sponsor_id = '', formation_choice = '', tactic = 'Balanced', lineup_ids = '[]', "
                "achievements = '[]' WHERE player_id = ?",
                (username, club_name, crest_id, json.dumps([0, 0, 0, 0, 0]), profile["player_id"]),
            )
            connection.execute("UPDATE league_clubs SET club_name = ? WHERE club_id = ?", (club_name, profile["club_id"]))
        connection.commit()
    finally:
        connection.close()
    return len(DEFAULT_PROFILES)
