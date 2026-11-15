# LIV Football Mobile
Unity football management game (Windows + WebGL). Scripts are in `Assets/Scripts`, generated sounds in `Assets/Resources/Sounds` (`tools/make_sounds.py`).
The Windows installer script is in `installer/` (compile with Inno Setup on Windows).

## Play in the browser (GitHub Pages)
The workflow in `.github/workflows/webgl.yml` builds WebGL and publishes it to GitHub Pages on every push to `main`.
One-time setup: add the repo secrets `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD` (see https://game.ci/docs/github/activation) and set Settings > Pages > Source = GitHub Actions.
The repo needs the rest of the Unity project (`ProjectSettings/`, `Packages/`, scenes) committed too.
