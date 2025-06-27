using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

public static class Helpers
{
    public enum RectCutMode
    {
        Corners,
        Vertical,
        Horizontal
    }
    private static IEnumerable<Rect> Minus_internal (Rect A, Rect B, RectCutMode mode = RectCutMode.Corners) {
        var bYMax = Mathf.Max( Mathf.Min(B.yMax, A.yMax), A.yMin);
        var bYMin = Mathf.Min(Mathf.Max(B.yMin, A.yMin), A.yMax);
        var bXMax = Mathf.Max( Mathf.Min(B.xMax, A.xMax), A.xMin);
        var bXMin = Mathf.Min(Mathf.Max(B.xMin, A.xMin), A.xMax);
        
        var bWidth = bXMax - bXMin;
        var bHeight = bYMax - bYMin;
        
        var α = bXMin - A.xMin;
        var β = A.yMax - bYMax;
        var γ =  bYMin - A.yMin;
        var δ = A.xMax - bXMax;
        
        if (mode != RectCutMode.Vertical) // use short vertical rects
        {
            yield return new(A.xMin, bYMin, α, bHeight);
            yield return new(bXMax, bYMin, δ, bHeight);
        }
        if (mode != RectCutMode.Horizontal) // use short horizontal rects
        {
            yield return new(bXMin, bYMax, bWidth, β);
            yield return new(bXMin, A.yMin, bWidth, γ);
        }
        switch (mode)
        {
            // use the corner rects
            case RectCutMode.Corners: // copilot 
                yield return new(A.xMin, A.yMin, α, γ);
                yield return new(bXMax, A.yMin, δ, γ);
                yield return new(A.xMin, bYMax, α, β);
                yield return new(bXMax, bYMax, δ, β);
                break;
            // use long horizontal rects
            case RectCutMode.Horizontal: // copilot 
                yield return new(A.xMin, A.yMin, A.width, γ);
                yield return new(A.xMin, bYMax, A.width, β);
                break;
            // use long vertical rects
            case RectCutMode.Vertical: // copilot 
                yield return new(A.xMin, A.yMin, α, A.height);
                yield return new(bXMax, A.yMin, δ, A.height);
                break;
        }
    }
    
    public static IEnumerable<Rect> Minus(this Rect A, Rect B, RectCutMode mode = RectCutMode.Corners) 
        => from rect in Minus_internal(A, B, mode)
            where rect is { width: > 0, height: > 0 }
            select rect;
    
    
    private const float εSquared = 1e-6f;

    public static bool ApproximatelyEquals(this Vector3 v, Vector3 w) => 
        (v - w).sqrMagnitude < εSquared;

    public static bool ApproximateEquals(this float t, float s) => 
        (t - s) * (t - s) < εSquared;

    public static Vector3 Clamp(this Vector3 x, Vector3 minPos, Vector3 maxPos)
    {
        return new(
            Mathf.Clamp(x.x, minPos.x, maxPos.x),
            Mathf.Clamp(x.y, minPos.y, maxPos.y),
            Mathf.Clamp(x.z, minPos.z, maxPos.z)
        );
    }

    public static Vector3 Max(Vector3 a, Vector3 b)
    {
        return new(
            Mathf.Max(a.x, b.x),
            Mathf.Max(a.y, b.y),
            Mathf.Max(a.z, b.z)
        );
    }
    
    public static Vector3 Min(Vector3 a, Vector3 b)
    {
        return new(
            Mathf.Min(a.x, b.x),
            Mathf.Min(a.y, b.y),
            Mathf.Min(a.z, b.z)
        );
    }
    
    public static bool AtLeast(this Vector3 a, Vector3 b, bool ignoreZ = false) => 
        a.x >= b.x && a.y >= b.y && (ignoreZ || a.z >= b.z);
    
    public static bool AtMost(this Vector3 a, Vector3 b, bool ignoreZ = false) =>
        a.x <= b.x && a.y <= b.y && (ignoreZ || a.z <= b.z);
    
    public static Vector3 Average(this IEnumerable<Vector3> enumerable)
    {
        var sum = Vector3.zero;
        var count = 0;
        foreach (var v in enumerable)
        {
            sum += v;
            count++;
        }

        return count > 0 ? sum / count : Vector3.zero;
    }
    
