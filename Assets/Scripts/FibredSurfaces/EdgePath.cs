using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

public class EdgePath : IReadOnlyList<Strip>
{
    /// <summary>
    /// This is a normal edge path as before. Every EdgePath is built like a tree consisting of EdgePaths, and the leaves are EdgePaths where normalEdgePath is not null
    /// </summary>
    private readonly Strip[] normalEdgePath;
    /// <summary>
    /// This is the tree-like structure of EdgePaths. internalEdgePath is not null iff normalEdgePath is not null.
    /// </summary>
    private readonly EdgePath[] internalEdgePath;

    private string toString = null;

    public EdgePath()
    {
        normalEdgePath = Array.Empty<Strip>();
    }
    
    public EdgePath(params Strip[] strips)
    {
        normalEdgePath = strips;
    }

    public EdgePath(params EdgePath[] internalEdgePath)
    {
        this.internalEdgePath = internalEdgePath;
    }
    public EdgePath(IEnumerable<Strip> strips) : this(strips.ToArray())
    {   }
    
    public EdgePath(IEnumerable<EdgePath> strips) : this(strips.ToArray())
    {   }

    public static implicit operator EdgePath(List<Strip> list) => new(list.ToArray());
    public static implicit operator EdgePath(Strip[] list) => new(list);
    public static implicit operator EdgePath(Strip strip) => new(strip);

    public EdgePath ConjugateLeft(EdgePath c) => new(c, this, c.Inverse()) { toString = $"({c})°({this})" };
    
    public EdgePath ConjugateRight(EdgePath c) => new(c.Inverse(), this, c) { toString = $"({this})^({c})" };

    public EdgePath Inverse()
    {
        if (normalEdgePath != null)
        {
            Strip[] reversedNormalEdgePath = new Strip[normalEdgePath.Length];
            for (int i = 0; i < normalEdgePath.Length; i++) 
                reversedNormalEdgePath[i] = normalEdgePath[^(i + 1)].Reversed();
            return new EdgePath(reversedNormalEdgePath);
        }

        return new EdgePath(internalEdgePath.Reverse().Select(path => path.Inverse()));
    }

