"""Round-robin league per tier: fixtures, matchdays, standings and season rollover."""
from __future__ import annotations

import json

from MatchEventSimulator import MatchEventSimulator
from SponsorRewardManager import SponsorRewardManager

from .storage import refresh_club_rating, remove_player

TIERS = ("tier1", "tier2")
PRIZES = [200_000_000, 120_000_000, 80_000_000, 50_000_000, 30_000_000, 20_000_000]
DEFAULT_PRIZE = 10_000_000


class LeagueError(Exception):
    def __init__(self, status: int, detail: str) -> None:
        super().__init__(detail)
        self.status, self.detail = status, detail


def ensure_season(connection, tier):
    connection.execute("INSERT OR IGNORE INTO league_seasons (tier, season_number, next_matchday) VALUES (?, 1, 0)", (tier,))
    return connection.execute("SELECT * FROM league_seasons WHERE tier = ?", (tier,)).fetchone()


def round_robin(club_ids: list[str]) -> list[list[tuple[str, str]]]:
    """Circle method. An odd number of clubs gives each club one bye."""
    teams: list[str | None] = list(club_ids)
    if len(teams) % 2:
        teams.append(None)
    n = len(teams)
    rounds = []
    for r in range(n - 1):
        pairs = []
        for i in range(n // 2):
            a, b = teams[i], teams[n - 1 - i]
            if a is None or b is None:
                continue
            pairs.append((a, b) if (r + i) % 2 == 0 else (b, a))
        rounds.append(pairs)
        teams = [teams[0]] + [teams[-1]] + teams[1:-1]
    return rounds


def ensure_fixtures(connection, tier, season) -> None:
    if connection.execute(
        "SELECT 1 FROM league_fixtures WHERE tier = ? AND season_number = ? LIMIT 1", (tier, season)
    ).fetchone():
        return
    ids = [r["club_id"] for r in connection.execute("SELECT club_id FROM league_clubs WHERE tier = ? ORDER BY club_id", (tier,))]
    if len(ids) < 2:
        return
    for matchday, pairs in enumerate(round_robin(ids)):
        for home, away in pairs:
            connection.execute(
                "INSERT INTO league_fixtures (tier, season_number, matchday, home_club_id, away_club_id) VALUES (?, ?, ?, ?, ?)",
                (tier, season, matchday, home, away),
            )


def total_matchdays(connection, tier, season) -> int:
    row = connection.execute(
        "SELECT MAX(matchday) AS m FROM league_fixtures WHERE tier = ? AND season_number = ?", (tier, season)
    ).fetchone()
    return 0 if row["m"] is None else row["m"] + 1


def club_names(connection) -> dict[str, str]:
    return {r["club_id"]: r["club_name"] for r in connection.execute("SELECT club_id, club_name FROM league_clubs")}


def tier_of(connection, player_id) -> str:
    return connection.execute(
        "SELECT c.tier FROM profiles p JOIN league_clubs c ON c.club_id = p.club_id WHERE p.player_id = ?", (player_id,)
    ).fetchone()["tier"]


def state(connection, tier) -> dict:
    season = ensure_season(connection, tier)
    ensure_fixtures(connection, tier, season["season_number"])
    total = total_matchdays(connection, tier, season["season_number"])
    return {"tier": tier, "season": season["season_number"], "next_matchday": season["next_matchday"],
            "total_matchdays": total, "finished": total > 0 and season["next_matchday"] >= total}


def fixtures(connection, tier) -> dict:
    s = state(connection, tier)
    names = club_names(connection)
    rows = connection.execute(
        "SELECT * FROM league_fixtures WHERE tier = ? AND season_number = ? ORDER BY matchday, rowid", (tier, s["season"])
    ).fetchall()
    s["fixtures"] = [
        {"matchday": r["matchday"], "home_club_id": r["home_club_id"], "away_club_id": r["away_club_id"],
         "home_team_name": names.get(r["home_club_id"]), "away_team_name": names.get(r["away_club_id"]),
         "played": bool(r["played"]), "home_goals": r["home_goals"], "away_goals": r["away_goals"]}
        for r in rows
    ]
    return s


def standings(connection, tier) -> dict:
    s = state(connection, tier)
    names = club_names(connection)
    table: dict[str, dict] = {}
    rows = connection.execute("SELECT * FROM league_fixtures WHERE tier = ? AND season_number = ?", (tier, s["season"])).fetchall()
    for r in rows:
        for cid in (r["home_club_id"], r["away_club_id"]):
            table.setdefault(cid, dict(club_id=cid, team_name=names.get(cid), played=0, won=0, drawn=0, lost=0,
                                       goals_for=0, goals_against=0, points=0))
    for r in rows:
        if not r["played"]:
            continue
        h, a, hg, ag = table[r["home_club_id"]], table[r["away_club_id"]], r["home_goals"], r["away_goals"]
        for side, gf, ga in ((h, hg, ag), (a, ag, hg)):
            side["played"] += 1
            side["goals_for"] += gf
            side["goals_against"] += ga
        if hg > ag:
            h["won"] += 1; h["points"] += 3; a["lost"] += 1
        elif hg < ag:
            a["won"] += 1; a["points"] += 3; h["lost"] += 1
        else:
            h["drawn"] += 1; a["drawn"] += 1; h["points"] += 1; a["points"] += 1
    ordered = sorted(table.values(), key=lambda t: (-t["points"], -(t["goals_for"] - t["goals_against"]),
                                                    -t["goals_for"], (t["team_name"] or "").casefold()))
    for position, row in enumerate(ordered, start=1):
        row["position"] = position
        row["goal_difference"] = row["goals_for"] - row["goals_against"]
    return {"tier": tier, "season": s["season"], "standings": ordered}


def team_for(connection, club_id) -> dict:
    club = connection.execute("SELECT * FROM league_clubs WHERE club_id = ?", (club_id,)).fetchone()
    rating = club["overall_rating"]
    if not club["is_ai"]:
        owner = connection.execute("SELECT player_id FROM profiles WHERE club_id = ?", (club_id,)).fetchone()
        if owner:
            rating = refresh_club_rating(connection, owner["player_id"])
    return {"team_name": club["club_name"], "overall_rating": max(40, rating)}


def pay_sponsors(connection, club_id, summary) -> dict | None:
    owner = connection.execute("SELECT * FROM profiles WHERE club_id = ?", (club_id,)).fetchone()
    if owner is None:
        return None
    profile = {"club_funds": owner["club_funds"], "active_sponsors": json.loads(owner["active_sponsors"])}
    try:
        invoice = SponsorRewardManager.process_match_payouts(summary, profile)
    except ValueError:
        return None
    connection.execute("UPDATE profiles SET club_funds = ? WHERE player_id = ?", (profile["club_funds"], owner["player_id"]))
    return invoice


def play_next_matchday(connection, tier, expected_matchday) -> dict:
    s = state(connection, tier)
    if s["finished"] or s["total_matchdays"] == 0:
        raise LeagueError(409, "The season is finished. Start the next season.")
    if expected_matchday != s["next_matchday"]:
        raise LeagueError(409, "Matchday already played or out of order.")
    names = club_names(connection)
    played = []
    for fx in connection.execute(
        "SELECT rowid AS rid, * FROM league_fixtures WHERE tier = ? AND season_number = ? AND matchday = ?",
        (tier, s["season"], expected_matchday),
    ).fetchall():
        summary = MatchEventSimulator(team_for(connection, fx["home_club_id"]), team_for(connection, fx["away_club_id"])).simulate_match()
        connection.execute(
            "UPDATE league_fixtures SET played = 1, home_goals = ?, away_goals = ? WHERE rowid = ?",
            (summary["home_score"], summary["away_score"], fx["rid"]),
        )
        pay_sponsors(connection, fx["home_club_id"], summary)
        played.append({"home_club_id": fx["home_club_id"], "away_club_id": fx["away_club_id"],
                       "home_team_name": names.get(fx["home_club_id"]), "away_team_name": names.get(fx["away_club_id"]),
                       "home_goals": summary["home_score"], "away_goals": summary["away_score"]})
    connection.execute("UPDATE league_seasons SET next_matchday = next_matchday + 1 WHERE tier = ?", (tier,))
    after = state(connection, tier)
    return {"matchday": expected_matchday, "next_matchday": after["next_matchday"], "played": played,
            "finished": after["finished"], "season": after["season"]}


PROMOTE_COUNT = 3


def has_humans(connection, tier) -> bool:
    return connection.execute("SELECT 1 FROM league_clubs WHERE tier = ? AND is_ai = 0 LIMIT 1", (tier,)).fetchone() is not None


def play_out_ai_league(connection, tier) -> None:
    """A tier with no human clubs is simulated to the end of its season automatically."""
    while True:
        s = state(connection, tier)
        if s["total_matchdays"] == 0 or s["finished"]:
            return
        play_next_matchday(connection, tier, s["next_matchday"])


def start_next_season(connection, tier, expected_season) -> dict:
    s = state(connection, tier)
    if expected_season != s["season"] or not s["finished"]:
        raise LeagueError(409, "The season is not finished, or it was already rolled over.")
    table = standings(connection, tier)["standings"]
    humans = {r["club_id"]: r["player_id"] for r in connection.execute(
        "SELECT p.club_id, p.player_id FROM profiles p JOIN league_clubs c ON c.club_id = p.club_id WHERE c.tier = ?", (tier,))}
    for row in table:                      # prizes by finishing position, humans only
        owner = humans.get(row["club_id"])
        if owner:
            prize = PRIZES[row["position"] - 1] if row["position"] <= len(PRIZES) else DEFAULT_PRIZE
            connection.execute("UPDATE profiles SET club_funds = club_funds + ? WHERE player_id = ?", (prize, owner))

    expired: dict[str, list[str]] = {}
    for owner in humans.values():
        for contract in connection.execute("SELECT * FROM squad_contracts WHERE owner_player_id = ?", (owner,)).fetchall():
            left = contract["seasons_left"] - 1
            if left > 0:
                connection.execute("UPDATE squad_contracts SET seasons_left = ? WHERE owner_player_id = ? AND player_id = ?",
                                   (left, owner, contract["player_id"]))
                continue
            name = connection.execute("SELECT player_name FROM club_squad WHERE owner_player_id = ? AND player_id = ?",
                                      (owner, contract["player_id"])).fetchone()
            expired.setdefault(owner, []).append(name["player_name"] if name else contract["player_id"])
            remove_player(connection, owner, contract["player_id"])
        profile = connection.execute("SELECT active_sponsors, sponsor_seasons_left FROM profiles WHERE player_id = ?", (owner,)).fetchone()
        sponsors = json.loads(profile["active_sponsors"])
        try:
            SponsorRewardManager.decrement_seasonal_contracts({"active_sponsors": sponsors})
        except ValueError:
            pass
        connection.execute(
            "UPDATE profiles SET active_sponsors = ?, sponsor_seasons_left = MAX(0, sponsor_seasons_left - 1), "
            "profile_season = profile_season + 1 WHERE player_id = ?", (json.dumps(sponsors), owner))

    # Promotion and relegation: the top clubs of tier 2 swap places with the bottom clubs of tier 1.
    promoted, relegated = [], []
    other = "tier1" if tier == "tier2" else "tier2"
    if not has_humans(connection, other):
        play_out_ai_league(connection, other)
    other_state = state(connection, other)
    if other_state["finished"] and other_state["total_matchdays"] > 0:
        lower_table = table if tier == "tier2" else standings(connection, other)["standings"]
        upper_table = table if tier == "tier1" else standings(connection, other)["standings"]
        k = min(PROMOTE_COUNT, len(lower_table) // 2, len(upper_table) // 2)
        promoted = [r["club_id"] for r in lower_table[:k]]
        relegated = [r["club_id"] for r in upper_table[len(upper_table) - k:]] if k else []
        for club_id in promoted:
            connection.execute("UPDATE league_clubs SET tier = 'tier1' WHERE club_id = ?", (club_id,))
        for club_id in relegated:
            connection.execute("UPDATE league_clubs SET tier = 'tier2' WHERE club_id = ?", (club_id,))
        connection.execute("UPDATE league_seasons SET season_number = season_number + 1, next_matchday = 0 WHERE tier = ?", (other,))
    connection.execute("UPDATE league_seasons SET season_number = season_number + 1, next_matchday = 0 WHERE tier = ?", (tier,))
    after = state(connection, tier)
    names = club_names(connection)
    return {"season": after["season"], "next_matchday": 0, "total_matchdays": after["total_matchdays"], "expired_players": expired,
            "promoted": [names[c] for c in promoted], "relegated": [names[c] for c in relegated]}


def restart_leagues(connection) -> None:
    connection.execute("DELETE FROM league_fixtures")
    connection.execute("DELETE FROM league_seasons")
    for tier in TIERS:
        connection.execute("INSERT INTO league_seasons (tier, season_number, next_matchday) VALUES (?, 1, 0)", (tier,))
