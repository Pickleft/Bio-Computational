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
