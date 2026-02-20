public class TangentSpace : ITransformable<TangentSpace>
{
    public readonly Point point;
    public readonly Matrix3x3 basis;
    
    public TangentVector A => new(point, basis.a);
    public TangentVector B => new(point, basis.b);
    public TangentVector C => new(point, basis.c);

    public TangentSpace(Point point, Matrix3x3 basis)
    {
        this.point = point;
        this.basis = basis;
    }

    public TangentSpace ApplyHomeomorphism(Homeomorphism homeomorphism) => 
        new(
            point.ApplyHomeomorphism(homeomorphism),
            homeomorphism.df(point.Position) * basis
        );

    public void Deconstruct(out Point point, out Matrix3x3 basis)  
    {
        point = this.point;
        basis = this.basis;
    }
}