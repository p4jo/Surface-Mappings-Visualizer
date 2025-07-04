using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public partial class Junction: PatchedDrawnsformable, IEquatable<Junction>
{
    public readonly FibredSurface fibredSurface;
    
    /// <summary>
    /// f maps this junction into the image junction, i.e. g(this) = image;
    /// </summary>
    public Junction image;

    private static int _lastId = 0;
    private readonly int id = _lastId++;
    
    public Junction(FibredSurface fibredSurface, IEnumerable<IDrawnsformable> drawables, string name = null, Junction image = null, Color? color = null) :
        base(drawables)
    {
        this.fibredSurface = fibredSurface;
        this.image = image;
        if (color.HasValue) Color = color.Value;
        Name = name ?? fibredSurface.NextVertexName();
    }
    
    public Junction(FibredSurface fibredSurface, IDrawnsformable drawable, string name = null, Junction image = null, Color? color = null) : 
        this(fibredSurface, new[] {drawable}, name, image, color)
    { }

    public Point Position => Patches.FirstOrDefault(v => v is Point) as Point;

    public Junction Copy(FibredSurface fibredSurface = null, string name = null, Junction image = null, Color? color = null, IEnumerable<IDrawnsformable> patches = null) =>
        new(
            fibredSurface ?? this.fibredSurface,
            patches ?? from patch in Patches select patch.Copy(),
            name ?? Name,
            image ?? this.image,
            color ?? Color
        );

    public void AddDrawable(IDrawnsformable drawable)
    {
        patches.Add(drawable);
        drawable.Color = Color;
    }
    
    public void AddCurveAsPartOfJunction(Curve curve)
    {
        AddDrawable(curve);
        patches.RemoveAll(drawable => Equals(curve.StartPosition, drawable) || Equals(curve.EndPosition, drawable));
    }

    public override string ToString() => Name;

    public bool Equals(Junction other) => id == other?.id;

    public override bool Equals(object obj) => obj switch
    {
        Junction other => id == other.id,
        _ => false
    };

    public override int GetHashCode() => id;

    public string ToColorfulString() =>
        ((IDrawable)this).ColorfulName + " with cyclic ordered star " +
        FibredSurface.StarOrdered(this).Select(
            j => ((IDrawable)j).ColorfulName
        ).ToCommaSeparatedString();
}