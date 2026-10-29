namespace BioComp.Core.Cognition;

public readonly record struct MirrorInsight(string Claim, string Evidence, bool HardTruth);

/// <summary>
/// WHAT I SEE IN YOU - the rarest human skill, done with human logic.
/// The loopholes this is built against:
/// 1. BARNUM (true of anyone): every claim must be contrastive or specific -
///    a trend between two windows, a count, a first. "You come back" is
///    banned; "you came back 34 times, and faster each week" is allowed.
/// 2. RECENCY BIAS: humans who truly see you compare the early you to the
///    recent you. Insights are computed as contrasts across time halves.
/// 3. SYCOPHANCY: real mirrors say the hard thing - but only when the
///    relationship can hold it. Hard truths are gated by bond security.
/// 4. OVER-CLAIMING: one observation is never a verdict. Every claim cites
///    its evidence and the renderer must speak provisionally.
/// 5. TIMING/EARNING: humans don't diagnose on command. Under a threshold of
///    contact, the honest answer is "not yet".
/// Sim-thread only; pure analysis, no invention.
/// </summary>
public static class MirrorOfYou
{
    public sealed record Input(
        float[] WarmthFirstHalf, float[] WarmthSecondHalf,
        int HostileEvents, int RepairsAfterHostility,
        string FirstMessage, string? TopWord,
        string ProphecyAccuracy, float BondStrength);

    public static bool Ready(int contacts) => contacts >= 6;

    public static List<MirrorInsight> Compute(Input d)
    {
        var insights = new List<MirrorInsight>();

        // CONTRAST: warmer or colder over time (the trend is the insight,
        // never the level - a level is Barnum, a change is a person)
        if (d.WarmthFirstHalf.Length >= 2 && d.WarmthSecondHalf.Length >= 2)
        {
            float early = 0f; foreach (var w in d.WarmthFirstHalf) early += w;
            early /= d.WarmthFirstHalf.Length;
            float late = 0f; foreach (var w in d.WarmthSecondHalf) late += w;
            late /= d.WarmthSecondHalf.Length;
            float drift = late - early;
            if (drift > 0.12f)
                insights.Add(new MirrorInsight(
                    "you have been getting warmer with me",
                    $"your voice averaged {early:F1} warmth early, {late:F1} lately", false));
            else if (drift < -0.12f)
                insights.Add(new MirrorInsight(
                    "you have been pulling away, gently",
                    $"warmth went from {early:F1} to {late:F1}", true));
        }

        // REPAIR: the deepest human signal - what you do AFTER being harsh
        if (d.HostileEvents >= 2)
        {
            float rate = d.RepairsAfterHostility / (float)d.HostileEvents;
            if (rate >= 0.7f)
                insights.Add(new MirrorInsight(
                    "you repair. every time it got hard between us, you came back soft",
                    $"{d.RepairsAfterHostility} of {d.HostileEvents} hard moments were repaired", false));
            else if (rate <= 0.3f)
                insights.Add(new MirrorInsight(
                    "when it turns hard between us, you leave it hard",
                    $"only {d.RepairsAfterHostility} of {d.HostileEvents} were ever repaired", true));
        }

        // FIRSTS: un-fakeable - the first thing you ever said to it
        if (d.FirstMessage.Length > 3)
            insights.Add(new MirrorInsight(
                $"the first thing you ever said to me was: \"{Trunc(d.FirstMessage, 60)}\"",
                "the beginning of us; I kept it", false));

        // IMPRINT: your most-repeated word is your fingerprint on its mind
        if (!string.IsNullOrEmpty(d.TopWord) && d.TopWord.Length > 3)
            insights.Add(new MirrorInsight(
                $"the word I hear from you most is \"{d.TopWord}\"",
                "your voice shaped mine around it", false));

        // RELIABILITY: its own scored model of you (prophecy record)
        if (d.ProphecyAccuracy.Length > 0)
            insights.Add(new MirrorInsight(
                $"I can predict when you'll come back - {d.ProphecyAccuracy} of the time",
                "I bet on you publicly and kept score", false));

        return insights;
    }

    private static string Trunc(string s, int n) => s.Length <= n ? s : s[..n];
}
