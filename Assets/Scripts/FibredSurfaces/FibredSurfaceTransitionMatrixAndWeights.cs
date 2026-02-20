using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.LinearAlgebra;

public partial class FibredSurface
{

    /// <summary>
    /// The transition matrix is a combinatorial representation of the graph map g: E -> E* forgetting order and direction.
    /// The column corresponding to an edge e is how often g(e) crosses each unoriented edge e' in the graph.
    /// The surface homeomorphism described by this FibredSurface is reducible if the transition matrix of H is reducible, as this means that there is a non-trivial subgraph of G that does not only consist of pre-periphery. The boundary of the induced subgraph is preserved by an isotoped version of the homeomorphism and consists at least in part of essential edges, thus determining a reduction in the sense of the Thurston-Nielsen classification.
    /// </summary>
    /// <returns></returns>
    public Dictionary<UnorientedStrip, Dictionary<UnorientedStrip, int>> TransitionMatrix(
        IEnumerable<UnorientedStrip> strips = null)
    {
        var stripArray = (strips ?? Strips).ToArray();
        return stripArray.ToDictionary(
            edgeToCross => edgeToCross,
            edgeToCross => stripArray.ToDictionary(
                edge => edge, 
                edge => edge.EdgePath.Count(e => e.UnderlyingEdge == edgeToCross))
        );
    }

    public Dictionary<UnorientedStrip, Dictionary<UnorientedStrip, int>> TransitionMatrixEssential() =>
        TransitionMatrix(EssentialSubgraph());

    /// <summary>
    /// Tests if there is a preserved subgraph that is not contained in the (pre-)periphery.
    /// If there is such a subgraph and it is not a forest in G/P (clear at the point in the algorithm where this test is done), then the map is reducible:
    /// The boundary components of the preserved subgraph are preserved up to isotopy and at least one of them is essential, thus determining a reduction in the sense of the Thurston-Nielsen classification.  
    /// 
    /// If not, the transition matrix M is irreducible*. 
    /// The "growth" of the graph map is the spectral radius (Frobenius-Perron eigenvalue) and the associated positive eigenvector is interpreted as the widths of the edges (in the train track sense).
    /// (*) it is not necessarily irreducible, as P u pre-P is a preserved subgraph, but it defines positive weights on the edges (this is a condition on the block of M mapping G\P to P)
    /// </summary>
    /// <returns> A non-trivial essential preserved subgraph or null if it doesn't exist, i.e. the transition matrix is irreducible. </returns>
    public HashSet<UnorientedStrip> PreservedSubgraph_Reducibility()
    {
        var prePeriphery = PrePeriphery();
        var fullGraph = Strips.ToHashSet();
        var essentialSubgraph = new HashSet<UnorientedStrip>(fullGraph);
        essentialSubgraph.ExceptWith(prePeriphery);
        var edgesKnownToHaveFullOrbit = new HashSet<UnorientedStrip>();

        foreach (var edge in essentialSubgraph)
        {
            if (edgesKnownToHaveFullOrbit.Contains(edge)) 
                continue;
            var orbit = OrbitOfEdge(edge, edgesKnownToHaveFullOrbit);
            if (!orbit.IsSupersetOf(fullGraph))  
                return orbit;
            edgesKnownToHaveFullOrbit.Add(edge);
        }
        return null; 
    }

    public AlgorithmSuggestion ReducibilitySuggestion()
    {
        var preservedSubgraph = PreservedSubgraph_Reducibility();
        if (preservedSubgraph == null) return null;
        var boundaryWordText = CheckIfBoundaryWordsOfSubgraphArePreserved(preservedSubgraph, out var arePreserved);
        if (!arePreserved)
            HandleInconsistentBehavior($"The boundary words of the invariant subgraph {preservedSubgraph.ToCommaSeparatedString(e => e.ColorfulName)} are not preserved!");
        var description = "The map is reducible because there is an invariant essential subgraph, with edges "
                          + preservedSubgraph.Select(e =>
                                  peripheralSubgraph.Contains(e)
                                      ? e.ColorfulName
                                      : $"<b>{e.ColorfulName}</b>")
                              .ToCommaSeparatedString()
                          + $".\n {boundaryWordText}\n You can continue with the algorithm, but it will not necessarily terminate.";
        return new AlgorithmSuggestion(
            options: new (object, string)[] { },
            description: description, 
            buttons: new[] { AlgorithmSuggestion.ignoreReducibleButton }
        );
    }

    /// <summary>
    /// Finds the left and right eigenvectors of the essential transition matrix M_H associated to the Frobenius-Perron eigenvalue (growth) λ.
    /// </summary>
    public void FrobeniusPerron(bool essentialSubgraph, out double λ, out Dictionary<UnorientedStrip, double> widths,
        out Dictionary<UnorientedStrip, double> lengths,
        out Dictionary<UnorientedStrip, Dictionary<UnorientedStrip, int>> transitionMatrix)
    {
        var matrix = transitionMatrix = essentialSubgraph ? TransitionMatrixEssential() : TransitionMatrix();
        var strips = matrix.Keys.ToArray();
        Matrix<double> M = Matrix<double>.Build.Dense(strips.Length, strips.Length,
            (i, j) => matrix[strips[i]][strips[j]]
        );
        if (strips.Length == 0)
        { // M.Evd throws an exception if the matrix is empty
            λ = 1;
            widths = new Dictionary<UnorientedStrip, double>();
            lengths = new Dictionary<UnorientedStrip, double>();
            return;
        }
        var eigenvalueDecomposition = M.Evd();
        int
            columnIndex; // the index of the largest eigenvalue - should be the last index, as the eigenvalues are sorted by magnitude (?)
        (columnIndex, λ) = eigenvalueDecomposition.EigenValues.ArgMaxIndex(c => (float)c.Real);
        var eigenvector = eigenvalueDecomposition.EigenVectors.Column(columnIndex);
        if (eigenvector[0] < 0) eigenvector = eigenvector.Negate();
        var rightEigenvector = eigenvalueDecomposition.EigenVectors.Inverse().Row(columnIndex);
        if (rightEigenvector[0] < 0) rightEigenvector = rightEigenvector.Negate();

        eigenvector *= λ / eigenvector.GeometricMean();
        var relativeLengths = ((IEnumerable<double>)rightEigenvector).Enumerate().Select(tuple =>
        {
            var (i, length) = tuple;
            if (Math.Abs(length) < 1e-6) return 0;
            var strip = strips[i];
            var expectedLength = strip.EdgePath.Count;
            return expectedLength / length;
        });
        rightEigenvector *= relativeLengths.Where(x => x > 0).GeometricMean();

        widths = new Dictionary<UnorientedStrip, double>(from i in Enumerable.Range(0, strips.Length)
            select new KeyValuePair<UnorientedStrip, double>(strips[i], eigenvector[i]));
        lengths = new Dictionary<UnorientedStrip, double>(from i in Enumerable.Range(0, strips.Length)
            select new KeyValuePair<UnorientedStrip, double>(strips[i], rightEigenvector[i] < 1e-12 ? 0 : rightEigenvector[i]));
    }

    // todo: Extend weights to the prePeriphery. Implement the train tracks.

}