namespace Maroik.Core.Domain.Formulas;

/// <summary>Column letters and numbers (A = 1, Z = 26, AA = 27).</summary>
public static class FormulaColumns
{
    /// <summary>The 1-based number of upper-case column letters.</summary>
    public static int Number(string letters) => letters.Aggregate(0, (n, c) => (n * 26) + (c - 'A' + 1));

    /// <summary>The letters of a 1-based column number.</summary>
    public static string Letters(int number)
    {
        string letters = "";
        for (; number > 0; number = (number - 1) / 26)
            letters = (char)('A' + ((number - 1) % 26)) + letters;
        return letters;
    }
}
