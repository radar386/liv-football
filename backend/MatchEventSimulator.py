from __future__ import annotations

import random
from typing import Any


class MatchEventSimulator:
    """Simulate a 90-minute match and return its score, events, and statistics."""

    MATCH_MINUTES = 90
    BASE_EVENT_CHANCE = 0.20   # about 2.5 goals a match, like the web game
    STAMINA_DEPLETION_PER_MINUTE = 0.8
    MIN_STAMINA_FOR_PERFORMANCE = 20
    HOME_ADVANTAGE = 1.03

    def __init__(
        self,
        home_team: dict[str, Any],
        away_team: dict[str, Any],
        rng: random.Random | None = None,
    ) -> None:
        self.home_team = home_team
        self.away_team = away_team
        self.rng = rng or random.Random()

        self.home_score = 0
        self.away_score = 0
        self.current_minute = 0
        self.timeline_events: list[dict[str, Any]] = []
        self.stats = {
            "home": {"shots": 0, "shots_on_target": 0, "fouls": 0, "corners": 0},
            "away": {"shots": 0, "shots_on_target": 0, "fouls": 0, "corners": 0},
        }

    def simulate_match(self) -> dict[str, Any]:
        self._add_timeline_event(0, "KICKOFF", "Match has started!")

        for minute in range(1, self.MATCH_MINUTES + 1):
            self.current_minute = minute
            self._deplete_stamina(self.home_team)
            self._deplete_stamina(self.away_team)

            if minute == 45:
                self._add_timeline_event(
                    45,
                    "HALF_TIME",
                    f"Halftime! Score: {self.home_score} - {self.away_score}",
                )

            if self.rng.random() < self.BASE_EVENT_CHANCE:
                self._process_attacking_chance()

        self._add_timeline_event(
            self.MATCH_MINUTES,
            "FULL_TIME",
            f"Full-time whistle! Final Score: {self.home_score} - {self.away_score}",
        )
        return self._generate_match_summary()

    def _calculate_effective_rating(self, team: dict[str, Any]) -> float:
        base_rating = float(team.get("overall_rating", 60))
        chemistry_multiplier = float(team.get("chemistry_matrix_multiplier", 1.0))
        players = team.get("players", [])
        if not players:
            return base_rating * chemistry_multiplier

        average_stamina = sum(
            float(player.get("stamina", 100)) for player in players
        ) / len(players)
        stamina_factor = (
            1.0
            if average_stamina >= 70
            else max(0.75, average_stamina / 70.0)
        )
        return base_rating * chemistry_multiplier * stamina_factor

    @staticmethod
    def _share(own: float, other: float) -> float:
        """Share of the chances a side creates. The sixth power makes rating matter
        (80 against 60 gives about three quarters of the chances)."""
        return own**6 / (own**6 + other**6)

    def _deplete_stamina(self, team: dict[str, Any]) -> None:
        for player in team.get("players", []):
            current_stamina = float(player.get("stamina", 100.0))
            player["stamina"] = max(
                0.0, current_stamina - self.STAMINA_DEPLETION_PER_MINUTE
            )

    def _process_attacking_chance(self) -> None:
        home_power = self._calculate_effective_rating(self.home_team)
        away_power = self._calculate_effective_rating(self.away_team)
        total_power = home_power + away_power
        if home_power < 0 or away_power < 0 or total_power <= 0:
            raise ValueError("Teams must have positive combined match strength.")

        attacking_side = (
            "home"
            if self.rng.random() < self._share(home_power * self.HOME_ADVANTAGE, away_power)
            else "away"
        )
        defending_side = "away" if attacking_side == "home" else "home"
        attacker_team = self.home_team if attacking_side == "home" else self.away_team
        star_player = self._select_random_player(attacker_team)
        player_name = (
            star_player.get("player_name", "An attacker")
            if star_player
            else "An attacker"
        )
        self.stats[attacking_side]["shots"] += 1

        roll = self.rng.random()
        if roll < 0.35:
            self.stats[attacking_side]["shots_on_target"] += 1
            if self.rng.random() < 0.40:
                if attacking_side == "home":
                    self.home_score += 1
                else:
                    self.away_score += 1
                self._add_timeline_event(
                    self.current_minute,
                    "GOAL",
                    f"GOAL! {player_name} fires it home! "
                    f"{self.home_score}-{self.away_score}",
                    attacking_side,
                )
            else:
                self._add_timeline_event(
                    self.current_minute,
                    "SAVE",
                    f"Great save! The keeper denies a powerful strike from {player_name}.",
                    defending_side,
                )
        elif roll < 0.50:
            self.stats[defending_side]["fouls"] += 1
            self._add_timeline_event(
                self.current_minute,
                "FOUL",
                f"Foul committed down the wing against {player_name}.",
                defending_side,
            )
        elif roll < 0.60:
            self.stats[attacking_side]["corners"] += 1
            self._add_timeline_event(
                self.current_minute,
                "CORNER",
                "Deflection leads to a corner kick for "
                f"{attacker_team.get('team_name', 'Attacking Team')}.",
                attacking_side,
            )
        else:
            self._add_timeline_event(
                self.current_minute,
                "DEFEND",
                "The defense comfortably breaks up the attack orchestrated by "
                f"{player_name}.",
            )

    def _select_random_player(
        self, team: dict[str, Any]
    ) -> dict[str, Any] | None:
        players = team.get("players", [])
        outfield_players = [
            player for player in players if player.get("position") != "GK"
        ]
        if outfield_players:
            return self.rng.choice(outfield_players)
        return self.rng.choice(players) if players else None

    def _add_timeline_event(
        self, minute: int, event_type: str, message: str, side: str = "neutral"
    ) -> None:
        self.timeline_events.append(
            {
                "minute": minute,
                "type": event_type,
                "message": message,
                "side": side,
            }
        )

    def _generate_match_summary(self) -> dict[str, Any]:
        return {
            "home_team_name": self.home_team.get("team_name", "Home Team"),
            "away_team_name": self.away_team.get("team_name", "Away Team"),
            "final_score": f"{self.home_score}-{self.away_score}",
            "home_score": self.home_score,
            "away_score": self.away_score,
            "stats": self.stats,
            "timeline": self.timeline_events,
        }
