using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class ParametricSurface: DrawingSurface
{
    public readonly Homeomorphism embedding; 
    public readonly List<Rect> chartRects = new();
    // this has to be assigned after creation as the homeomorphism has this as one of its fields
    public ParametricSurface(string name, Homeomorphism embedding, IEnumerable<Rect> chartRects) : base(name, embedding.source.Genus, false)
    {
        embedding.target = this;
        punctures.AddRange(from p in embedding.source.punctures select p.ApplyHomeomorphism(embedding));
        this.chartRects.AddRange(chartRects);
        this.embedding = embedding;
    }

    public override Point ClampPoint(Vector3? point)
    { 
        // this should only be called on hits of ray with collider, so these shouldn't be too far.
        // Still we probably need to optimize over both variables
        // todo
        return point.HasValue ? new BasicPoint(point.Value) : null;
    }
       
}