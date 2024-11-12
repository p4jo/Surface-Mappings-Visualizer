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

    
    public override Homeomorphism GetAutomorphism(AutomorphismType type, ITransformable[] parameters)
    {
        switch (type)
        {
            case AutomorphismType.DehnTwist when parameters.Length == 1 && parameters[0] is Curve curve:
                var strip = new Strip(curve, closed: true);
                var homeoOnStrip = strip.embedding * strip.DehnTwist * strip.embedding.Inverse;
                return Homeomorphism.Continue(homeoOnStrip, curve.Surface);
        }
    }
}