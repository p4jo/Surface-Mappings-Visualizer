using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface IPatchedDrawnsformable : IDrawnsformable<IPatchedDrawnsformable>
{
    // you have to implement Patches, Color, Name, and Copy; but not ApplyHomeomorphism
    IEnumerable<IDrawnsformable> Patches { get; } 
    

    IPatchedDrawnsformable IDrawnsformable<IPatchedDrawnsformable>.ApplyHomeomorphism(Homeomorphism homeomorphism) =>
        new TransformedPatch(this, homeomorphism);
}

public class TransformedPatch : IPatchedDrawnsformable
{
    private readonly IPatchedDrawnsformable original;
    private readonly Homeomorphism homeomorphism;

    public TransformedPatch(IPatchedDrawnsformable original, Homeomorphism homeomorphism)
    {
        this.original = original;
        this.homeomorphism = homeomorphism;
    }

    public IEnumerable<IDrawnsformable> Patches => from patch in original.Patches select patch.ApplyHomeomorphism(homeomorphism);

    public string Name {
        get => original.Name;
        set => original.Name = value; // sketchy?
    }

    public Color Color { 
        get => original.Color; 
        set => original.Color = value;  // sketchy?
    }

    public IPatchedDrawnsformable Copy() => new TransformedPatch(original.Copy(), homeomorphism);
    
     IPatchedDrawnsformable IDrawnsformable<IPatchedDrawnsformable>.ApplyHomeomorphism(Homeomorphism homeomorphism) => new TransformedPatch(original, homeomorphism * this.homeomorphism);
}

public class PatchedDrawnsformable : IPatchedDrawnsformable
{
    protected List<IDrawnsformable> patches;
    public IEnumerable<IDrawnsformable> Patches => patches;

    public string Name { get; set; }

    public PatchedDrawnsformable()
    {
        patches = new List<IDrawnsformable>();
    }

    public PatchedDrawnsformable(IEnumerable<IDrawnsformable> patches)
    {
        this.patches = patches.ToList();
    }
    
    
    public Color Color // "virtual"
    {
        get => Patches.First().Color;
        set
        {
            foreach (var patch in Patches)
                patch.Color = value;
        }
    }

    public IPatchedDrawnsformable Copy() => new PatchedDrawnsformable(from patch in patches select patch.Copy()){
        Name = Name,
        Color = Color
    };

}