    public static (T, float) ArgMin<T>(this IEnumerable<T> enumerable, System.Func<T, float> selector)
    {
        var min = float.MaxValue;
        T argMin = default;
        foreach (var t in enumerable)
        {
            var value = selector(t);
            if (value < min)
            {
                min = value;
                argMin = t;
            }
        }

        return (argMin, min);
    }

    public static (int, float) ArgMinIndex<T>(this IEnumerable<T> enumerable, Func<T, float> selector) 
    {
        var min = float.MaxValue;
        var argMin = -1;
        var index = 0;
        foreach (var t in enumerable)
        {
            var value = selector(t);
            if (value < min)
            {
                min = value;
                argMin = index;
            }
            index++;
        }

        return (argMin, min);
    }
    
    public static (T, float) ArgMax<T>(this IEnumerable<T> enumerable, System.Func<T, float> selector)
    {
        var res = enumerable.ArgMin(t => -selector(t));
        return (res.Item1, -res.Item2);
    }
    
    public static (int, float) ArgMaxIndex<T>(this IEnumerable<T> enumerable, Func<T, float> selector)
    {
        var res = enumerable.ArgMinIndex(t => -selector(t));
        return (res.Item1, -res.Item2);
    }


    public static IEnumerable<(T, T2)> CartesianProduct<T, T2>(this IEnumerable<T> enumerable, IEnumerable<T2> other)
    {
        return from t in enumerable from t2 in other select (t, t2);
    }
    
    public static float Angle(this Vector2 v) => Mathf.Atan2(v.y, v.x);
    public static float Angle(this Vector3 v) => Mathf.Atan2(v.y, v.x);
    
    public static Vector3 ToVector3(this Complex v) => new((float)v.Real, (float)v.Imaginary);
    
    public static Vector2 ToVector2(this Complex v) => new((float)v.Real, (float)v.Imaginary);
    
    public static Matrix3x3 ToMatrix3x3(this Complex v) => new((float)v.Real, (float)-v.Imaginary, (float)v.Imaginary, (float)v.Real);
    
    public static Complex ToComplex(this Vector2 v) => new(v.x, v.y);
    
    public static Complex ToComplex(this Vector3 v) => new(v.x, v.y);
    
    public static T Pop<T> (this List<T> list)
    {
        if (list.Count == 0) return default;
        var last = list[^1];
        list.RemoveAt(list.Count - 1);
        return last;
    }
    
    public static void Deconstruct<T>(this IEnumerable<T> list, out T first, out T second)
    {
        using var enumerator = list.GetEnumerator();
        first = enumerator.MoveNext() ? enumerator.Current : default;
        second = enumerator.MoveNext() ? enumerator.Current : default;
    }
    
    public static IEnumerable<(int i, T t)> Enumerate<T>(this IEnumerable<T> item)
    {
        int i = 0;
        foreach (var t in item)
            yield return (i++, t);
    }
    
    public static bool ContainsDuplicates<T>(this IEnumerable<T> list)
        => !list.All(new HashSet<T>().Add); // this is some weird way of writing this. From 
    // the function is not x => new HashSet<T>().Add(x) but rather x => y.Add(x) where y = new HashSet<T>() is only created once.
    
    public static T FirstDuplicate<T>(this IEnumerable<T> list)
    {
        var hashSet = new HashSet<T>();
        return list.FirstOrDefault(t => !hashSet.Add(t));
    }
    
    public static T FirstDuplicate<T, T2>(this IEnumerable<T> list, Func<T, T2> selector)
    {
        var hashSet = new HashSet<T2>();
        return list.FirstOrDefault(t => !hashSet.Add(selector(t)));
    }
    
    public static IEnumerable<T> WithoutDuplicates<T>(this IEnumerable<T> list)
    {
        var hashSet = new HashSet<T>();
        return list.Where(hashSet.Add);
    }

    /// <summary>
    /// Rotates the list so that the first element is the one specified.
    /// </summary>
    public static IEnumerable<T> CyclicShift<T>(this IEnumerable<T> list, T firstElement)
    {
        var done = false;
        var shiftedList = new List<T>();
        foreach (var element in list)
        {
            if (!done && !Equals(firstElement, element))
            {
                shiftedList.Add(element);
                continue;
            }
            done = true;
            yield return element;
        }
        foreach (var element in shiftedList)
            yield return element;
    }

