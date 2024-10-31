using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MathMesh
{
    public enum MeshType
    {
        Astroidal_Ellipsoid,
        BentHorns,
        BonBon,
        Boy_Apery,
        Boy_BK,
        Braided_Torus,
        Breather,
        Catalan,
        Catenoid,
        Clifford_Torus,
        Cosinus,
        Crescent,
        CrossCap,
        Cyclide,
        Ding_Dong,
        Dini,
        Enneper,
        HyperBolic_Helicoid,
        Isolator,
        Klein_Bottle,
        Kuen,
        Mobius_Strip,
        Pseudocatenoid,
        Pseudosphere,
        Roman,
        Scherk,
        Snail_Shell,
        Steinbach_Screw,
        SteroSphere,
        Torus,
        TriaxialTeardrop,
        Twisted_Klein_Bottle,
        Twisted_Torus,
        Wellenkugel
        /*
        Banchoff_Chmutov,
        Barth_Decic,
        Barth_Sextic,
        Bianchi_Pinkall_Flat_Tori,
        Bohemian_Dome,
        Breather_Soliton,
        Bretzel2,
        Bretzel5,
        Calabi_Yau_MS,
        CHM,
        Chmutov,
        Costa_MS,
        Chen_Gackstatter_MS,
        Deco_Cube,
        Deco_Tetrahedron,
        Dirac_Belt,
        Helicoid_Catenoid,
        Higher_Genus_Costa_MS,
        Hopf_Fibered_Linked_Tori,
        Inverted_Boy,
        Morin,
        Nordstrands_Weird,
        Orthocircles,
        Pilz,
        Pretzel,
        Punctured_Helicoid,
        Richmond_MS,
        Scherk_Collins,
        Spherical_Helicoid,
        Two_Soliton,
        Three_Soliton,
        Four_Soliton, //Oh god the horror
        Togliatti,
        Torus,
        Unduloid
        */
    }
}

public enum MathMeshTopology
{
    Triangles,
    Points,
    Lines
}

//Add depth option to surfaces?

//Mesh to Voronoi as its own asset. Laplacian mesh optimization/mesh smoothing.
//To do lines, make voronoi inside each voronoi

//https://mathematica.stackexchange.com/questions/37698/how-to-plot-a-certain-surface-what-is-its-parametric-equation
//https://math.stackexchange.com/questions/4258718/how-can-i-find-3d-parametric-functions-that-create-unbroken-loops-that-repeat-pe
//http://virtualmathmuseum.org/Surface/gallery_o.html
//https://www.wolframalpha.com/input/?i=algebraic+surface

//Shaders
//https://www.google.com/search?q=dini+surface&sxsrf=APq-WBvghmpQ4ddk_8MrrSTVNhFfkOaaxg:1645621527244&tbm=isch&source=iu&ictx=1&vet=1&fir=1ppg91NKvSlZDM%252C_GwcLgStYkqHpM%252C_%253BEJidUbdK54HcAM%252C_GwcLgStYkqHpM%252C_%253Bwn9LdjP4WfviCM%252C7JpH5hFrubECpM%252C_%253BLu5tvJa0uWVtgM%252C2FjN8mAuAk52yM%252C_%253BaBncTVI3FOfzhM%252CSuhCvy3a2nH-_M%252C_%253BU4fnL7zd-4Hy9M%252CbvYKXQkan8vzeM%252C_%253BprqGgKQ3FQGQDM%252CeLQGXxAW5VSKqM%252C_%253BClRrtZIXAHnyjM%252C0JU0mdzmu5uCcM%252C_%253BwZepo8aJDPijRM%252C9jl-u4iXpQmkHM%252C_%253BgTVNZhsa3R4LVM%252C8GZA5LNk2UTboM%252C_&usg=AI4_-kRNptGTt4WMIf_iFzY6S-i_quZJoQ&sa=X&ved=2ahUKEwjNrcP08ZX2AhUTKX0KHchiD0cQ9QF6BAgaEAE
//https://www.google.com/search?q=bioluminenscent+shader&oq=bioluminenscent+shader&aqs=edge..69i57j0i13l3j0i22i30l5.4463j0j1&sourceid=chrome&ie=UTF-8
//https://solsea.io/collection/62033ac1130c9f447119435d

//VFX Links
//https://github.com/keijiro/SdfVfxSamples
//https://github.com/IxxyXR/Parametric-VFX

//Torus Knots would be super cool. 3DXM Space Curve Gallery

//Interactive 3d voronoi generator

//Fractals are pack 3. Voxel is kinda sick
//https://www.trmdesignco.com/mandelbulb-3d-fractals.html
//https://www.georgehart.com/rp/rp.html
//https://www.softology.com.au/voc.htm
//http://2008.sub.blue/blog/2009/12/13/mandelbulb.html
//Mandelbulb/Mandelbox
//Fractal coloring is its own beast
//https://www.mi.sanu.ac.rs/vismath/javier1/index.html
//https://www.paridebroggi.com/blogpost/2015/05/06/fractal-continuous-coloring/
//http://www.fractalforums.com/programming/basic-3d-fractal-coloring/
//http://blog.hvidtfeldts.net/index.php/2011/08/distance-estimated-3d-fractals-ii-lighting-and-coloring/

/*
 * Ellipsoid:
x = aa · sin u cos v, y = bb · sin u sin v, z = cc · cos u,
1-sheeted Hyperboloid:
x = aa cosh u cos v, y = bb cosh u sin v, z = cc sinh u,
2-sheeted Hyperboloid (2nd sheet z → −z):
x = aa sinh u cos v, y = bb sinh u sin v, z = cc cosh u,
Elliptic Paraboloid:
x = aa · u cos v, y = bb · u sin v, z = cc · u2
,
Hyperbolic Paraboloid:
x = aa · u, y = bb · v, z = cc · uv.*/