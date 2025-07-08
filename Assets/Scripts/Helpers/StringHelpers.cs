
using System;
using System.Collections.Generic;
using System.Linq;

public static class StringHelpers
{
    
    public static ObjectWithString WithToString(this object obj, string toString) => new(obj, toString);

    public class ObjectWithString
    {
        public readonly object obj;
        public readonly string toString;

        public ObjectWithString(object obj, string toString)
        {
            this.obj = obj;
            this.toString = toString;
        }

        public override string ToString() => toString;
    }
    
    public static string AddDots(this string s, int maxLength)
    {
        if (s.Length <= maxLength) return s;
        return s[..(maxLength - 3)] + "...";
    }

    public static string ReverseUpper(this string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return s;
        if (char.IsUpper(s.First(char.IsLetter)))
            return s.ToLowerInvariant();
        return s.ToUpperInvariant();
    }
    
    public static string AddDotsMiddle(this string s, int maxLength, int? tail = null)
    {
        if (s.Length <= maxLength) return s;
        var firstSegment = tail.HasValue ? maxLength - tail.Value - 3 : maxLength / 2;
        var secondSegment = tail ?? maxLength - firstSegment - 3;
        return s[..firstSegment] + "..." + s[^secondSegment..];
    }

    public static string ToShortString(this int i) =>
        i switch
        {
            < 0 => "-" + ToShortString(-i),
            < 1000 => i.ToString(),
            < 1000000 => (i * 1e-3).ToString("G3") + "k",
            < 1000000000 => (i * 1e-6).ToString("G3") + "M",
            _ => (i * 1e-9).ToString("G3") + "G"
        };
    public static string ToShortString(this double d) =>
        d switch
        {
            < 0 => "-" + ToShortString(-d),
            0 => "0",
            < 1e-9 => (d * 1e12).ToString("G3") + "p",
            < 1e-6 => (d * 1e9).ToString("G3") + "n",
            < 1e-3 => (d * 1e6).ToString("G3") + "μ",
            < 1 => (d * 1e3).ToString("G3") + "m",
            < 1e3 => d.ToString("G3"),
            < 1e6 => (d * 1e-3).ToString("G3") + "k",
            < 1e9 => (d * 1e-6).ToString("G3") + "M",
            < 1e12 => (d * 1e-9).ToString("G3") + "G",
            _ => d.ToString("G3")
        };

}