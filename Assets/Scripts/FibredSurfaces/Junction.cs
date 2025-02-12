using System;
using System.Collections.Generic;
using System.Linq;
using QuikGraph;
using UnityEngine;
using FibredGraph = QuikGraph.UndirectedGraph<Junction, UnorientedStrip>;

public partial class Junction: PatchedDrawnsformable, IEquatable<Junction>
{
    public readonly FibredGraph graph;
    
    /// <summary>
    /// f maps this junction into the image junction, i.e. g(this) = image;
    /// </summary>
    public Junction image;

    private static int _lastId = 0;
    private readonly int id = _lastId++;
    
    public Junction(FibredGraph graph, IEnumerable<IDrawnsformable> drawables, string name = null, Junction image = null, Color? color = null): base(drawables)
    {
        this.graph = graph;
        this.image = image;
        if (color.HasValue) Color = color.Value;
        Name = name ?? "v" + id;
    }
    public Junction(FibredGraph graph, IDrawnsformable drawable, string name = null, Junction image = null, Color? color = null) : this(graph, new[] {drawable}, name, image, color)
    { }
    
    public Junction Copy(FibredGraph graph = null, string name = null, Junction image = null, Color? color = null)
    {
        return new Junction(graph ?? this.graph, from patch in Patches select patch.Copy(), name ?? Name, image ?? this.image, color ?? Color);
    }

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
}