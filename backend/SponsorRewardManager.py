from __future__ import annotations

from typing import Any


class SponsorRewardManager:
    """Apply active sponsor match payouts and season-based contract expiry."""

    CLEAN_SHEET_BONUS_MULTIPLIER = 0.25

    @classmethod
    def process_match_payouts(
        cls,
        match_summary: dict[str, Any],
        home_profile: dict[str, Any],
    ) -> dict[str, Any]:
        if not isinstance(match_summary, dict) or not isinstance(home_profile, dict):
            raise TypeError("Match summary and home profile must be dictionaries.")

        away_score = cls._non_negative_integer(match_summary.get("away_score"), "away_score")
        current_funds = cls._non_negative_integer(
            home_profile.get("club_funds"), "club_funds"
        )
        active_sponsors = home_profile.get("active_sponsors", [])
        if not isinstance(active_sponsors, list):
            raise ValueError("home_profile.active_sponsors must be a list.")

        is_clean_sheet = away_score == 0
        sponsor_reports: list[dict[str, Any]] = []
        total_matchday_earnings = 0

        # Validate and calculate every line before mutating the club balance.
        for index, sponsor in enumerate(active_sponsors):
            if not isinstance(sponsor, dict):
                raise ValueError(f"Active sponsor at index {index} must be a dictionary.")

            seasons_remaining = cls._non_negative_integer(
                sponsor.get("seasons_remaining", 0),
                f"active_sponsors[{index}].seasons_remaining",
            )
            if seasons_remaining == 0:
                continue

            base_payout = cls._non_negative_integer(
                sponsor.get("payout", 0),
                f"active_sponsors[{index}].payout",
            )
            clean_sheet_bonus = (
                int(base_payout * cls.CLEAN_SHEET_BONUS_MULTIPLIER)
                if is_clean_sheet
                else 0
            )
            sponsor_total = base_payout + clean_sheet_bonus
            total_matchday_earnings += sponsor_total
            sponsor_reports.append(
                {
                    "sponsor_name": sponsor.get("sponsor_name", "Unknown Corporate"),
                    "base_payout": base_payout,
                    "clean_sheet_bonus": clean_sheet_bonus,
                    "total_earned": sponsor_total,
                }
            )

        home_profile["club_funds"] = current_funds + total_matchday_earnings
        return {
            "total_commercial_revenue": total_matchday_earnings,
            "clean_sheet_achieved": is_clean_sheet,
            "sponsor_breakdown": sponsor_reports,
        }

    @staticmethod
    def decrement_seasonal_contracts(
        home_profile: dict[str, Any],
    ) -> list[str]:
        if not isinstance(home_profile, dict):
            raise TypeError("Home profile must be a dictionary.")
        active_sponsors = home_profile.get("active_sponsors", [])
        if not isinstance(active_sponsors, list):
            raise ValueError("home_profile.active_sponsors must be a list.")

        # Validate first so malformed data cannot leave only part of the list updated.
        for index, sponsor in enumerate(active_sponsors):
            if not isinstance(sponsor, dict):
                raise ValueError(f"Active sponsor at index {index} must be a dictionary.")
            SponsorRewardManager._non_negative_integer(
                sponsor.get("seasons_remaining", 0),
                f"active_sponsors[{index}].seasons_remaining",
            )

        expired_notices: list[str] = []
        for sponsor in active_sponsors:
            seasons_remaining = sponsor.get("seasons_remaining", 0)
            if seasons_remaining > 0:
                sponsor["seasons_remaining"] = seasons_remaining - 1
                if sponsor["seasons_remaining"] == 0:
                    expired_notices.append(
                        str(sponsor.get("sponsor_name", "Unknown Corporate"))
                    )

        return expired_notices

    @staticmethod
    def _non_negative_integer(value: Any, field_name: str) -> int:
        if isinstance(value, bool) or not isinstance(value, int) or value < 0:
            raise ValueError(f"{field_name} must be a non-negative integer.")
        return value


def create_sample_loki_profile() -> dict[str, Any]:
    """Return a fresh example ledger matching the supplied Loki FC data."""
    return {
        "team_name": "Loki FC",
        "club_funds": 50_000_000,
        "active_sponsors": [
            {
                "id": "SP01",
                "sponsor_name": "Deel",
                "purchase_price": 20_000_000,
                "payout": 15_000_000,
                "seasons_remaining": 2,
            },
            {
                "id": "SP02",
                "sponsor_name": "Sela",
                "purchase_price": 45_000_000,
                "payout": 35_000_000,
                "seasons_remaining": 3,
            },
            {
                "id": "SP03",
                "sponsor_name": "Adidas",
                "purchase_price": 120_000_000,
                "payout": 120_000_000,
                "seasons_remaining": 4,
            },
        ],
    }
