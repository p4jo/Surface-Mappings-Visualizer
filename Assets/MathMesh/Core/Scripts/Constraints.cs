using System.Collections.Generic;

public class Constraints
{
    public Dictionary<string, float> constraints = null;

    public Constraints(string[] names, float[] ranges)
    {
        constraints = new Dictionary<string, float>();
        for(int i = 0; i < names.Length; i++)
        {
            constraints[names[i]] = ranges[i];
        }
    }
    public float GetMinU()
    {
        if (constraints != null)
        {
            return constraints["minU"];
        }
        return 0.0f;
    }
    public float GetMaxU()
    {
        if (constraints != null)
        {
            return constraints["maxU"];
        }
        return 0.0f;
    }
    public float GetMinV()
    {
        if (constraints != null)
        {
            return constraints["minV"];
        }
        return 0.0f;
    }
    public float GetMaxV()
    {
        if (constraints != null)
        {
            return constraints["maxV"];
        }
        return 0.0f;
    }
    public float GetMinA()
    {
        if (constraints != null)
        {
            return constraints["minA"];
        }
        return 0.0f;
    }
    public float GetMaxA()
    {
        if (constraints != null)
        {
            return constraints["maxA"];
        }
        return 0.0f;
    }
    public float GetMinB()
    {
        if (constraints != null)
        {
            return constraints["minB"];
        }
        return 0.0f;
    }
    public float GetMaxB()
    {
        if (constraints != null)
        {
            return constraints["maxB"];
        }
        return 0.0f;
    }
    public float GetMinC()
    {
        if (constraints != null)
        {
            return constraints["minC"];
        }
        return 0.0f;
    }
    public float GetMaxC()
    {
        if (constraints != null)
        {
            return constraints["maxC"];
        }
        return 0.0f;
    }
    public float GetMinD()
    {
        if (constraints != null)
        {
            return constraints["minD"];
        }
        return 0.0f;
    }
    public float GetMaxD()
    {
        if (constraints != null)
        {
            return constraints["maxD"];
        }
        return 0.0f;
    }
}
