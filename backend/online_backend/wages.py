"""Players are paid every season. The wage depends on the rating (Unity scale 55-115)."""
from __future__ import annotations

import math


def wage_for(rating: int) -> int:
    """About 6M at 80, 36M at 100, 88M at 110. The same curve as Assets/Scripts/Wages.cs."""
    return max(10_000, int(round(1_000_000 * math.exp((rating - 60) * 0.09) / 10_000)) * 10_000)


def wage_bill(connection, player_id: str) -> int:
    rows = connection.execute("SELECT rating FROM club_squad WHERE owner_player_id = ?", (player_id,)).fetchall()
    return sum(wage_for(r["rating"]) for r in rows)
