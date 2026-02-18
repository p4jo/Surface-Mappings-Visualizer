using System.Collections.Generic;
using System.Linq;

public partial class FibredSurface
{
    public IEnumerable<List<Strip>> GetPeripheralInefficiencies()
    {
        HashSet<UnorientedStrip> prePeriphery = PrePeriphery();
        foreach (var edge in OrientedEdges)
        {
            if (!prePeriphery.Contains(edge.Dg))
                continue;
            var otherEdges = Star(edge.Source).Where(
                e => Equals(e.Dg, edge.Dg)).ToList();
            if (otherEdges.Count > 1)
                yield return otherEdges;
        }
    }

    public AlgorithmSuggestion RemovePeripheralInefficiencySuggestion()
    {
        var e = GetPeripheralInefficiencies().ToArray();
        if (e.Length == 0) return null;
        return new AlgorithmSuggestion(
            options: from l in e
            // l is a list of edges with the same Dg and this Dg(l) is in the periphery.
            select (l.Select(edge => edge.Name) as object,
                    l.Select(edge => edge.ColorfulName).ToCommaSeparatedString() +
                    $" are all mapped to {l.First()!.Dg!.ColorfulName} ∈ P."
                ),
            description: "Fold initial segments of edges that map to edges in the pre-periphery.",
            buttons: new[] { AlgorithmSuggestion.peripheralInefficiencyButton }
        );
    }

    public IEnumerable<AlgorithmSuggestion> RemovePeripheralInefficiency(IReadOnlyList<Strip> foldableEdges) => 
        FoldInitialSegmentInSteps(foldableEdges); 

}