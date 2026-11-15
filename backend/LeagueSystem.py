from __future__ import annotations

from dataclasses import dataclass
from enum import Enum


class LeagueTier(str, Enum):
    TIER1_ELITE = "tier1"
    TIER2_CHAMPIONSHIP = "tier2"


@dataclass
class SimulationClub:
    club_id: str
    club_name: str
    overall_rating: int
    current_tier: LeagueTier
    points: int = 0
    wins: int = 0
    draws: int = 0
    losses: int = 0
    goals_for: int = 0
    goals_against: int = 0

    @property
    def goal_difference(self) -> int:
        return self.goals_for - self.goals_against

    def reset_season_stats(self) -> None:
        self.points = 0
        self.wins = 0
        self.draws = 0
        self.losses = 0
        self.goals_for = 0
        self.goals_against = 0


class MultiTierLeagueSystem:
    TEAMS_PER_LEAGUE = 20
    CLUBS_TO_PROMOTE = 3

    TIER1_NAMES = (
        "Real Madrid", "Manchester City", "Legends XI", "Bayern Munich",
        "Liverpool", "Chelsea", "Arsenal", "Juventus", "Barcelona",
        "AC Milan", "PSG", "Inter Milan", "Atletico Madrid", "Dortmund",
        "Napoli", "Spurs", "Leverkusen", "Sporting CP", "Benfica", "Ajax",
    )
    TIER2_NAMES = (
        "Luton Town", "Burnley", "Sheffield Utd", "Leeds United",
        "Leicester City", "Southampton", "Coventry", "Middlesbrough",
        "Sunderland", "West Brom", "Norwich", "Hull City", "Watford",
        "Stoke City", "Millwall", "Swansea", "Plymouth", "Blackburn",
        "Preston", "Derby County",
    )

    def __init__(self) -> None:
        self.tier1_clubs: list[SimulationClub] = []
        self.tier2_clubs: list[SimulationClub] = []

    def build_two_tier_ecosystem(
        self,
        online_users: list[dict[str, object]] | None = None,
        local_club_name: str = "Your Club",
        local_club_rating: int = 83,
    ) -> None:
        online_users = online_users or []
        tier1 = [
            SimulationClub(
                "team_local", local_club_name, local_club_rating,
                LeagueTier.TIER1_ELITE,
            )
        ]
        tier2: list[SimulationClub] = []

        seen_ids = {"team_local"}
        for peer in online_users:
            peer_id = peer.get("user_id")
            peer_name = peer.get("active_club_name")
            peer_rating = peer.get("team_ovr")
            if (
                not isinstance(peer_id, str)
                or not peer_id
                or peer_id in seen_ids
                or not isinstance(peer_name, str)
                or not peer_name
                or isinstance(peer_rating, bool)
                or not isinstance(peer_rating, int)
                or peer_rating < 0
            ):
                raise ValueError("Online users require unique IDs, names, and non-negative ratings.")

            target_tier = (
                LeagueTier.TIER1_ELITE
                if peer_rating >= 80
                else LeagueTier.TIER2_CHAMPIONSHIP
            )
            target_clubs = tier1 if target_tier is LeagueTier.TIER1_ELITE else tier2
            if len(target_clubs) < self.TEAMS_PER_LEAGUE:
                target_clubs.append(
                    SimulationClub(peer_id, peer_name, peer_rating, target_tier)
                )
                seen_ids.add(peer_id)

        self._fill_league(
            tier1, self.TIER1_NAMES, LeagueTier.TIER1_ELITE, 78, 88
        )
        self._fill_league(
            tier2, self.TIER2_NAMES, LeagueTier.TIER2_CHAMPIONSHIP, 68, 77
        )
        self.tier1_clubs = tier1
        self.tier2_clubs = tier2

    def record_match(
        self,
        home_club_id: str,
        away_club_id: str,
        home_goals: int,
        away_goals: int,
    ) -> None:
        if (
            isinstance(home_goals, bool)
            or isinstance(away_goals, bool)
            or not isinstance(home_goals, int)
            or not isinstance(away_goals, int)
            or home_goals < 0
            or away_goals < 0
        ):
            raise ValueError("Match goals must be non-negative integers.")
        clubs = self._find_clubs(home_club_id, away_club_id)
        if clubs is None:
            raise ValueError("Both clubs must exist in the same league.")
        home, away = clubs
        home.goals_for += home_goals
        home.goals_against += away_goals
        away.goals_for += away_goals
        away.goals_against += home_goals

        if home_goals > away_goals:
            home.wins += 1
            home.points += 3
            away.losses += 1
        elif home_goals < away_goals:
            away.wins += 1
            away.points += 3
            home.losses += 1
        else:
            home.draws += 1
            away.draws += 1
            home.points += 1
            away.points += 1

    def standings(self, tier: LeagueTier) -> list[SimulationClub]:
        clubs = self._clubs_for_tier(tier)
        return sorted(
            clubs,
            key=lambda club: (
                -club.points,
                -club.goal_difference,
                -club.goals_for,
                club.club_name.casefold(),
            ),
        )

    def process_promotion_and_relegation(self) -> tuple[list[SimulationClub], list[SimulationClub]]:
        if (
            len(self.tier1_clubs) != self.TEAMS_PER_LEAGUE
            or len(self.tier2_clubs) != self.TEAMS_PER_LEAGUE
        ):
            raise ValueError("Both divisions must contain exactly 20 clubs.")

        tier1 = self.standings(LeagueTier.TIER1_ELITE)
        tier2 = self.standings(LeagueTier.TIER2_CHAMPIONSHIP)
        relegated = tier1[-self.CLUBS_TO_PROMOTE :]
        promoted = tier2[: self.CLUBS_TO_PROMOTE]

        for club in relegated:
            self.tier1_clubs.remove(club)
            club.current_tier = LeagueTier.TIER2_CHAMPIONSHIP
            self.tier2_clubs.append(club)
        for club in promoted:
            self.tier2_clubs.remove(club)
            club.current_tier = LeagueTier.TIER1_ELITE
            self.tier1_clubs.append(club)

        for club in self.tier1_clubs + self.tier2_clubs:
            club.reset_season_stats()
        return promoted, relegated

    def _fill_league(
        self,
        clubs: list[SimulationClub],
        names: tuple[str, ...],
        tier: LeagueTier,
        min_rating: int,
        max_rating: int,
    ) -> None:
        used_names = {club.club_name for club in clubs}
        for index, name in enumerate(names):
            if len(clubs) >= self.TEAMS_PER_LEAGUE:
                break
            if name in used_names:
                continue
            rating = min_rating + (index % (max_rating - min_rating + 1))
            clubs.append(
                SimulationClub(
                    f"ai_{tier.value}_{index:02d}", name, rating, tier
                )
            )
            used_names.add(name)

    def _clubs_for_tier(self, tier: LeagueTier) -> list[SimulationClub]:
        if tier is LeagueTier.TIER1_ELITE:
            return self.tier1_clubs
        if tier is LeagueTier.TIER2_CHAMPIONSHIP:
            return self.tier2_clubs
        raise ValueError(f"Unknown league tier: {tier}")

    def _find_clubs(
        self, home_id: str, away_id: str
    ) -> tuple[SimulationClub, SimulationClub] | None:
        for tier in (LeagueTier.TIER1_ELITE, LeagueTier.TIER2_CHAMPIONSHIP):
            clubs = self._clubs_for_tier(tier)
            by_id = {club.club_id: club for club in clubs}
            home = by_id.get(home_id)
            away = by_id.get(away_id)
            if home is not None and away is not None and home is not away:
                return home, away
        return None
