from __future__ import annotations

from typing import Any

from P2PMatchVerifier import P2PMatchVerifier
from SponsorRewardManager import SponsorRewardManager


def settle_verified_match(
    client_a_results: dict[str, Any],
    client_b_hash: str,
    home_profile: dict[str, Any],
) -> dict[str, Any]:
    """Verify peer results before applying the home club's sponsor payouts."""
    verified, local_hash = P2PMatchVerifier.verify_peer_results(
        client_a_results,
        client_b_hash,
    )
    if not verified:
        return {
            "verified": False,
            "match_hash": local_hash,
            "financial_invoice": None,
        }

    financial_invoice = SponsorRewardManager.process_match_payouts(
        match_summary=client_a_results,
        home_profile=home_profile,
    )
    return {
        "verified": True,
        "match_hash": local_hash,
        "financial_invoice": financial_invoice,
    }
