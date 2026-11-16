"""Entry point: uvicorn online_backend.main:app  (run it from the backend/ folder)."""
from .app import create_app

app = create_app()
