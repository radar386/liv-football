# Online backend

FastAPI + SQLite, server-authoritative: pack pulls, contracts, match results and league prizes are decided here.

```
pip install -r requirements.txt
export JWT_SECRET=...            # or JWKS_URL=... for RS256 tokens (the token's `sub` is the player id)
export ADMIN_PLAYER_IDS=id1,id2  # admin tools stay off (503) until this is set
uvicorn online_backend.main:app  # database: LIV_DATABASE (default football.sqlite3)
python3 -m unittest              # 31 tests
```

Wages: paid each season by rating (`online_backend/wages.py`), after prizes.
Players come from `../AllPlayers.csv` (invented names). AI clubs are the 40 strongest catalog clubs (20 per tier). Coaches: the 97 coaches of the web game (`online_backend/coaches.py`).
New human clubs start in tier 2 with 50,000,000. Each season the top 3 of tier 2 swap places with the bottom 3 of tier 1 (a tier with no human clubs is simulated automatically).
Packs: 5 standard types plus eFootball-style `featured` and `epic` (listed under `efootball_types`; rated on the eFootball scale, bought and opened directly, not stored). Sponsors: 160 in `online_backend/sponsors.py`.
