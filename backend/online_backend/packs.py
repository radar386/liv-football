"""Pack types, the player catalog (AllPlayers.csv) and the pull logic."""
from __future__ import annotations

import csv
import math
import random
from pathlib import Path

CATALOG_PATH = Path(__file__).resolve().parents[2] / "AllPlayers.csv"

TIER_NAMES = ("Bronze", "Silver", "Gold", "Elite")
TIER_MIN = (0, 80, 95, 105)          # Bronze <80, Silver 80-94, Gold 95-104, Elite 105+
DUPLICATE_REFUND_PERCENT = 30

PACK_TYPES = [
    {"type_id": "standard", "name": "Standard Pack", "price": 2_000_000, "min_rating": 0,
     "chances": [80, 18, 2, 0], "guarantee_min_rating": 80},
    {"type_id": "premium", "name": "Premium Pack", "price": 10_000_000, "min_rating": 80,
     "chances": [0, 75, 22, 3], "guarantee_min_rating": 95},
    {"type_id": "elite", "name": "Elite Pack", "price": 40_000_000, "min_rating": 95,
     "chances": [0, 0, 85, 15], "guarantee_min_rating": 105},
    {"type_id": "world_class", "name": "World Class Pack", "price": 25_000_000, "min_rating": 100,
     "chances": [0, 0, 80, 20], "guarantee_min_rating": 105},
    {"type_id": "superstar", "name": "Superstar Pack", "price": 100_000_000, "min_rating": 110,
     "chances": [0, 0, 0, 100], "guarantee_min_rating": 110},
]
PACK_BY_ID = {p["type_id"]: p for p in PACK_TYPES}


def market_value(rating: int) -> int:
    """Same formula as PlayerCatalog.ValueOf in the Unity project."""
    return int(round(500_000 * 1.09 ** (rating - 60) / 100_000)) * 100_000


def tier_index(rating: int) -> int:
    for tier in (3, 2, 1):
        if rating >= TIER_MIN[tier]:
            return tier
    return 0


def _load_players() -> list[dict]:
    with open(CATALOG_PATH, newline="", encoding="utf-8") as handle:
        return [
            {"id": r["id"], "name": r["name"], "club": r["club"],
             "position": r["position"], "rating": int(r["rating"])}
            for r in csv.DictReader(handle)
        ]


PLAYERS: list[dict] = _load_players()


def club_ratings() -> dict[str, int]:
    """Rating of each catalog club: the mean of its best 11 players."""
    by_club: dict[str, list[int]] = {}
    for player in PLAYERS:
        by_club.setdefault(player["club"], []).append(player["rating"])
    return {club: round(sum(sorted(r, reverse=True)[:11]) / min(11, len(r))) for club, r in by_club.items()}


def _pick(rng: random.Random, tier: int, min_rating: int) -> dict:
    pool = [p for p in PLAYERS if tier_index(p["rating"]) == tier and p["rating"] >= min_rating]
    if not pool:
        pool = [p for p in PLAYERS if p["rating"] >= min_rating]
    return rng.choice(pool)


def draw(pack: dict, count: int, rng: random.Random | None = None) -> list[dict]:
    """Draw `count` players. An x10 always holds a player at the guarantee rating or better."""
    rng = rng or random.SystemRandom()
    picks = []
    for _ in range(count):
        roll, tier = rng.uniform(0, 100), 0
        running = 0.0
        for index, chance in enumerate(pack["chances"]):
            running += chance
            if roll < running:
                tier = index
                break
        picks.append(_pick(rng, tier, pack["min_rating"]))
    if count >= 10 and not any(p["rating"] >= pack["guarantee_min_rating"] for p in picks):
        pool = [p for p in PLAYERS if p["rating"] >= pack["guarantee_min_rating"]]
        picks[-1] = rng.choice(pool)
    return picks
