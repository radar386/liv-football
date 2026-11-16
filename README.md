# LIV Football Mobile
Unity football management game (Windows + WebGL). Scripts are in `Assets/Scripts`, generated sounds in `Assets/Resources/Sounds` (`tools/make_sounds.py`).
The Windows installer script is in `installer/` (compile with Inno Setup on Windows).

## Play in the browser (GitHub Pages)
The workflow in `.github/workflows/webgl.yml` builds WebGL and publishes it to GitHub Pages on every push to `main`.
One-time setup: add the repo secrets `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD` (see https://game.ci/docs/github/activation) and set Settings > Pages > Source = GitHub Actions.
The repo needs the rest of the Unity project (`ProjectSettings/`, `Packages/`, scenes) committed too.

## What is in the repo
| Folder | What it is |
|---|---|
| `Assets/Scripts` | The Unity game (C#): clubs, squads, shops, packs, academy scouting, league, match engine, wages. |
| `backend/` | Python match simulator, peer verification, league tiers and sponsor payouts, with unit tests (`python3 -m unittest`). |
| `web/index.html` | The single-file web version of the game. Open it in a browser, no build needed. |
| `AllPlayers.csv`, `tools/make_catalog.py` | 1,034 invented players in 50 invented clubs. Edit the script and run it to regenerate the CSV and `Assets/Scripts/PlayerCatalog.cs`. |
| `installer/` | Inno Setup script and EULA for the Windows installer. |

## Rules in short
- Ratings are the only thing that decides a match; formation does not.
- Players are paid 12% of their market value every season, after sponsor income.
- Scouting rolls a rating from 40 up to your team ovr and never above it.
- Contracts: academy players leave when theirs ends; others can be renewed for a fee.

## Names and licences
All player and club names in the catalog are invented. Coach names and sponsor brands in the game are real; see `RELEASE_CHECKLIST.md` before releasing publicly.

## Rating scales
`Assets/Scripts/RatingScale.cs` converts between the Unity rating (55-115), the web rating (50-97) and the eFootball rating used by the Featured and Epic packs.
