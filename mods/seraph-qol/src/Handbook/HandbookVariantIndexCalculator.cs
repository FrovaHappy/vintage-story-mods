namespace SeraphQoL.Handbook;

/// <summary>
/// Calcula índices circulares de variantes y ajusta el contador secundario sin caer en negativos
/// al retroceder con la rueda del mouse en componentes del Handbook.
/// </summary>
public static class HandbookVariantIndexCalculator
{
    private const long MaxCyclePeriod = 1_000_000;

    public static int ComputeNextIndex(int currentIndex, int totalCount, int delta)
    {
        if (totalCount <= 0) return 0;
        int next = (currentIndex + delta) % totalCount;
        return next < 0 ? next + totalCount : next;
    }

    public static int ComputeAdjustedSecondCounter(
        int currentSecondCounter,
        int indexDelta,
        IReadOnlyList<int>? unnamedVariantLengths)
    {
        int next = currentSecondCounter + indexDelta;
        if (next >= 0) return next;

        if (unnamedVariantLengths == null || unnamedVariantLengths.Count == 0)
        {
            return 0;
        }

        long period = 1;
        bool hasPositive = false;
        for (int i = 0; i < unnamedVariantLengths.Count; i++)
        {
            int len = unnamedVariantLengths[i];
            if (len <= 0) continue;
            hasPositive = true;
            period = LeastCommonMultiple(period, len);
            if (period >= MaxCyclePeriod)
            {
                period = MaxCyclePeriod;
                break;
            }
        }

        if (!hasPositive || period <= 0) return 0;
        long wrapped = ((next % period) + period) % period;
        return (int)wrapped;
    }

    private static long LeastCommonMultiple(long a, long b)
    {
        if (a <= 0 || b <= 0) return 0;
        long gcd = GreatestCommonDivisor(a, b);
        return (a / gcd) * b;
    }

    private static long GreatestCommonDivisor(long a, long b)
    {
        while (b != 0)
        {
            long t = a % b;
            a = b;
            b = t;
        }
        return a;
    }
}
