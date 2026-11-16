"""The FastAPI application. `uvicorn online_backend.main:app` (database path: LIV_DATABASE, default football.sqlite3)."""
from __future__ import annotations

import json
import os
import random
import uuid
from pathlib import Path
from typing import Literal

from fastapi import Depends, FastAPI, HTTPException
from pydantic import BaseModel, Field

from MatchEventSimulator import MatchEventSimulator

from . import league
from .auth import get_authenticated_player_id
from .packs import DUPLICATE_REFUND_PERCENT, PACK_BY_ID, PACK_TYPES, PLAYERS, draw, market_value, tier_index, TIER_NAMES
from .storage import (add_player, connect_database, ensure_player_profile, owns_player, profile_dict,
                      refresh_club_rating, remove_player, squad_rows, NEW_CLUB_NAME)

API = "/api/v1"
SAMPLE_COACHES = [
    {"id": "sample_coach_basic", "name": "Sample Coach (Basic)", "rating": 65, "price": 30_000_000},
    {"id": "sample_coach_pro", "name": "Sample Coach (Pro)", "rating": 80, "price": 90_000_000},
    {"id": "sample_coach_elite", "name": "Sample Coach (Elite)", "rating": 92, "price": 220_000_000},
]
SAMPLE_SPONSORS = [
    {"id": "SP01", "sponsor_name": "Deel", "purchase_price": 20_000_000, "payout": 15_000_000, "seasons": 2},
    {"id": "SP02", "sponsor_name": "Sela", "purchase_price": 45_000_000, "payout": 35_000_000, "seasons": 3},
    {"id": "SP03", "sponsor_name": "Adidas", "purchase_price": 120_000_000, "payout": 120_000_000, "seasons": 4},
]


class RenameBody(BaseModel):
    team_name: str = Field(min_length=1, max_length=40)


class OpenPacksBody(BaseModel):
    pack_type_id: str
    count: Literal[1, 10]
    use_owned: bool = False


class MatchBody(BaseModel):
    opponent_id: str


class MatchdayBody(BaseModel):
    expected_matchday: int


class SeasonBody(BaseModel):
    expected_season: int


class AmountBody(BaseModel):
    amount: int


class AddPlayerBody(BaseModel):
    player_id: str = Field(min_length=1, max_length=80)
    player_name: str = Field(min_length=1, max_length=80)
    position: str = Field(min_length=1, max_length=4)
    rating: int = Field(ge=1, le=130)


class CoachBody(BaseModel):
    coach_id: str


class SponsorBody(BaseModel):
    sponsor_id: str


class GrantBody(BaseModel):
    pack_type_id: str
    count: int = Field(ge=1, le=1000)