    public IEnumerator<Strip> GetEnumerator()
    {
        return (normalEdgePath as IEnumerable<Strip>)?.GetEnumerator() ??
            internalEdgePath.SelectMany(edgePath => edgePath).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public EdgePath Replace(IReadOnlyDictionary<UnorientedStrip,Strip> newEdges)
    {
        if (normalEdgePath != null)
        {
            return new EdgePath(normalEdgePath.Select(
                strip => strip is OrderedStrip { reverse: true }
                    ? newEdges[strip.UnderlyingEdge].Reversed()
                    : newEdges[strip.UnderlyingEdge]
            ));
        }

        return new EdgePath(internalEdgePath.Select(
            path => path.Replace(newEdges)
        ));
    }
    public EdgePath Replace(IReadOnlyDictionary<UnorientedStrip,UnorientedStrip> newEdges)
    { // this is copied from the above Replace
        if (normalEdgePath != null)
        {
            return new EdgePath(normalEdgePath.Select(
                strip => strip is OrderedStrip { reverse: true }
                    ? newEdges[strip.UnderlyingEdge].Reversed()
                    : newEdges[strip.UnderlyingEdge]
            ));
        }

        return new EdgePath(internalEdgePath.Select(
            path => path.Replace(newEdges)
        ));
    }

    public EdgePath Replace(IReadOnlyDictionary<UnorientedStrip, EdgePath> newEdgePaths)
    {
        if (normalEdgePath != null)
        {
            return new EdgePath(normalEdgePath.Select(
                strip => strip is OrderedStrip { reverse: true }
                    ? newEdgePaths[strip.UnderlyingEdge].Inverse()
                    : newEdgePaths[strip.UnderlyingEdge]
            ));
        }
        
        return new EdgePath(internalEdgePath.Select(
            path => path.Replace(newEdgePaths)
        ));
    }

    public EdgePath Concat(EdgePath other)
    {
        if (normalEdgePath != null && other.normalEdgePath != null)
            return new(normalEdgePath.Concat(other.normalEdgePath));
        if (normalEdgePath != null)
            return new(other.internalEdgePath.Prepend(this));
        if (other.normalEdgePath != null)
            return new(internalEdgePath.Append(other));
        return new(internalEdgePath.Concat(other.internalEdgePath));
    }

    public int Count => normalEdgePath?.Length ?? internalEdgePath.Sum(path => path.Count);

    public Strip this[int index] => TryGetElementAt(ref index, out var result) ? result : null;

    private bool TryGetElementAt(ref int i, out Strip result)
    {
        var c = Count;
        if (i >= c)
        {
            i -= c;
            result = null;
            return false;
        }

        if (normalEdgePath != null)
        { 
            result = normalEdgePath[i];
            return true;
        }

        foreach (var edgePath in internalEdgePath)
        {
            if (edgePath.TryGetElementAt(ref i, out result))
                return true;
        }

        throw new ArithmeticException(
            "EdgePath.Count must have been calculated incorrectly, as trying to access edgePath[i] didn't work.");
    }

    public static EdgePath FromString(string text, IEnumerable<UnorientedStrip> strips) =>
        FromString(text, strips.Concat(
            strips.Select(edge => edge.Reversed())
        ).ToDictionary(
            strip => strip.Name
        ));

    public static EdgePath FromString(string text, IReadOnlyDictionary<string, Strip> stripDict)
    {
        if (text.Count(c => c == '(') != text.Count(c => c == ')'))
            throw new ArgumentException("The expression for the edge path contains a different number of closing and opening parentheses");

        var result = new List<EdgePath>();
        var currentlyReadNormalEdgePath = new List<Strip>();
        var currentlyReadName = "";
        Strip lastStrip = null;
        EdgePath lastThing = null;
        int i = 0;
        for (; i < text.Length; i++)
        {
            var c = text[i];
            switch (c)
            {
                case '*':
                case '·':
                case ' ':
                case '\t':
                case '\n':
                    PushLastThing();
                    CloseLastVariable();
                    PushLastVariable();
                    break;
                case '\'':
                    CloseLastVariable();
                    if (lastStrip != null)
                    {
                        lastStrip = lastStrip.Reversed();
                        break;
                    }
                    if (lastThing != null)
                    {
                        lastThing = lastThing.Inverse();
                        break;
                    }
                    throw new ArgumentException($"Misplaced ' in your input at location {i}");

                case '°':
                    CloseLastVariable();
                    PromoteLastVariableToLastThing();
                    PushLastNormalEdgePath(); // the edge path before the last variable
                    var nextThing = ReadNextThing();
                    lastThing = nextThing.ConjugateLeft(lastThing);
                    break;
                case '(':
                    PushLastThing();
                    CloseLastVariable();
                    PushLastVariable();
                    PushLastNormalEdgePath();
                    lastThing = ReadParentheses();
                    break;
                case ')':
                    throw new ArgumentException(
                        $"There is an unmatched closing parenthesis in your input at location {i}");
                case '^':
                    CloseLastVariable();
                    PromoteLastVariableToLastThing();
                    PushLastNormalEdgePath();
                    nextThing = ReadNextThing();
                    lastThing = lastThing.ConjugateRight(nextThing);
                    break;
                default:
                    PushLastThing();
                    PushLastVariable();
                    currentlyReadName += c;
                    break;
            }
        }
        PushLastThing();
        if (currentlyReadName != "")
        {
            CloseLastVariable();
            PushLastVariable();
        }
        if (currentlyReadNormalEdgePath.Count > 0)
            PushLastNormalEdgePath();
        return result.Count == 1 ? result[0] : new EdgePath(result);

        void CloseLastVariable()
        {
            if (currentlyReadName == "")
                return;
            if (!stripDict.TryGetValue(currentlyReadName, out lastStrip))
                throw new ArgumentException($"Unknown strip {currentlyReadName} in your input at location {i}");
            currentlyReadName = "";
        }

        
        void PushLastVariable()
        {
            if (lastStrip != null)
                currentlyReadNormalEdgePath.Add(lastStrip);
            lastStrip = null;
        }

        void PushLastNormalEdgePath()
        {
            if (currentlyReadNormalEdgePath.Count > 0) 
                result.Add(new(currentlyReadNormalEdgePath));
            currentlyReadNormalEdgePath.Clear();
        }

        void PushLastThing()
        {
            if (lastThing != null)
                result.Add(lastThing);
            lastThing = null;
        }
        
        void PromoteLastVariableToLastThing()
        {
            if (lastStrip != null)
                lastThing = lastStrip;
            lastStrip = null;
        }

        EdgePath ReadParentheses()
        {
            var startIndex = i + 1;
            i = text.IndexOf(')', startIndex); // will be increased after the call to this
            return FromString(text.Substring(startIndex, i - startIndex), stripDict);
        }

        EdgePath ReadNextThing()
        {
            i++;
            for (; i < text.Length; i++)
            {
                var c = text[i];
                switch (c)
                {
                    case '*':
                    case '·':
                    case ' ':
                        if (CloseAndReturnLastVariable(out var readStrip))
                            return readStrip;
                        break;
                    case '\'':
                        if (CloseAndReturnLastVariable(out readStrip))
                        {
                            var stripReversed = readStrip?.Reversed();
                            return stripReversed;
                        }
                        throw new ArgumentException($"Misplaced ' in your input at location {i}");
                    case '°':
                    case '^':
                        if (CloseAndReturnLastVariable(out readStrip))
                        {
                            i--; // read the ° or ^ again. (left-grouping)
                            return readStrip;
                        }
                        throw new ArgumentException(
                            $"There are consecutive ° and ^ symbols in your input at location {i}");
                    case '(':
                        if (CloseAndReturnLastVariable(out readStrip))
                        {
                            i--; // read the ( again.
                            return readStrip;
                        }
                        return ReadParentheses();
                    case ')':
                        throw new ArgumentException(
                            $"There is an unexpected closing parenthesis in your input at location {i} after a ° or ^");
                    default:
                        currentlyReadName += c;
                        break;
                }
            }

            if (CloseAndReturnLastVariable(out var strip))
                return strip;
            throw new ArgumentException("Your input ended unexpectedly.");

            bool CloseAndReturnLastVariable(out Strip edgePath)
            {
                CloseLastVariable();
                if (lastStrip != null)
                {
                    edgePath = lastStrip;
                    lastStrip = null;
                    return true;
                }
                edgePath = null;
                return false;
            }
        }
    }

    public override string ToString() => ToString(150, 10);

    public string ToString(int maxLength, int tail) => 
        ToColoredString(maxLength, tail, obj => obj.ToString());

    public string ToColorfulString(int maxLength, int tail) =>
        ToColoredString(maxLength, tail, obj => ((IDrawable)obj).ColorfulName);

    private string ToColoredString(int maxLength, int tail, Func<Strip, string> colorfulName)
    {
        if (normalEdgePath != null)
        {

            if (normalEdgePath.Length <= maxLength)
                return normalEdgePath.ToCommaSeparatedString(colorfulName, " ");
                
            var initialCount = maxLength - tail;
            var initialEdges = normalEdgePath[0..initialCount].ToCommaSeparatedString(colorfulName, " ");;
            var terminalEdges = normalEdgePath[^tail..].ToCommaSeparatedString(colorfulName, " ");
            return $"{initialEdges} ... {terminalEdges}";
        }

        throw new NotImplementedException();
    }
}
