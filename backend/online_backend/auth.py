"""Who is calling? Reads a bearer JWT and returns its `sub` as the player id.

Set JWKS_URL (RS256, e.g. Unity Authentication) or JWT_SECRET (HS256). Optional: JWT_AUDIENCE, JWT_ISSUER.
Tests override get_authenticated_player_id with a dependency override.
"""
from __future__ import annotations

import os

import jwt
from fastapi import Header, HTTPException


def get_authenticated_player_id(authorization: str | None = Header(default=None)) -> str:
    if not authorization or not authorization.lower().startswith("bearer "):
        raise HTTPException(status_code=401, detail="Missing bearer token.")
    token = authorization.split(" ", 1)[1].strip()
    options = {"require": ["sub"]}
    kwargs = {}
    if os.environ.get("JWT_AUDIENCE"):
        kwargs["audience"] = os.environ["JWT_AUDIENCE"]
    else:
        options["verify_aud"] = False
    if os.environ.get("JWT_ISSUER"):
        kwargs["issuer"] = os.environ["JWT_ISSUER"]
    try:
        jwks_url = os.environ.get("JWKS_URL")
        if jwks_url:
            key = jwt.PyJWKClient(jwks_url).get_signing_key_from_jwt(token).key
            claims = jwt.decode(token, key, algorithms=["RS256"], options=options, **kwargs)
        else:
            secret = os.environ.get("JWT_SECRET")
            if not secret:
                raise HTTPException(status_code=503, detail="Authentication is not configured.")
            claims = jwt.decode(token, secret, algorithms=["HS256"], options=options, **kwargs)
    except HTTPException:
        raise
    except jwt.PyJWTError as error:
        raise HTTPException(status_code=401, detail="Invalid token.") from error
    sub = claims.get("sub")
    if not isinstance(sub, str) or not sub:
        raise HTTPException(status_code=401, detail="Token has no subject.")
    return sub
