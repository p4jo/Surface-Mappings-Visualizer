using System;
using System.Collections.Generic;
using UnityEngine;

public enum AutomorphismType
{
    DehnTwist,
    HalfTwist,
    PointPush
}


public partial class ModelSurface
{

    
    public override Homeomorphism GetAutomorphism(AutomorphismType type, IDrawnsformable[] parameters)
    {
        switch (type)
        {
            case AutomorphismType.DehnTwist when parameters.Length == 1 && parameters[0] is Curve curve:
                var strip = new CurveStrip(curve, closed: true);
                var homeoOnStrip = strip.embedding * strip.DehnTwist * strip.embedding.Inverse;
                return Homeomorphism.ContinueAutomorphismOnSubsurface(homeoOnStrip, curve.Surface);
            case AutomorphismType.HalfTwist when parameters.Length == 2 && parameters[0] is Point a && parameters[1] is Point b:
                throw new NotImplementedException();
            case AutomorphismType.PointPush when parameters.Length == 1 && parameters[0] is Curve curve:
                throw new NotImplementedException();
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }
}