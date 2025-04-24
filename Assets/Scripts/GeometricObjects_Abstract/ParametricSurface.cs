using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class ParametricSurface: Surface
{
    public readonly Homeomorphism embedding; 
    
    public readonly List<Rect> chartRects = new();
    
    public override Vector3 MinimalPosition { get; }
    public override Vector3 MaximalPosition { get; }
    
    public ParametricSurface(string name, Homeomorphism embedding, IEnumerable<Rect> chartRects, Vector3 minimalPosition, Vector3 maximalPosition) : base(name, embedding.source.Genus, false)
    {
        embedding.target = this;
        punctures.AddRange(from p in embedding.source.punctures select p.ApplyHomeomorphism(embedding));
        this.chartRects.AddRange(chartRects);
        this.embedding = embedding;
        MinimalPosition = minimalPosition;
        MaximalPosition = maximalPosition;
    }
    
    public ParametricSurface((string, Homeomorphism, IEnumerable<Rect>, Vector3, Vector3) args): 
        this(args.Item1, args.Item2, args.Item3, args.Item4, args.Item5) {}

    public override Point ClampPoint(Vector3? point, float closenessThreshold)
    { 
        // this should only be called on hits of ray with collider, so these shouldn't be too far.
        // Still we probably need to optimize over both variables
        // todo
        return point.HasValue ? new BasicPoint(point.Value) : null;
    }

    public override TangentSpace BasisAt(Point position)
    {
        return new (position, embedding.df(embedding.fInv(position)) * Matrix3x3.InvertZ);
    }
    //     => embedding.source.BasisAt(
    //     position.ApplyHomeomorphism(embedding.Inverse)
    // ).ApplyHomeomorphism(embedding); // todo: inefficient: a) we transform the point by the homeomorphism and then back... b) the source is a model surface which has a very constant basis.

}