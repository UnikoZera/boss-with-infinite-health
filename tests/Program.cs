using BossSurvival;

var cases = new (int Hp, long Completed, float Fraction)[]
{
    (1, 0, 0f),
    (0, 0, 0f),
    (-1, 0, 1f / 300f),
    (-150, 0, 0.5f),
    (-299, 0, 299f / 300f),
    (-300, 1, 0f),
    (-301, 1, 1f / 300f),
    (-450, 1, 0.5f),
    (-599, 1, 299f / 300f),
    (-600, 2, 0f),
    (-601, 2, 1f / 300f),
    (-899, 2, 299f / 300f),
    (-900, 3, 0f),
    (int.MinValue, 7_158_278, 248f / 300f)
};

foreach (var testCase in cases)
{
    var actual = OverkillBarProgressCalculator.Calculate(testCase.Hp, 300);
    if (actual.CompletedBars != testCase.Completed || Math.Abs(actual.CurrentBarFraction - testCase.Fraction) > 0.00001f)
        throw new InvalidOperationException(
            $"HP {testCase.Hp}: expected {testCase.Completed} bars at {testCase.Fraction}, " +
            $"got {actual.CompletedBars} bars at {actual.CurrentBarFraction}.");
}

var zeroMaxHp = OverkillBarProgressCalculator.Calculate(-1, 0);
if (zeroMaxHp != new OverkillBarProgress(1, 0f))
    throw new InvalidOperationException($"Expected zero max HP to clamp safely; got {zeroMaxHp}.");

Console.WriteLine($"Passed {cases.Length + 1} overkill bar progress cases.");
