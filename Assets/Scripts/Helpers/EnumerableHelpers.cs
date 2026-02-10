using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using UnityEngine;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

public static class EnumerableHelpers
{
    public static string ToCommaSeparatedString<T>(this IEnumerable<T> list, string comma = ", ") => string.Join(comma, list);
    
    public static string ToCommaSeparatedString<T>(this IEnumerable<T> list, Func<T, string> selector, string comma = ", ") =>
        string.Join(comma, list.Select(selector));
    
    public static string ToLineSeparatedString<T>(this IEnumerable<T> list, string newline = "\n") => 
        string.Join(newline, list);
    
    public static string ToLineSeparatedString<T>(this IEnumerable<T> list, Func<T, string> selector, string newline = "\n") =>
        string.Join(newline, list.Select(selector));

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

    public static int FirstIndex<T>(this IEnumerable<T> enumerable, Func<T, bool> selector)
    {
        var index = 0;
        foreach (var t in enumerable)
        {
            if (selector(t))
                return index;
            index++;
        }
        return -1;
    }

    public static IEnumerable<(T, T2)> CartesianProduct<T, T2>(this IEnumerable<T> enumerable, IEnumerable<T2> other)
    {
        return from t in enumerable from t2 in other select (t, t2);
    }
    
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
    
    public static IEnumerable<T> WithoutDuplicates<T>(this IEnumerable<T> list) // the same as Distinct()
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
    
    /// <summary>
    /// Rotates the list until the first element satisfying the selector is at the front.
    /// </summary>
    public static IEnumerable<T> CyclicShift<T>(this IEnumerable<T> list, Func<T, bool> selector)
    {
        var done = false;
        var shiftedList = new List<T>();
        foreach (var element in list)
        {
            if (!done && !selector(element))
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

    public static IEnumerable<T> EndlessLoop<T>(this IEnumerable<T> list)
    {
        while (true)
            foreach (var element in list)
                yield return element;
    }
    
    public static IEnumerable<T> Loop<T>(this IEnumerable<T> list, int length) => list.EndlessLoop().Take(length);
    
    #region weirdly specific "cancellation" methods for (T, bool) tuples
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

    #endregion

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


