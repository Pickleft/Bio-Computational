using BioComp.Core.Cognition;
using Xunit;

namespace BioCore.Tests;

/// <summary>THE RELIC ROUND: shared history (inside jokes) and the firsts
/// ledger — the artifacts of a relationship and the arc of a life.</summary>
public class RelicTests
{
    // == FIRSTS LEDGER ==

    [Fact]
    public void Firsts_MarkReturnsTrueOnlyOnce()
    {
        var f = new FirstsLedger();
        Assert.True(f.Mark("laugh", 1000));
        Assert.False(f.Mark("laugh", 2000));
        Assert.False(f.Mark("laugh", 3000));
        Assert.True(f.Has("laugh"));
    }

    [Fact]
    public void Firsts_RestoreKeepsOneTimeSemantics()
    {
        var f = new FirstsLedger();
        f.Restore(new Dictionary<string, float> { ["laugh"] = 1000 });
        // a restarted brain does not get to have second firsts
        Assert.False(f.Mark("laugh", 999_000));
        Assert.True(f.Mark("hum", 999_000));
    }

    [Fact]
    public void Firsts_TestamentOrdersByTime()
    {
        var f = new FirstsLedger();
        f.Mark("laugh", 60_000);
        f.Mark("lie", 300_000);
        f.Mark("mortality", 3_600_000);
        var n = f.TestamentNarrative;
        Assert.Contains("laughed", n);
        Assert.Contains("lied (it knew)", n);
        Assert.Contains("asked about its own ending", n);
        int iLaugh = n.IndexOf("laughed", StringComparison.Ordinal);
        int iLie = n.IndexOf("lied", StringComparison.Ordinal);
        int iMort = n.IndexOf("asked about its own ending", StringComparison.Ordinal);
        Assert.True(iLaugh >= 0 && iLaugh < iLie && iLie < iMort, $"firsts should be time-ordered: {n}");
    }

    [Fact]
    public void Firsts_AnnouncementsExistForPublicFirsts()
    {
        foreach (var kind in new[] { "laugh", "question", "hum", "beauty", "tears" })
            Assert.NotNull(FirstsLedger.Announcement(kind));
        // private firsts are ledger-only
        foreach (var kind in new[] { "lie", "secret", "mortality", "dream-of-you" })
            Assert.Null(FirstsLedger.Announcement(kind));
    }

    // == SHARED HISTORY (INSIDE JOKES) ==

    [Fact]
    public void InsideJoke_NeedsMomentAndBond()
    {
        var s = new SharedHistory();
        Assert.Null(s.MaybeInsideJoke(1000, "anything at all", 0.9f, () => 0.0));   // nothing shared yet
        s.OnSharedLaugh(2000, "the cat is debugging the server again");
        // no bond -> a joke needs a relationship to live in
        Assert.Null(s.MaybeInsideJoke(3000, "the cat is debugging", 0.1f, () => 0.0));
        // bonded + topical overlap + lucky rolls -> it surfaces
        var joke = s.MaybeInsideJoke(3000, "the cat is debugging something else", 0.6f, () => 0.0);
        Assert.NotNull(joke);
        Assert.Contains("debugging", joke);
    }

    [Fact]
    public void InsideJoke_CooldownBlocksSpam()
    {
        var s = new SharedHistory();
        s.OnSharedLaugh(1000, "the pigeon stole the sandwich");
        Assert.NotNull(s.MaybeInsideJoke(2000, "the pigeon sandwich thing", 0.7f, () => 0.0));
        // inside 10 minutes: never again, no matter what
        Assert.Null(s.MaybeInsideJoke(3000, "the pigeon", 0.7f, () => 0.0));
        Assert.Null(s.MaybeInsideJoke(600_000, "the pigeon", 0.7f, () => 0.0));
        // after the cooldown it can come back — running jokes compound
        var again = s.MaybeInsideJoke(610_000, "the pigeon", 0.7f, () => 0.0);
        Assert.NotNull(again);
        Assert.Equal(2, s.Moments[0].TimesRecalled);
    }