    public static IEnumerable<T> CyclicShift<T>(this IEnumerable<T> list, int shift)
    {
        if (shift < 0) throw new NotImplementedException();
        var shiftedList = new List<T>();
        
        foreach (var element in list)
        {
            if (shift > 0)
            {
                shift--;
                shiftedList.Add(element);
                continue;
            }
            yield return element;
        }
        
        foreach (var element in shiftedList)
            yield return element;
    }

    public static IEnumerable<T> EndlessLoop<T>(this IEnumerable<T> list)
    {
        while (true)
            foreach (var element in list)
                yield return element;
    }
    
    public static IEnumerable<T> Loop<T>(this IEnumerable<T> list, int length) => list.EndlessLoop().Take(length);
    
    
    public static List<(T, bool)> ConcatWithCancellation<T>(this IEnumerable<(T, bool)> list, IEnumerable<(T, bool)> other)
    {
        var newList = new List<(T, bool)>(list);
        var doneCancelling = false;
        foreach (var (t, reverse) in other)
        {
            if (doneCancelling || newList.Count == 0 || !newList[^1].Equals((t, !reverse)))
            {
                doneCancelling = true;
                newList.Add((t, reverse));
                continue;
            }

            newList.RemoveAt(newList.Count - 1);
        }

        return newList;
    }
    
    public static List<(T, bool)> ConcatWithCancellation<T>(this IEnumerable<(T, bool)> list, IEnumerable<(T, bool)> other, out int cancellation)
    {
        var newList = new List<(T, bool)>(list);
        var doneCancelling = false;
        cancellation = 0;
        foreach (var (t, reverse) in other)
        {
            if (doneCancelling || newList.Count == 0 || !newList[^1].Equals((t, !reverse)))
            {
                doneCancelling = true;
                newList.Add((t, reverse));
                continue;
            }

            newList.RemoveAt(newList.Count - 1);
            cancellation++;
        }

        return newList;
    }

    public static int SharedInitialSegment<T>(this IEnumerable<T> list, IEnumerable<T> other)
    {
        using var enumerator = list.GetEnumerator();
        using var otherEnumerator = other.GetEnumerator();
        var equality = 0;

        while (enumerator.MoveNext() && otherEnumerator.MoveNext())
        {
            if (Equals(enumerator.Current, otherEnumerator.Current))
                equality++;
            else
                break;
        }
        return equality;
    }
    
    public static int CancellationLength<T>(this IEnumerable<(T, bool)> list, IEnumerable<(T, bool)> other)
    {
        using var enumerator = list.Reverse().GetEnumerator();
        using var otherEnumerator = other.GetEnumerator();
        var cancellation = 0;

        while (enumerator.MoveNext() && otherEnumerator.MoveNext())
        {
            if (enumerator.Current.Equals((otherEnumerator.Current.Item1, !otherEnumerator.Current.Item2)))
                cancellation++;
            else
                break;
        }
        return cancellation;
    }
    
    
    public static IEnumerable<(T, bool)> Inverse<T>(this IEnumerable<(T, bool)> list) => 
        list.Reverse().Select(t => (t.Item1, !t.Item2));


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
        if (char.IsUpper(s.First(char.IsSymbol)))
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

    public static string ToCommaSeparatedString<T>(this IEnumerable<T> list, string comma = ", ") => string.Join(comma, list);
    
    public static string ToCommaSeparatedString<T>(this IEnumerable<T> list, Func<T, string> selector, string comma = ", ") =>
        string.Join(comma, list.Select(selector));
    
    public static string ToLineSeparatedString<T>(this IEnumerable<T> list, string newline = "\n") => 
        string.Join(newline, list);
    
    public static string ToLineSeparatedString<T>(this IEnumerable<T> list, Func<T, string> selector, string newline = "\n") =>
        string.Join(newline, list.Select(selector));

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

    public static double GeometricMean(this IEnumerable<double> list) 
    {
        var product = 1.0;
        var count = 0;
        foreach (var value in list)
        {
            if (value <= 0) 
                product *= - value; // if the value is negative, we take its absolute value
            product *= value;
            count++;
        }
        return count > 0 ? Math.Pow(product, 1.0 / count) : 0.0;
    }
}


