
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using UnityEngine;

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

}
