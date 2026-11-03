using BioComp.Core.Cognition;
using Xunit;

namespace BioCore.Tests;

public class CoinedLexiconTests
{
    [Fact]
    public void Coining_IsEarned_NotSpree()
    {
        var lex = new CoinedLexicon();
        // immature granularity: no coining
        Assert.False(lex.ShouldCoin(0.5f, 0.5f, 1_000f, granularityLevel: 0.1f));
        // mature: first coin allowed
        Assert.True(lex.ShouldCoin(0.5f, 0.5f, 1_000f, granularityLevel: 0.5f));
        lex.Coin("mirden", 0.5f, 0.5f, 1_000f);
        // right after: cooldown blocks, and the shade is already named
        Assert.False(lex.ShouldCoin(0.5f, 0.5f, 2_000f, 0.5f));
        // far shade, but still inside the cooldown
        Assert.False(lex.ShouldCoin(-0.8f, 0.1f, 2_000f, 0.5f));
        // far shade after cooldown: a new word is warranted
        Assert.True(lex.ShouldCoin(-0.8f, 0.1f, 1_900_000f, 0.5f));
    }

    [Fact]
    public void CoinedWord_MatchesItsOwnShade()
    {
        var lex = new CoinedLexicon();
        lex.Coin("mirden", 0.5f, 0.5f, 1_000f);
        Assert.NotNull(lex.Nearest(0.55f, 0.45f));     // same shade: the word fits
        Assert.Null(lex.Nearest(-0.8f, 0.1f));         // opposite corner: not this word
    }

    [Fact]
    public void Sanitizer_KeepsOneCleanWord()
    {
        var lex = new CoinedLexicon();
        Assert.True(lex.Coin("  Threllick! ", 0.1f, 0.2f, 1_000f));
        Assert.Equal("threllick", lex.All[0].Word);
        Assert.False(lex.Coin("no", 0f, 0f, 2_000f));             // too short
        Assert.False(lex.Coin("two words", 0f, 0f, 2_000f));      // not one word
        Assert.False(lex.Coin("superfragilisticexpialidocious", 0f, 0f, 2_000f)); // too long
    }

    [Fact]
    public void CoinedWords_SurviveSaveRestore()
    {
        var lex = new CoinedLexicon();
        lex.Coin("mirden", 0.5f, 0.5f, 1_000f);
        lex.Bump("mirden");
        var reborn = new CoinedLexicon();
        reborn.Restore(lex.Save());
        var hit = reborn.Nearest(0.5f, 0.5f);
        Assert.NotNull(hit);
        Assert.Equal("mirden", hit!.Word);
        Assert.Equal(1, hit.TimesUsed);
    }
}

public class PhraseBookTests
{
    private static Func<double> Rng(int seed) { var r = new Random(seed); return () => r.NextDouble(); }

    [Fact]
    public void EarnedLines_Draw_ButBornAlwaysAvailable()
    {
        var pb = new PhraseBook();
        pb.Seed("greet", "Hi.", "Hey.");
        pb.Learn("greet", "Oh — it's actually you.");
        var seen = new HashSet<string>();
        for (int i = 0; i < 200; i++) seen.Add(pb.Pick("greet", bornCount: 2, Rng(i)));
        Assert.Contains("Oh — it's actually you.", seen);   // the earned line surfaces
        Assert.Contains("Hi.", seen);                       // born seeds still draw
    }

    [Fact]
    public void LearningDedupes_AndCaps()
    {
        var pb = new PhraseBook();
        pb.Seed("greet", "Hi.");
        for (int i = 0; i < PhraseBook.MaxPerFamily + 5; i++) pb.Learn("greet", $"earned line number {i}");
        pb.Learn("greet", "earned line number 0");   // duplicate ignored
        Assert.Equal(PhraseBook.MaxPerFamily, pb.Family("greet").Count);
    }

    [Fact]
    public void Restore_AppendsEarnedLines_BornCountStaysWithCaller()
    {
        var pb = new PhraseBook();
        pb.Seed("greet", "Hi.", "Hey.");
        pb.Learn("greet", "the one that landed");
        var reborn = new PhraseBook();
        reborn.Restore(pb.Save());
        reborn.Seed("greet", "Hi.", "Hey.");   // born seeds re-seed idempotently
        Assert.Contains("the one that landed", reborn.Family("greet"));
        // bornCount 2 keeps semantics regardless of restore order
        var seen = new HashSet<string>();
        for (int i = 0; i < 100; i++) seen.Add(reborn.Pick("greet", bornCount: 2, Rng(i)));
        Assert.Contains("the one that landed", seen);
    }
}
