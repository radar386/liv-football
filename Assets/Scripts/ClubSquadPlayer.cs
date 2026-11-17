// One player in a user's squad. DRAFT written from how the other scripts use it; compare with your own.
// IMPORTANT: [System.Serializable] or Unity will not save it.
[System.Serializable]
public class ClubSquadPlayer
{
    public string id;
    public string playerName;
    public string position;
    public int rating;
    public bool hasRating;   // false = no rating yet (counts as 70)
}
