using UnityEngine;

public interface ITransformable
{ 
    // these are things that homeomorphisms can be applied to
    public ITransformable ApplyHomeomorphism(Homeomorphism homeomorphism);
}

public interface IDrawable
{ // these are things that our visualization can show (has to handle)
    string Name { get; set; }
    Color Color { get; set; }
    // TODO: Don't make it the responsibility of the implementer to copy these properties.
    // TODO: Add more properties. Style for curves (dashed, width, etc.). Add Hoverability, i.e. make IToolTip, so that we can select / highlight curves etc.
    // btw. there is a Raycast method in Dreamteck.Spline.
    
    IDrawable Copy();

    public virtual string ColorfulName => GetColorfulName();
        
    public string GetColorfulName() => $"<color=#{ColorUtility.ToHtmlStringRGBA(Color)}>{Name}</color>";
    public static string GetColorfulName(Color color, string name) => $"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>{name}</color>";
}

public interface IDrawnsformable: IDrawable, ITransformable
{
    new IDrawnsformable Copy(); // you have to implement this more specific version
    
    IDrawable IDrawable.Copy() => Copy();
    
    new IDrawnsformable ApplyHomeomorphism(Homeomorphism homeomorphism); // you have to implement this more specific version
    
    ITransformable ITransformable.ApplyHomeomorphism(Homeomorphism homeomorphism) => ApplyHomeomorphism(homeomorphism);
}

public interface ITransformable<T> : ITransformable where T : ITransformable
{ // this is just so that the types match. Example:
  // class TangentVector: ITransformable<TangentVector>
    new T ApplyHomeomorphism(Homeomorphism homeomorphism); // this is the only thing you need to implement
    
    ITransformable ITransformable.ApplyHomeomorphism(Homeomorphism homeomorphism) => ApplyHomeomorphism(homeomorphism);

    public static T operator *(Homeomorphism homeo, ITransformable<T> input) => input.ApplyHomeomorphism(homeo);
}

public interface IDrawable<T>: IDrawable where T : IDrawable
{ // this is just so that the types match.
    new T Copy(); // you have to implement this, and Color and Name.
    
    IDrawable IDrawable.Copy() => Copy();
}

public interface IDrawnsformable<T>: IDrawnsformable, IDrawable<T>, ITransformable<T> where T : IDrawnsformable
{ // yes, the name is silly XD
    // you have to implement T ApplyHomeomorphism(...), and Color and Name, and T Copy().
    new T ApplyHomeomorphism(Homeomorphism homeomorphism); 
    
    IDrawnsformable IDrawnsformable.ApplyHomeomorphism(Homeomorphism homeomorphism) => ApplyHomeomorphism(homeomorphism);
    
    ITransformable ITransformable.ApplyHomeomorphism(Homeomorphism homeomorphism) => ApplyHomeomorphism(homeomorphism);
    
    T ITransformable<T>.ApplyHomeomorphism(Homeomorphism homeomorphism) => ApplyHomeomorphism(homeomorphism);
    
    new T Copy(); 
    
    T IDrawable<T>.Copy() => Copy(); // yes, this overwrites itself basically, but the compiler is confused when calling Copy on IDrawnsformable<T>
    
    IDrawnsformable IDrawnsformable.Copy() => Copy();
    
    IDrawable IDrawable.Copy() => Copy(); // this is the same override as in IDrawable<T>, but the compiler can't know if we want that or the implementation from IDrawnsformable.

}