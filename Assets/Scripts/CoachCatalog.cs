using System.Collections.Generic;

[System.Serializable]
public class CoachOffer
{
    public string id;
    public string coachName;
    public string club;       // the club he manages now, or "Legend" / "Free agent"
    public string formation;
    public int rating;        // overall coaching quality
    public long price;
}

public static class CoachCatalog
{
    // Club coaches are as of early November 2026. Managers get sacked and hired all the time,
    // so check this list again before you ship. Formations, ratings and prices are made up for
    // the game (not real data), and every price is above 20 million so the
    // "ask for 10M off" step works for every coach.
    public static List<CoachOffer> Create()
    {
        List<CoachOffer> coaches = new List<CoachOffer>();

        // --- Budget ---
        coaches.Add(CreateCoach("coach_rookie",    "Rookie Coach",         "Free agent",        "4-4-2",   60,  25_000_000));
        coaches.Add(CreateCoach("coach_veteran",   "Club Veteran",         "Free agent",        "4-4-2",   68,  35_000_000));

        // --- Premier League ---
        coaches.Add(CreateCoach("coach_jaissle",   "Matthias Jaissle",     "Newcastle United",  "4-2-3-1", 78,  50_000_000));
        coaches.Add(CreateCoach("coach_carrick",   "Michael Carrick",      "Manchester United", "4-2-3-1", 80,  80_000_000));
        coaches.Add(CreateCoach("coach_zerbi",     "Roberto De Zerbi",     "Tottenham Hotspur", "4-2-3-1", 83,  90_000_000));
        coaches.Add(CreateCoach("coach_iraola",    "Andoni Iraola",        "Liverpool",         "4-2-3-1", 87, 150_000_000));
        coaches.Add(CreateCoach("coach_emery",     "Unai Emery",           "Aston Villa",       "4-2-3-1", 88, 160_000_000));
        coaches.Add(CreateCoach("coach_maresca",   "Enzo Maresca",         "Manchester City",   "4-2-3-1", 89, 190_000_000));
        coaches.Add(CreateCoach("coach_alonso",    "Xabi Alonso",          "Chelsea",           "3-4-2-1", 90, 210_000_000));
        coaches.Add(CreateCoach("coach_arteta",    "Mikel Arteta",         "Arsenal",           "4-3-3",   90, 220_000_000));

        // --- La Liga ---
        coaches.Add(CreateCoach("coach_flick",     "Hansi Flick",          "FC Barcelona",      "4-2-3-1", 91, 210_000_000));
        coaches.Add(CreateCoach("coach_simeone",   "Diego Simeone",        "Atletico Madrid",   "4-4-2",   91, 220_000_000));
        coaches.Add(CreateCoach("coach_mourinho",  "Jose Mourinho",        "Real Madrid",       "4-2-3-1", 95, 340_000_000));

        // --- Serie A ---
        coaches.Add(CreateCoach("coach_gattuso",   "Gennaro Gattuso",      "Lazio",             "4-3-3",   79,  60_000_000));
        coaches.Add(CreateCoach("coach_sarri",     "Maurizio Sarri",       "Atalanta",          "4-3-3",   84,  95_000_000));
        coaches.Add(CreateCoach("coach_amorim",    "Ruben Amorim",         "AC Milan",          "3-4-3",   85, 110_000_000));
        coaches.Add(CreateCoach("coach_chivu",     "Cristian Chivu",       "Inter Milan",       "3-5-2",   86, 125_000_000));
        coaches.Add(CreateCoach("coach_gasperini", "Gian Piero Gasperini", "AS Roma",           "3-4-2-1", 87, 140_000_000));
        coaches.Add(CreateCoach("coach_spalletti", "Luciano Spalletti",    "Juventus",          "3-5-2",   88, 150_000_000));
        coaches.Add(CreateCoach("coach_allegri",   "Massimiliano Allegri", "Napoli",            "3-5-2",   89, 170_000_000));

        // --- Bundesliga ---
        coaches.Add(CreateCoach("coach_hoeness",   "Sebastian Hoeness",    "VfB Stuttgart",     "4-2-3-1", 80,  65_000_000));
        coaches.Add(CreateCoach("coach_kovac",     "Niko Kovac",           "Borussia Dortmund", "4-2-3-1", 83,  85_000_000));
        coaches.Add(CreateCoach("coach_kompany",   "Vincent Kompany",      "Bayern Munich",     "4-2-3-1", 91, 230_000_000));

        // --- Ligue 1 ---
        coaches.Add(CreateCoach("coach_filipe",    "Filipe Luis",          "AS Monaco",         "4-3-3",   75,  40_000_000));
        coaches.Add(CreateCoach("coach_genesio",   "Bruno Genesio",        "Marseille",         "4-2-3-1", 80,  60_000_000));
        coaches.Add(CreateCoach("coach_fonseca",   "Paulo Fonseca",        "Lyon",              "4-2-3-1", 81,  70_000_000));
        coaches.Add(CreateCoach("coach_enrique",   "Luis Enrique",         "Paris Saint-Germain","4-3-3",  94, 300_000_000));

        // --- Legends ---
        coaches.Add(CreateCoach("coach_conte",     "Antonio Conte",        "Legend",            "3-5-2",   89, 170_000_000));
        coaches.Add(CreateCoach("coach_zidane",    "Zinedine Zidane",      "Legend",            "4-3-3",   90, 200_000_000));
        coaches.Add(CreateCoach("coach_wenger",    "Arsene Wenger",        "Legend",            "4-4-2",   92, 260_000_000));
        coaches.Add(CreateCoach("coach_klopp",     "Jurgen Klopp",         "Legend",            "4-3-3",   94, 320_000_000));
        coaches.Add(CreateCoach("coach_ancelotti", "Carlo Ancelotti",      "Legend",            "4-3-3",   96, 380_000_000));
        coaches.Add(CreateCoach("coach_guardiola", "Pep Guardiola",        "Legend",            "4-3-3",   98, 450_000_000));
        coaches.Add(CreateCoach("coach_ferguson",  "Sir Alex Ferguson",    "Legend",            "4-4-2",   99, 500_000_000));

        return coaches;
    }

    public static CoachOffer Find(string id)
    {
        foreach (CoachOffer coach in Create())
        {
            if (coach.id == id) return coach;
        }
        return null;
    }

    private static CoachOffer CreateCoach(string id, string name, string club, string formation, int rating, long price)
    {
        return new CoachOffer
        {
            id = id,
            coachName = name,
            club = club,
            formation = formation,
            rating = rating,
            price = price
        };
    }
}
