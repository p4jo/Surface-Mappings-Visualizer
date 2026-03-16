
using System;

public static class NumberHelpers
{
    
    public static float Square (this float x) => x * x;
    
    
    public static long Lcm(this long a, long b)
    {
        if (a <= 0 || b <= 0)
            return -1;

        var gcd = Gcd(a, b);
        try
        {
            checked
            {
                return (a / gcd) * b;
            }
        }
        catch (OverflowException)
        {
            return -1;
        }
    }

    public static long Gcd(this long a, long b)
    {
        while (b != 0)
            (a, b) = (b, a % b);
        return Math.Abs(a);
    }
}