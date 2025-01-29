using System.Collections.Generic;
using System.Linq;
using UnityEngine;



public partial class Point : IDrawnsformable<Point>
{
    public readonly int id;
    private static int _lastId;

    protected Point()
    {
        id = _lastId++;
    }

    private Color? color;

    public string Name { get; set; }

    public virtual Color Color
    {
        get => color ?? DefaultColor;
        set => color = value;
    }


    protected virtual Color DefaultColor => colors[id % colors.Count];

    private static readonly List<Color> colors = new()
    {
        new Color(26f / 256, 105f / 256, 58f / 256), // dark green
        new Color(0.3f, 0.3f, 0.3f),
    };

    public abstract Point Copy();
}

public partial class BasicPoint
{
    public override Point Copy() => new BasicPoint(Position);
}

public partial class ConcatenationSingularPoint
{
    protected override Color DefaultColor => incomingCurve.Color;
    public override Point Copy() => new ConcatenationSingularPoint()
    {
        incomingCurve = incomingCurve.Copy(),
        outgoingCurve = outgoingCurve.Copy(),
        incomingPosIndex = incomingPosIndex,
        outgoingPosIndex = outgoingPosIndex,
        visualJump = visualJump,
        actualJump = actualJump,
        angleJump = angleJump,
        time = time
    };
}

public partial class ModelSurfaceBoundaryPoint
{
    protected override Color DefaultColor => side.Color;
    public override Point Copy() => new ModelSurfaceBoundaryPoint(side.Copy() as ModelSurfaceSide, t);
}

public partial class ModelSurfaceVertex
{
    public override Point Copy() => new ModelSurfaceVertex(
        positions.ToArray(),
        angles.ToList(),
        boundaryCurves.Select(side => side.Copy() as ModelSurfaceSide).ToList()
    );
}