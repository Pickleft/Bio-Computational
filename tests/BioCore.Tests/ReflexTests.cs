using BioComp.Core.Cognition;
using Xunit;

namespace BioCore.Tests;

public class ReflexTests
{
    [Fact]
    public void EarnedReflex_FiresOnRepeat_ButNotFirstTime()
    {
        var map = new ReflexMap();
        Assert.Null(map.Fire("you look tired tonight"));   // never routed: no reflex yet
        map.Learn("you look tired tonight", "sleep");
        var hit = map.Fire("you look tired tonight");
        Assert.NotNull(hit);
        Assert.Equal("sleep", hit!.Intent);
    }

    [Fact]
    public void ShortPhrases_NeverEarnReflexStatus()
    {
        var map = new ReflexMap();
        map.Learn("sleep", "sleep");                 // 1 word: would hijack sentences
        map.Learn("go to bed now", "sleep");         // 4 words but 12 chars: earns
        Assert.Null(map.Fire("sleep"));
        Assert.NotNull(map.Fire("go to bed now"));
    }

    [Fact]
    public void ReflexDoesNotFire_InsideUnrelatedLongSentence()
    {
        var map = new ReflexMap();
        map.Learn("you look tired tonight", "sleep");
        // the pathway is a minor part of a much longer utterance — that's
        // conversation, not the command
        Assert.Null(map.Fire("i was reading that you look tired tonight in an article about sleep research actually"));
        Assert.NotNull(map.Fire("hey you look tired tonight"));
    }

    [Fact]
    public void LongestPathwayWins_WhenTwoOverlap()
    {
        var map = new ReflexMap();
        map.Learn("rest a while", "sleep");
        map.Learn("maybe you should rest a while", "sleep");
        var hit = map.Fire("maybe you should rest a while");
        Assert.NotNull(hit);
        Assert.Equal("maybe you should rest a while", hit!.Phrase);
    }

    [Fact]
    public void Reflexes_SurviveSaveRestore_AndCap()
    {
        var map = new ReflexMap();
        for (int i = 0; i < ReflexMap.MaxReflexes + 10; i++)
            map.Learn($"phrase number {i:000} for the map", "story");
        Assert.Equal(ReflexMap.MaxReflexes, map.All.Count);   // oldest fade, newest stay
        var saved = map.Save();
        var reborn = new ReflexMap();
        reborn.Restore(saved);
        Assert.Equal(ReflexMap.MaxReflexes, reborn.All.Count);
        Assert.NotNull(reborn.Fire("phrase number 069 for the map"));
        Assert.Null(reborn.Fire("phrase number 000 for the map"));   // faded before the save
    }

    [Fact]
    public void Bump_CountsFires()
    {
        var map = new ReflexMap();
        map.Learn("tell me about your dreams", "dream");
        map.Bump("tell me about your dreams");
        map.Bump("tell me about your dreams");
        Assert.Equal(2, map.All[0].Fires);
    }
}
