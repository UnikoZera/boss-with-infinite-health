namespace BossSurvival;

internal readonly record struct OverkillBarProgress(long CompletedBars, float CurrentBarFraction);

internal static class OverkillBarProgressCalculator
{
    public static OverkillBarProgress Calculate(int currentHp, int originalMaxHp)
    {
        if (currentHp >= 0)
            return new OverkillBarProgress(0, 0f);

        var maxHp = Math.Max(1, originalMaxHp);
        var damage = -(long)currentHp;
        return new OverkillBarProgress(damage / maxHp, (float)(damage % maxHp) / maxHp);
    }
}
