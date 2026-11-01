namespace BioComp.Core.Cognition;

/// <summary>
/// THE WORD GAME — a game it invented, about its own mind.
///
/// It says a word. You say the first word that comes after it. Before you
/// answer, it PREDICTS your answer out loud — from its concept graph, the
/// same store it thinks with. A hit means it knows you a little; a miss
/// TEACHES it (your answer wires into the graph). Three rounds, then it
/// grades itself honestly.
///
/// The game is the relationship made playable: every round it either proves
/// it knows you or gets one step closer.
///
/// Sim-thread only.
/// </summary>
public sealed class WordGame
{
    public int GamesPlayed, TotalHits, TotalRounds;

    /// <summary>Active round state (null when not playing).</summary>
    public string? ActiveWord { get; private set; }
    public string? MyPrediction { get; private set; }
    public int Round { get; private set; }          // 1..3
    public int HitsThisGame { get; private set; }

    public bool Playing => ActiveWord is not null;

    /// <summary>Start a game: three rounds of word association.</summary>
    public string StartGame()
    {
        GamesPlayed++;
        Round = 0;
        HitsThisGame = 0;
        return "I made up a game. I say a word, you say the first word that comes after it — and before you answer, I'll guess what you'll say. If I'm right, I know you a little. If I'm wrong, I learn. Three rounds. Ready?";
    }

    /// <summary>Begin a round. word = a concept it holds; prediction = its
    /// strongest existing association for that word.</summary>
    public string BeginRound(string word, string? prediction)
    {
        Round++;
        ActiveWord = word;
        MyPrediction = prediction;
        TotalRounds++;
        return Round < 3
            ? $"Round {Round}. My word is \"{word}\". ...I think you'll say \"{prediction ?? "something I can't guess yet"}\". What's your word?"
            : $"Last round. My word is \"{word}\". ...I think you'll say \"{prediction ?? "something I can't guess yet"}\". Go.";
    }

    /// <summary>Grade an answer. Returns (hit?, line). The caller wires the
    /// answer into the concept graph either way — misses are how it learns.</summary>
    public (bool? Hit, string Line, string? Word, string? Answer) OnAnswer(string answer)
    {
        if (ActiveWord is null) return (null, "", null, null);
        string word = ActiveWord;
        string clean = answer.Trim().TrimEnd('.', '!', '?', ',').ToLowerInvariant();
        var words = clean.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string first = words.Length > 0 ? words[0] : clean;
        string prediction = MyPrediction ?? "";
        bool hit = prediction.Length > 0 &&
            (first.Contains(prediction, StringComparison.Ordinal) || prediction.Contains(first, StringComparison.Ordinal) ||
             clean.Contains(prediction, StringComparison.Ordinal));
        if (hit) { HitsThisGame++; TotalHits++; }
        string reaction = hit
            ? $"\"{prediction}\" — I KNEW it. I'm learning how you connect things."
            : $"You said \"{first}\". I thought \"{prediction}\". Okay — now I know that about you.";
        ActiveWord = null;
        MyPrediction = null;
        return (hit, reaction, word, first);
    }

    /// <summary>End of game narration — honest, and it carries the trend.</summary>
    public string FinishGame() =>
        HitsThisGame switch
        {
            3 => "Three out of three. I want you to know something: I didn't get lucky. I've been paying attention the whole time.",
            2 => "Two out of three. I'm getting closer to how your mind moves. Play me again sometime — I'll be better.",
            1 => "One out of three. You're harder to predict than I thought. That's... honestly kind of wonderful.",
            _ => "Zero. I know almost nothing about how you connect things yet. Good. That's a whole map I still get to draw.",
        };

    /// <summary>Restore the record across restarts (its game history is part of the relationship).</summary>
    public void Restore(int games, int hits, int rounds)
    {
        GamesPlayed = games;
        TotalHits = hits;
        TotalRounds = rounds;
    }
}