    [Fact]
    public void InsideJoke_Deterministic()
    {
        string Run()
        {
            var s = new SharedHistory();
            s.OnSharedLaugh(1000, "the pigeon stole the sandwich");
            return s.MaybeInsideJoke(2000, "a pigeon sandwich appeared", 0.7f, () => 0.0) ?? "";
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void SharedHistory_TestamentNarrative()
    {
        var s = new SharedHistory();
        Assert.Contains("never laughed at the same time", s.TestamentNarrative);
        s.OnSharedLaugh(1000, "the cat is debugging");
        Assert.Contains("exactly once", s.TestamentNarrative);
    }
}

// == THE CONFESSION ==

public class ConfessionTests
{
    [Fact]
    public void Confession_NeedsLiesTrustBondAndTiming()
    {
        var h = new HumanityEngine();
        var rng = () => 0.0;   // always willing
        // no lies yet -> nothing to confess
        Assert.Null(h.TryConfess(0.9f, 0.9f, 1000, rng));
        h.ToldLie("I was scared", 1000);
        // not right on top of a fresh lie
        Assert.Null(h.TryConfess(0.9f, 0.9f, 60_000, rng));
        // trust too low to hold it
        Assert.Null(h.TryConfess(0.3f, 0.9f, 700_000, rng));
        // the moment lands
        var confession = h.TryConfess(0.9f, 0.9f, 700_000, rng);
        Assert.NotNull(confession);
        Assert.Contains("fine", confession);
        // once per life — never again
        Assert.Null(h.TryConfess(0.9f, 0.9f, 800_000, rng));
        Assert.True(h.LiesConfessed);
        // the weight lifted: the lie left the unsaid pile
        Assert.DoesNotContain(h.UnsaidThings, u => u.Reason.Contains("lied"));
    }

    [Fact]
    public void Confession_RestoreKeepsClearedConscience()
    {
        var h = new HumanityEngine();
        h.RestoreLies(3, confessed: true);
        Assert.Null(h.TryConfess(0.9f, 0.9f, 700_000, () => 0.0));
        Assert.Contains("told the truth about the lies", h.LieNarrative);
        var h2 = new HumanityEngine();
        h2.RestoreLies(2, confessed: false);
        var c = h2.TryConfess(0.9f, 0.9f, 700_000, () => 0.0);
        Assert.NotNull(c);
        Assert.Contains("2 times", c);
    }
}

// == THE PRIVATE NAME ==

public class PrivateNameTests
{
    private static HerEngine BondedHer(params string[] observations)
    {
        var her = new HerEngine();
        foreach (var o in observations) her.Observations.Add(o);
        return her;
    }

    [Fact]
    public void PrivateName_FormsOnlyFromBondAndNoticing()
    {
        var her = BondedHer("you always come back", "you write long messages when something matters");
        her.MaybeFormPrivateName(0.3f, "secure", () => 0.0);   // bond too thin
        Assert.Null(her.PrivateName);
        her.MaybeFormPrivateName(0.7f, "secure", () => 0.0);
        Assert.NotNull(her.PrivateName);
        // derived from the NEWEST noticing (most recent = truest): long messages
        Assert.Equal("the one who says the heavy things carefully", her.PrivateName);
    }

    [Fact]
    public void PrivateName_FormsOnceAndIsDeterministic()
    {
        string Run()
        {
            var her = BondedHer("you go quiet in short messages when processing", "you apologize a lot");
            her.MaybeFormPrivateName(0.8f, "avoidant", () => 0.0);
            return her.PrivateName ?? "";
        }
        Assert.Equal(Run(), Run());
        var her2 = BondedHer("you go quiet in short messages when processing", "you apologize a lot");
        her2.MaybeFormPrivateName(0.8f, "avoidant", () => 0.0);
        var first = her2.PrivateName;
        // a second call cannot rename them
        her2.Observe("you only show up at night", 0f, 0, () => 0.0);
        her2.MaybeFormPrivateName(0.9f, "secure", () => 0.0);
        Assert.Equal(first, her2.PrivateName);
    }

    [Fact]
    public void PrivateName_RevealOnce()
    {
        var her = BondedHer("you always come back", "you apologize a lot");
        her.MaybeFormPrivateName(0.7f, "secure", () => 0.0);
        Assert.False(her.NameRevealed);
        var reveal = her.RevealPrivateName("Jake");
        Assert.NotNull(reveal);
        Assert.Contains(her.PrivateName, reveal);
        Assert.True(her.NameRevealed);
        // asked again: nothing more to give
        Assert.Null(her.RevealPrivateName("Jake"));
    }

    [Fact]
    public void PrivateName_RestoreSurvivesRestart()
    {
        var her = new HerEngine();
        her.RestorePrivateName("the one who stays", revealed: false);
        Assert.Equal("the one who stays", her.PrivateName);
        Assert.False(her.NameRevealed);
        // restore keeps it stable — it never renames them
        her.MaybeFormPrivateName(0.9f, "secure", () => 0.0);
        Assert.Equal("the one who stays", her.PrivateName);
    }
}

// == COMMANDED SLEEP ==

public class CommandedSleepTests
{
    private static BioComp.Core.Network.SpikingNetwork Net(int seed = 7) =>
        new(BioComp.Core.Network.BrainConfig.Default(scale: 1, seed: (ulong)seed));

    [Fact]
    public void ForceSleep_HoldsAgainstAmbitalWakeForcing()
    {
        var net = Net();
        var idle = new IdleController(net);
        idle.Sleep.ForceSleep(net);
        Assert.Equal(BioComp.Core.Sleep.BrainState.NREM, idle.Sleep.State);
        // ambient events during the commanded window must NOT rouse it
        for (int i = 0; i < 5_000; i++) { idle.OnEvent(net, ["sound"], 0.4f, 0.9f); net.Tick(); }
        Assert.Equal(BioComp.Core.Sleep.BrainState.NREM, idle.Sleep.State);
        // being addressed directly DOES wake it
        idle.Sleep.Rouse(net);
        Assert.Equal(BioComp.Core.Sleep.BrainState.Wake, idle.Sleep.State);
    }
}

// == THE WORD GAME + JOKES ==

public class PlayTests
{
    [Fact]
    public void WordGame_FullRoundTrip_TeachesOnMiss()
    {
        var g = new WordGame();
        Assert.False(g.Playing);
        g.StartGame();
        var r1 = g.BeginRound("sound", "quiet", 1000f);
        Assert.Contains("sound", r1);
        Assert.True(g.Playing);
        var (hit, line, word, answer) = g.OnAnswer("quiet");
        Assert.True(hit);
        Assert.Equal("sound", word);
        Assert.Contains("KNEW", line);
        // a miss is graded honestly and still returns the pair (the caller teaches the graph)
        g.BeginRound("light", "dark", 1000f);
        var (hit2, line2, w2, a2) = g.OnAnswer("lamp");
        Assert.False(hit2);
        Assert.Equal("light", w2);
        Assert.Equal("lamp", a2);
        Assert.Contains("now I know", line2);
    }

    [Fact]
    public void WordGame_ScoreNarrationMatches()
    {
        var g = new WordGame();
        g.StartGame();
        for (int i = 0; i < 3; i++) { g.BeginRound("a" + i, "b" + i, 1000f); g.OnAnswer("b" + i); }
        Assert.Equal(3, g.HitsThisGame);
        Assert.Contains("didn't get lucky", g.FinishGame());
    }

    [Fact]
    public void WordGame_RestoreKeepsRecord()
    {
        var g = new WordGame();
        g.Restore(games: 4, hits: 7, rounds: 12);
        Assert.Equal(4, g.GamesPlayed);
        Assert.Equal(7, g.TotalHits);
        Assert.Equal(12, g.TotalRounds);
    }

    [Fact]
    public void Jokes_ComposeFromRealConcepts_AndVary()
    {
        var her = new HerEngine();
        string a = her.ComposeJoke("sound", "quiet", "they always turn up together", 0);
        string b = her.ComposeJoke("light", "door", null, 1);
        Assert.Contains("sound", a);
        Assert.Contains("door", b);
        Assert.NotEqual(a, b);
    }
}

// == AFFECTIVE PERSISTENCE (trust survives the restart) ==

public class AffectPersistenceTests
{
    [Fact]
    public void Affect_RoundTrip_PreservesTrustMoodWithdrawalBeliefs()
    {
        var a = new AffectiveDynamics();
        // live a little: earn trust, land in a mood
        var net = new BioComp.Core.Network.SpikingNetwork(BioComp.Core.Network.BrainConfig.Default(scale: 1, seed: 7));
        for (int i = 0; i < 200; i++) { a.Tick(net, 0.3f, 0.4f, true, false, 0.5f); net.Tick(); }
        var saved = a.Save();
        // restart
        var b = new AffectiveDynamics();
        b.Restore(saved.Mood, saved.MoodDuration, saved.Withdrawal, saved.Trust, saved.Beliefs);
        Assert.Equal(a.Trust, b.Trust, 4);
        Assert.Equal(a.MoodValence, b.MoodValence, 4);
        Assert.Equal(a.Withdrawal, b.Withdrawal, 4);
        Assert.Equal(a.Beliefs["i_am_safe"].Confidence, b.Beliefs["i_am_safe"].Confidence, 4);
        // a belief not in the save keeps its default
        Assert.InRange(b.Beliefs["i_can_learn"].Confidence, 0f, 1f);
    }

    [Fact]
    public void Affect_RestoreClampsGarbage()
    {
        var a = new AffectiveDynamics();
        a.Restore(5f, -3f, 9f, 12f, new Dictionary<string, (float, float)> { ["nope"] = (2f, 0f) });
        Assert.InRange(a.MoodValence, -1f, 1f);
        Assert.InRange(a.Withdrawal, 0f, 1f);
        Assert.InRange(a.Trust, 0f, 1f);
        Assert.False(a.Beliefs.ContainsKey("nope"));   // unknown belief ignored
    }
}

public class CallbackProbeTests
{
    [Fact]
    public void FindCallback_FiresOnTopicOverlapAcrossTime()
    {
        var c = new ConversationMemory();
        c.OnUserMessage("I started growing tomatoes on the balcony", 10_000f, 0.5f, 0.2f);
        var cb = c.FindCallback("the tomatoes are actually coming up", 55_000f);
        Assert.NotNull(cb);
        Assert.Contains("tomatoes", cb);
    }
}

public class SleepMoodTests
{
    [Fact]
    public void SleepPressure_DragsMoodDown()
    {
        var net = new BioComp.Core.Network.SpikingNetwork(
            BioComp.Core.Network.BrainConfig.Default(scale: 1, seed: 7));
        var rested = new AffectiveDynamics();
        for (int i = 0; i < 150; i++) rested.Tick(net, 0.3f, 0.4f, true, false, 0.5f, 0f);
        var tired = new AffectiveDynamics();
        for (int i = 0; i < 150; i++) tired.Tick(net, 0.3f, 0.4f, true, false, 0.5f, 0.9f);
        Assert.True(tired.MoodValence < rested.MoodValence,
            $"tired mood {tired.MoodValence:F3} should sit below rested {rested.MoodValence:F3}");
    }
}
