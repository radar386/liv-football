# LIV Football Mobile: before you share it

## 1. Names you must replace or get permission for
The game currently uses real names. That is fine for testing with friends, but not for a public release (itch.io, stores).
- **Clubs and crests** (PlayerCatalog.cs, the 50 real clubs): real club names. Use made-up club names, or get a licence.
- **Players** (PlayerCatalog.cs, AllPlayers.csv): real player names. Real names and likenesses need a licence, or use fictional names.
- **Coaches** (CoachCatalog.cs): real coaches and legends, same issue.
- **Sponsors** (SponsorCatalog.cs, 123 brands): real company names and prices are made up. Using brand names without permission can be a problem, so use fictional brands for a public release.
- Easy way to swap: edit the generator CSVs (AllPlayers.csv, AllCoaches.csv) and regenerate, or find-and-replace the names in the three catalog files. The ids (player_..., coach_..., sponsor_...) only need to stay unique.

## 2. Crest artwork spelling (the picture itself, not the code)
- Sylford FC: the crest image says "SYLFOID FC"
- Unmended FC: the crest image says "UMENDED FC"
- Black Steel Rovers: the crest image says "BLACK ROVERS"
Fix these in your image editor and keep the same file names (sylford_fc, unmended_fc, black_steel_rovers) in Assets/Resources/Crests/.

## 3. Data that can be out of date
- Coaches are as of early November 2026. Managers get sacked and hired, so check again before release.
- Player values are mostly estimates and many line-ups came from predicted sites. Ratings and prices come from the values.
- Clubs with fewer than 11 players get "Reserve" fillers in matches.

## 4. Installer (LIVFootballMobile.iss)
- AppId: filled in with a fresh GUID. Keep it the same for every future version so updates replace the old install.
- MyAppPublisher: put your name or studio name.
- BuildDir: set to the folder where Unity builds the game (currently C:\Builds\LIVFootballMobile).
- MyAppExeName: must be exactly the exe Unity makes (Product Name in Player Settings).
- license.txt: the licence page is switched on. Open license.txt and replace the [YOUR NAME OR STUDIO], [YOUR EMAIL] and [YEAR] placeholders. It is a starting template, not legal advice. Keep it in the same folder as the .iss file.
- Optional: add icon.ico for SetupIconFile.
- Needs Inno Setup 6.3 or newer (for x64compatible). It can only be compiled on Windows.

## 5. Game checks in Unity
- All saves live in PlayerPrefs (one JSON). Press ResetAll() in a debug button while testing, because old saves lack newer fields (contracts, sponsor, lineup).
- UserProfile and ClubSquadPlayer must be [System.Serializable].
- My UserProfile and ClubSquadPlayer are drafts. Match the field names to your real classes.
- Use a monospaced font on the league table text so the columns line up.
- WebGL: PlayerPrefs works in the browser, but WebGL builds can be big, so test the load time.

## 6. Tuning knobs (change these to taste)
- MatchEngine.cs: goals per game, home advantage, cards (constants at the top)
- Negotiation.cs: MinOfferRatio, MaxChance
- AcademyScout.cs: MaxPerSeason (20), MinOvr (40), ContractSeasons (4)
- SponsorCatalog.cs: ContractSeasons (4)
- LeagueEngine.cs: WinBonus, DrawBonus, PlacePrizes
