from __future__ import annotations

import hashlib
import hmac
import json
import random
import secrets
from copy import deepcopy
from typing import Any, Type

from MatchEventSimulator import MatchEventSimulator


class P2PMatchVerifier:
    """Run reproducible peer simulations and compare canonical SHA-256 digests.

    Matching digests detect divergent simulation results; they do not prove that
    either client ran honestly. Competitive matches need server-side authority.
    """

    @staticmethod
    def generate_shared_seed() -> int:
        return secrets.randbits(32)

    @staticmethod
    def run_deterministic_simulation(
        seed: int,
        home_team: dict[str, Any],
        away_team: dict[str, Any],
        simulator_class: Type[MatchEventSimulator] = MatchEventSimulator,
    ) -> dict[str, Any]:
        if isinstance(seed, bool) or not isinstance(seed, int) or not 0 <= seed < 2**32:
            raise ValueError("The shared match seed must be an unsigned 32-bit integer.")

        simulator = simulator_class(
            deepcopy(home_team),
            deepcopy(away_team),
            rng=random.Random(seed),
        )
        return simulator.simulate_match()

    @staticmethod
    def calculate_match_hash(match_summary: dict[str, Any]) -> str:
        required_fields = {
            "final_score",
            "home_score",
            "away_score",
            "stats",
            "timeline",
        }
        missing_fields = required_fields.difference(match_summary)
        if missing_fields:
            raise ValueError(
                "Match summary is missing fields: "
                + ", ".join(sorted(missing_fields))
            )
        if not isinstance(match_summary["timeline"], list):
            raise ValueError("Match summary timeline must be a list.")

        payload = {
            "final_score": match_summary["final_score"],
            "home_score": match_summary["home_score"],
            "away_score": match_summary["away_score"],
            "stats": match_summary["stats"],
            "timeline": match_summary["timeline"],
        }
        try:
            serialized = json.dumps(
                payload,
                sort_keys=True,
                separators=(",", ":"),
                ensure_ascii=False,
                allow_nan=False,
            )
        except (TypeError, ValueError) as error:
            raise ValueError("Match summary is not valid canonical JSON data.") from error

        return hashlib.sha256(serialized.encode("utf-8")).hexdigest()

    @staticmethod
    def verify_peer_results(
        local_summary: dict[str, Any], remote_hash: str
    ) -> tuple[bool, str]:
        if not isinstance(remote_hash, str) or len(remote_hash) != 64:
            raise ValueError("Remote match hash must be a 64-character SHA-256 hex digest.")
        try:
            bytes.fromhex(remote_hash)
        except ValueError as error:
            raise ValueError("Remote match hash must contain hexadecimal characters.") from error

        local_hash = P2PMatchVerifier.calculate_match_hash(local_summary)
        return hmac.compare_digest(local_hash, remote_hash.lower()), local_hash
