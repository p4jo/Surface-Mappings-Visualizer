using System.Collections.Generic;
using System.Linq;
using UnityEngine;


public partial class Curve: IDrawnsformable<Curve>
{
    public readonly int id;
    private static int _lastId;
    protected Curve()
    {
        id = _lastId++;
    }
    
    private Color? color;

    public virtual Color Color
    {
        get => color ?? DefaultColor;
        set => color = value;
    }

    protected virtual Color DefaultColor => colors[id % colors.Count];

    private static readonly List<Color> colors = new()
    {
        Color.red, Color.green, Color.blue, Color.yellow, Color.cyan, Color.magenta, new Color(1, 0.5f, 0),
        new Color(0.5f, 0, 1)
    };
    
    // public float Width { get; set; } = 0.1f;
    // todo: width - min max behaivor relative to camera zoom or absolute...?

}

public partial class TransformedCurve
{
    protected override Color DefaultColor => curve.Color;
}

public partial class ReverseCurve
{
    protected override Color DefaultColor => curve.Color;
}

public partial class ConcatenatedCurve
{
    protected override Color DefaultColor => segments.First().Color;
}
