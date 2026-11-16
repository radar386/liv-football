"""Online backend for LIV Football Mobile (FastAPI + SQLite).

Server-authoritative: pack pulls, contracts, match results and league prizes are all decided here,
never by the client. Run: uvicorn online_backend.app:app  (see ../README_BACKEND.md)
"""
