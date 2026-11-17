using System;
using UnityEngine;
using UnityEngine.UI;

// Row prefab for the lineup list (DRAFT). One row per squad player; click to pick or drop him.
public class LineupRow : MonoBehaviour
{
    [SerializeField] private Text nameText;
    [SerializeField] private Text positionText;
    [SerializeField] private Text ratingText;
    [SerializeField] private Text statusText;   // "Injured" / "Suspended" / empty
    [SerializeField] private Button pickButton;
    [SerializeField] private Text pickLabel;

    public void Setup(ClubSquadPlayer player, bool selected, Action<ClubSquadPlayer> onToggle, string reason)
    {
        nameText.text = player.playerName;
        positionText.text = player.position;
        ratingText.text = player.hasRating ? player.rating.ToString() : "?";
        statusText.text = reason ?? "";
        bool unavailable = !string.IsNullOrEmpty(reason);
        pickLabel.text = selected ? "Remove" : "Pick";
        pickButton.interactable = selected || !unavailable;
        pickButton.onClick.RemoveAllListeners();
        pickButton.onClick.AddListener(() => onToggle(player));
    }
}
