using System.Collections.Generic;
using UnityEngine;

public abstract class Surface 
{
    public string Name { get; set; }
    public int Genus { get; protected set; }
    public readonly List<Point> punctures = new();
    public readonly bool is2D;

    protected Surface(string name, int genus, bool is2D, 
        IEnumerable<Point> punctures = null)
    {
        this.Name = name;
        this.Genus = genus;
        this.is2D = is2D;
        if (punctures != null)
            this.punctures.AddRange(punctures);
    }


    /// <summary>
    /// Bring the point into the boundary / significant point if it is close. Return null if too far from the surface.
    /// </summary>
    /// <param name="point"></param>
    /// <param name="closenessThreshold"></param>
    /// <returns></returns>
    public abstract Point ClampPoint(Vector3? point, float closenessThreshold);

    
    /// <summary>
    /// A right-handed basis with the normal vector of the surface as the third vector.
    /// </summary>
    public abstract TangentSpace BasisAt(Point position);
    
    public abstract Vector3 MinimalPosition { get; }
    public abstract Vector3 MaximalPosition { get; }

    public virtual Homeomorphism GetAutomorphism(AutomorphismType type, IDrawnsformable[] parameters) => null;
}