def create_app(database_path: str | Path | None = None) -> FastAPI:
    app = FastAPI(title="LIV Football Mobile backend")
    app.state.database_path = Path(database_path or os.environ.get("LIV_DATABASE", "football.sqlite3"))
    connect_database(app.state.database_path).close()

    def db():
        connection = connect_database(app.state.database_path)
        try:
            yield connection
        finally:
            connection.close()

    def me(connection=Depends(db), player_id: str = Depends(get_authenticated_player_id)):
        return ensure_player_profile(connection, player_id)

    def admin_allowlist() -> set[str]:
        return {i.strip() for i in os.environ.get("ADMIN_PLAYER_IDS", "").split(",") if i.strip()}

    def require_admin(player_id: str = Depends(get_authenticated_player_id)) -> str:
        allowed = admin_allowlist()
        if not allowed:
            raise HTTPException(503, "Admin tools are not configured (ADMIN_PLAYER_IDS is empty).")
        if player_id not in allowed:
            raise HTTPException(403, "Not an admin.")
        return player_id

    def target(connection, player_id):
        row = connection.execute("SELECT * FROM profiles WHERE player_id = ?", (player_id,)).fetchone()
        if row is None:
            raise HTTPException(404, "No such profile.")
        return row

    def audit(connection, actor, tgt, action, details):
        connection.execute(
            "INSERT INTO admin_audit_log (actor_player_id, target_player_id, action, details_json) VALUES (?, ?, ?, ?)",
            (actor, tgt, action, json.dumps(details, sort_keys=True)))

    @app.get("/health")
    def health():
        return {"status": "ok"}

    # ---------- profile ----------
    @app.get(API + "/profile")
    def get_profile(connection=Depends(db), profile=Depends(me)):
        return profile_dict(connection, profile, include_squad=True)

    @app.patch(API + "/profile")
    def rename(body: RenameBody, connection=Depends(db), profile=Depends(me)):
        name = body.team_name.strip()
        if not name:
            raise HTTPException(422, "Team name cannot be empty.")
        connection.execute("UPDATE profiles SET team_name = ? WHERE player_id = ?", (name, profile["player_id"]))
        connection.execute("UPDATE league_clubs SET club_name = ? WHERE club_id = ?", (name, profile["club_id"]))
        connection.commit()
        return profile_dict(connection, target(connection, profile["player_id"]))

    # ---------- packs ----------
    def owned_list(profile):
        owned = json.loads(profile["owned_packs"])
        return owned + [0] * (len(PACK_TYPES) - len(owned))

    @app.get(API + "/packs")
    def packs(profile=Depends(me)):
        owned = owned_list(profile)
        return {"duplicate_refund_percent": DUPLICATE_REFUND_PERCENT,
                "types": [{**p, "owned_count": owned[i]} for i, p in enumerate(PACK_TYPES)]}

    @app.post(API + "/packs/open")
    def open_packs(body: OpenPacksBody, connection=Depends(db), profile=Depends(me)):
        pack = PACK_BY_ID.get(body.pack_type_id)
        if pack is None:
            raise HTTPException(404, "Unknown pack type.")
        pid, index = profile["player_id"], PACK_TYPES.index(pack)
        owned = owned_list(profile)
        funds = profile["club_funds"]
        if body.use_owned:
            if owned[index] < body.count:
                raise HTTPException(409, "You do not own that many packs.")
            owned[index] -= body.count
        else:
            cost = pack["price"] * body.count
            if funds < cost:
                raise HTTPException(409, "Not enough money.")
            funds -= cost
        pulls = []
        for player in draw(pack, body.count):          # the server picks the players, never the client
            value = market_value(player["rating"])
            duplicate = owns_player(connection, pid, player["id"])
            refund = value * DUPLICATE_REFUND_PERCENT // 100 if duplicate else 0
            seasons = 0 if duplicate else add_player(connection, pid, player["id"], player["name"], player["position"], player["rating"])
            funds += refund
            pulls.append({"player_id": player["id"], "name": player["name"], "position": player["position"],
                          "rating": player["rating"], "tier": TIER_NAMES[tier_index(player["rating"])],
                          "market_value": value, "duplicate": duplicate, "refund": refund, "seasons": seasons})
        connection.execute("UPDATE profiles SET club_funds = ?, owned_packs = ? WHERE player_id = ?", (funds, json.dumps(owned), pid))
        rating = refresh_club_rating(connection, pid)
        connection.commit()
        return {"pack_type_id": pack["type_id"], "count": body.count, "use_owned": body.use_owned, "pulls": pulls,
                "club_funds": funds, "overall_rating": rating,
                "owned_packs": [{"type_id": p["type_id"], "count": owned[i]} for i, p in enumerate(PACK_TYPES)]}

    # ---------- free-choice matches (do not touch the league table) ----------
    @app.get(API + "/opponents")
    def opponents(connection=Depends(db), profile=Depends(me)):
        return [{"opponent_id": r["club_id"], "team_name": r["club_name"], "overall_rating": r["overall_rating"]}
                for r in connection.execute("SELECT * FROM league_clubs WHERE is_ai = 1 ORDER BY overall_rating DESC, club_id")]

    @app.post(API + "/matches", status_code=201)
    def play_match(body: MatchBody, connection=Depends(db), profile=Depends(me)):
        opp = connection.execute("SELECT * FROM league_clubs WHERE club_id = ? AND is_ai = 1", (body.opponent_id,)).fetchone()
        if opp is None:
            raise HTTPException(404, "No such opponent.")
        summary = MatchEventSimulator(league.team_for(connection, profile["club_id"]), league.team_for(connection, opp["club_id"])).simulate_match()
        invoice = {"total_commercial_revenue": 0, "clean_sheet_achieved": summary["away_score"] == 0, "sponsor_breakdown": []}
        match_id = uuid.uuid4().hex
        connection.execute(
            "INSERT INTO matches (match_id, home_club_id, away_club_id, summary_json, invoice_json, player_id) VALUES (?, ?, ?, ?, ?, ?)",
            (match_id, profile["club_id"], opp["club_id"], json.dumps(summary), json.dumps(invoice), profile["player_id"]))
        connection.commit()
        return {"match_id": match_id, "match": summary, "financial_invoice": invoice, "club_funds": profile["club_funds"]}

    @app.get(API + "/matches")
    def match_history(connection=Depends(db), profile=Depends(me)):
        return [{"match_id": r["match_id"], "created_at": r["created_at"], "match": json.loads(r["summary_json"]),
                 "season_number": r["season_number"], "matchday": r["matchday"]}
                for r in connection.execute("SELECT * FROM matches WHERE player_id = ? ORDER BY created_at DESC, rowid DESC", (profile["player_id"],))]

    # ---------- league ----------
    @app.get(API + "/league/standings")
    def league_standings(connection=Depends(db), profile=Depends(me)):
        result = league.standings(connection, league.tier_of(connection, profile["player_id"]))
        connection.commit()
        return result

    @app.get(API + "/league/fixtures")
    def league_fixtures(connection=Depends(db), profile=Depends(me)):
        result = league.fixtures(connection, league.tier_of(connection, profile["player_id"]))
        connection.commit()
        return result

    def run_league(connection, fn, *args):
        try:
            result = fn(connection, *args)
        except league.LeagueError as error:
            connection.rollback()
            raise HTTPException(error.status, error.detail) from error
        connection.commit()
        return result

    @app.post(API + "/league/matchdays/next")
    def next_matchday(body: MatchdayBody, connection=Depends(db), profile=Depends(me)):
        tier = league.tier_of(connection, profile["player_id"])
        result = run_league(connection, league.play_next_matchday, tier, body.expected_matchday)
        result["club_funds"] = target(connection, profile["player_id"])["club_funds"]
        return result

    @app.post(API + "/league/next-season")
    def next_season(body: SeasonBody, connection=Depends(db), profile=Depends(me)):
        tier = league.tier_of(connection, profile["player_id"])
        return run_league(connection, league.start_next_season, tier, body.expected_season)

    # ---------- admin ----------
    @app.get(API + "/admin/status")
    def admin_status(player_id: str = Depends(get_authenticated_player_id)):
        return {"is_admin": player_id in admin_allowlist()}

    @app.get(API + "/admin/catalog")
    def admin_catalog(admin: str = Depends(require_admin)):
        return {"coaches": SAMPLE_COACHES, "sponsors": SAMPLE_SPONSORS, "pack_types": PACK_TYPES, "player_count": len(PLAYERS)}

    @app.get(API + "/admin/profiles")
    def admin_profiles(query: str = "", connection=Depends(db), admin: str = Depends(require_admin)):
        like = f"%{query.lower()}%"
        rows = connection.execute(
            "SELECT * FROM profiles WHERE lower(player_id) LIKE ? OR lower(coalesce(username,'')) LIKE ? OR lower(team_name) LIKE ? "
            "ORDER BY player_id LIMIT 100", (like, like, like)).fetchall()
        return [profile_dict(connection, r) for r in rows]

    @app.get(API + "/admin/profiles/{pid}")
    def admin_profile(pid: str, connection=Depends(db), admin: str = Depends(require_admin)):
        return profile_dict(connection, target(connection, pid), include_squad=True)

    @app.post(API + "/admin/profiles/{pid}/money/change")
    def admin_money_change(pid: str, body: AmountBody, connection=Depends(db), admin: str = Depends(require_admin)):
        row = target(connection, pid)
        funds = max(0, row["club_funds"] + body.amount)
        connection.execute("UPDATE profiles SET club_funds = ? WHERE player_id = ?", (funds, pid))
        audit(connection, admin, pid, "change_money", {"amount": body.amount})
        connection.commit()
        return {"club_funds": funds}

    @app.put(API + "/admin/profiles/{pid}/money")
    def admin_money_set(pid: str, body: AmountBody, connection=Depends(db), admin: str = Depends(require_admin)):
        target(connection, pid)
        funds = max(0, body.amount)
        connection.execute("UPDATE profiles SET club_funds = ? WHERE player_id = ?", (funds, pid))
        audit(connection, admin, pid, "set_money", {"amount": body.amount})
        connection.commit()
        return {"club_funds": funds}

    @app.post(API + "/admin/profiles/{pid}/squad")
    def admin_add_player(pid: str, body: AddPlayerBody, connection=Depends(db), admin: str = Depends(require_admin)):
        target(connection, pid)
        if owns_player(connection, pid, body.player_id):
            raise HTTPException(409, "The club already has that player.")
        seasons = add_player(connection, pid, body.player_id, body.player_name, body.position, body.rating)
        audit(connection, admin, pid, "add_player", {"player_id": body.player_id})
        connection.commit()
        return {"contract_seasons": seasons, "overall_rating": refresh_club_rating(connection, pid)}

    @app.delete(API + "/admin/profiles/{pid}/squad/{player_id}")
    def admin_remove_player(pid: str, player_id: str, connection=Depends(db), admin: str = Depends(require_admin)):
        target(connection, pid)
        if not owns_player(connection, pid, player_id):
            raise HTTPException(404, "The club does not have that player.")
        remove_player(connection, pid, player_id)
        audit(connection, admin, pid, "remove_player", {"player_id": player_id})
        connection.commit()
        return {"removed": player_id}

    @app.put(API + "/admin/profiles/{pid}/coach")
    def admin_coach(pid: str, body: CoachBody, connection=Depends(db), admin: str = Depends(require_admin)):
        target(connection, pid)
        if body.coach_id and body.coach_id not in {c["id"] for c in SAMPLE_COACHES}:
            raise HTTPException(404, "Unknown coach.")
        connection.execute("UPDATE profiles SET coach_id = ? WHERE player_id = ?", (body.coach_id, pid))
        audit(connection, admin, pid, "set_coach", {"coach_id": body.coach_id})
        connection.commit()
        return {"coach_id": body.coach_id}

    @app.put(API + "/admin/profiles/{pid}/sponsor")
    def admin_sponsor(pid: str, body: SponsorBody, connection=Depends(db), admin: str = Depends(require_admin)):
        target(connection, pid)
        sponsor = next((s for s in SAMPLE_SPONSORS if s["id"] == body.sponsor_id), None)
        if sponsor is None:
            raise HTTPException(404, "Unknown sponsor.")
        active = [{"id": sponsor["id"], "sponsor_name": sponsor["sponsor_name"], "purchase_price": sponsor["purchase_price"],
                   "payout": sponsor["payout"], "seasons_remaining": sponsor["seasons"]}]
        connection.execute("UPDATE profiles SET sponsor_id = ?, sponsor_seasons_left = ?, active_sponsors = ? WHERE player_id = ?",
                           (sponsor["id"], sponsor["seasons"], json.dumps(active), pid))
        audit(connection, admin, pid, "set_sponsor", {"sponsor_id": sponsor["id"]})
        connection.commit()
        return {"sponsor_id": sponsor["id"], "sponsor_seasons_left": sponsor["seasons"]}

    @app.post(API + "/admin/profiles/{pid}/packs")
    def admin_grant_packs(pid: str, body: GrantBody, connection=Depends(db), admin: str = Depends(require_admin)):
        row = target(connection, pid)
        pack = PACK_BY_ID.get(body.pack_type_id)
        if pack is None:
            raise HTTPException(404, "Unknown pack type.")
        owned = owned_list(row)
        owned[PACK_TYPES.index(pack)] += body.count
        connection.execute("UPDATE profiles SET owned_packs = ? WHERE player_id = ?", (json.dumps(owned), pid))
        audit(connection, admin, pid, "grant_packs", {"pack_type_id": pack["type_id"], "count": body.count})
        connection.commit()
        return {"pack_type_id": pack["type_id"], "count": body.count, "owned_packs": owned}

    @app.post(API + "/admin/profiles/{pid}/reset")
    def admin_reset(pid: str, connection=Depends(db), admin: str = Depends(require_admin)):
        row = target(connection, pid)
        connection.execute("DELETE FROM club_squad WHERE owner_player_id = ?", (pid,))
        connection.execute("DELETE FROM squad_contracts WHERE owner_player_id = ?", (pid,))
        connection.execute(
            "UPDATE profiles SET team_name = ?, coach_id = '', sponsor_id = '', sponsor_seasons_left = 0, active_sponsors = '[]', "
            "club_funds = 0, owned_packs = '[0,0,0,0,0]', lineup_ids = '[]', player_statuses = '[]', potentials = '[]', "
            "achievements = '[]', formation_choice = '', profile_season = 1 WHERE player_id = ?", (NEW_CLUB_NAME, pid))
        connection.execute("UPDATE league_clubs SET club_name = ?, overall_rating = 0 WHERE club_id = ?", (NEW_CLUB_NAME, row["club_id"]))
        audit(connection, admin, pid, "reset_profile", {})
        connection.commit()
        return {"reset": pid}

    @app.post(API + "/admin/league/restart")
    def admin_restart(connection=Depends(db), admin: str = Depends(require_admin)):
        league.restart_leagues(connection)
        audit(connection, admin, None, "restart_league", {})
        connection.commit()
        return {"season": 1}

    return app

