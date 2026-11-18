using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

// Watch a final live (DRAFT). 90 minutes over FinalsEngine.LiveSeconds, with a first half, half-time and a second half,
// then a penalty shootout with named takers if it was level.
// Inspector: texts, a progress Image (Image Type "Filled", Horizontal), two KitViews, an Image for the arena picture
// (assign a stadium sprite; the web game's "LIV Arena" is a top-down night pitch with crowd and floodlights), Skip and Close.
public class LiveFinalUI : MonoBehaviour
{
    [SerializeField] private Text titleText;
    [SerializeField] private Text homeNameText;
    [SerializeField] private Text awayNameText;
    [SerializeField] private Text scoreText;
    [SerializeField] private Text clockText;
    [SerializeField] private Text feedText;
    [SerializeField] private Text penaltyText;
    [SerializeField] private Image progressBar;
    [SerializeField] private KitView homeKit;
    [SerializeField] private KitView awayKit;
    [SerializeField] private Image arenaBackdrop;
    [SerializeField] private Button skipButton;
    [SerializeField] private Button closeButton;

    private MatchResult result;
    private LiveFinalScript script;
    private bool skip;
    private Action onFinished;

    // Open(title, result, homeKit, awayKit, onFinished): the result is already decided, so this only replays it.
    public void Open(string title, MatchResult r, ClubKit home, ClubKit away, int seed, Action finished)
    {
        result = r;
        script = FinalsEngine.Build(r, seed);
        onFinished = finished;
        skip = false;
        gameObject.SetActive(true);

        titleText.text = title;
        homeNameText.text = r.home.name;
        awayNameText.text = r.away.name;
        scoreText.text = "0 - 0";
        clockText.text = "Kick-off";
        feedText.text = "";
        penaltyText.text = "";
        progressBar.fillAmount = 0f;
        homeKit.Apply(home);
        awayKit.Apply(away);

        skipButton.gameObject.SetActive(true);
        skipButton.onClick.RemoveAllListeners();
        skipButton.onClick.AddListener(() => { skip = true; });
        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(Close);

        StopAllCoroutines();
        StartCoroutine(Run());
    }

    public void Close()
    {
        StopAllCoroutines();
        gameObject.SetActive(false);
        if (onFinished != null) { Action a = onFinished; onFinished = null; a(); }
    }

    private IEnumerator Run()
    {
        float perMinute = (FinalsEngine.LiveSeconds - FinalsEngine.HalfTimeSeconds) / 90f;
        float total = FinalsEngine.LiveSeconds + (script.hasShootout ? FinalsEngine.PenaltySeconds : 0f);
        float elapsed = 0f;
        int[] score = new int[2];
        StringBuilder feed = new StringBuilder();
        int next = 0;

        for (int minute = 1; minute <= 90 && !skip; minute++)
        {
            clockText.text = (minute <= 45 ? "First half · " : "Second half · ") + minute + "'";
            while (next < script.goals.Count && FinalsEngine.MinuteOf(script.goals[next]) <= minute)
            {
                MatchEvent g = script.goals[next++];
                score[g.side == 1 ? 1 : 0]++;
                feed.AppendLine(FinalsEngine.MinuteOf(g) + "' GOAL " + g.player + " (" + (g.side == 1 ? result.away.name : result.home.name) + ")");
                scoreText.text = score[0] + " - " + score[1];
                feedText.text = feed.ToString();
            }
            yield return new WaitForSeconds(perMinute);
            elapsed += perMinute;
            progressBar.fillAmount = elapsed / total;

            if (minute == 45)
            {
                clockText.text = "Half-time";
                feed.AppendLine("45' Half-time");
                feedText.text = feed.ToString();
                if (!skip) yield return new WaitForSeconds(FinalsEngine.HalfTimeSeconds);
                elapsed += FinalsEngine.HalfTimeSeconds;
            }
        }

        // Show every goal, whether the clock ran out or the player skipped.
        score[0] = result.goals[0];
        score[1] = result.goals[1];
        scoreText.text = score[0] + " - " + score[1];
        feed.Length = 0;
        foreach (MatchEvent g in script.goals)
            feed.AppendLine(FinalsEngine.MinuteOf(g) + "' GOAL " + g.player + " (" + (g.side == 1 ? result.away.name : result.home.name) + ")");
        feedText.text = feed.ToString();

        if (script.hasShootout)
        {
            clockText.text = "Penalty shootout";
            StringBuilder rows = new StringBuilder();
            int[] pens = new int[2];
            float step = FinalsEngine.PenaltySeconds / 10f;
            for (int i = 0; i < script.kicks.Count; i++)
            {
                FinalKick k = script.kicks[i];
                if (k.scored) pens[k.side]++;
                string team = k.side == 0 ? result.home.name : result.away.name;
                string line = k.player + " (" + team + ") " + (k.scored ? "scores" : k.how + " - MISSED") + "   " + pens[0] + "-" + pens[1];
                rows.Insert(0, line + "\n");
                penaltyText.text = "Penalty shootout: " + pens[0] + " - " + pens[1] + "\n" + rows;
                if (!skip) yield return new WaitForSeconds(step);
            }
        }

        progressBar.fillAmount = 1f;
        string winnerName = script.winner == 0 ? result.home.name : result.away.name;
        clockText.text = "Full time. Winner: " + winnerName + (script.hasShootout ? " (penalties)" : "");
        skipButton.gameObject.SetActive(false);
    }
}
