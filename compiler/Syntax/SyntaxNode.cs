using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Lyric;

internal sealed partial class SyntaxNode
{
    internal partial SyntaxNode(SourceFile source, SyntaxKind kind, int textOffset, int textLength,
        GenericSpecialization? specialization, MultiClassInstantiation? multiClass)
    {
        Kind = kind;
        TextOffset = textOffset;
        TextLength = textLength;
        if (specialization is not null && multiClass is not null)
        {
            throw new InvalidOperationException("Derived syntax has exactly one specialization owner.");
        }
        if (specialization is not null
            && (source.Destroyed || specialization.Destroyed || specialization.Analysis is null
                || specialization.Analysis.Destroyed || specialization.Analysis.Compilation != source.Parent))
        {
            throw new InvalidOperationException("Derived syntax requires its original compilation.");
        }
        specialization?.AddSyntaxNode(this, source);
        multiClass?.AddSyntaxNode(this, source);
        try
        {
            source.AddSyntaxNode(this);
        }
        catch
        {
            Destroy();
            throw;
        }
    }

    internal partial void AddChild(SyntaxNode child)
    {
        if (Destroyed || child.Destroyed || Source is null || Source.Destroyed
            || child.Source != Source || child.Parent is not null
            || child.Specialization != Specialization
            || child.MultiClass != MultiClass || MultiClass?.SyntaxSealed == true
            || Specialization?.Function is not null)
        {
            throw new InvalidOperationException("Cannot attach syntax child.");
        }

        // A common source is not enough: refuse cycles before changing either end.
        for (SyntaxNode? ancestor = this; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ancestor == child)
            {
                throw new InvalidOperationException("Syntax edge would create a cycle.");
            }
        }

        if (Specialization is null && MultiClass is null)
        {
            Source.Parent?.ClearAnalysis();
        }
        Children.Add(child);
        child.Parent = this;
    }

    internal partial void RemoveChild(SyntaxNode child)
    {
        int index = Children.LastIndexOf(child);
        if (child.Parent != this || index < 0)
        {
            throw new InvalidOperationException("Syntax child has different parent.");
        }

        if (Specialization is null && MultiClass is null)
        {
            Source?.Parent?.ClearAnalysis();
        }
        else if ((Specialization is { Destroyed: false, Function: not null })
            || (MultiClass is { Destroyed: false, SyntaxSealed: true }))
        {
            throw new InvalidOperationException("Published derived syntax is structurally fixed.");
        }
        Children.RemoveAt(index);
        child.Parent = null;
    }

    internal partial int ChildCount()
    {
        return Children.Count;
    }

    internal partial SyntaxNode ChildAt(int index)
    {
        return Children[index];
    }

    internal partial void Destroy()
    {
        if (Destroyed)
        {
            return;
        }

        if (Specialization is GenericSpecialization owner && !owner.Destroyed)
        {
            owner.Destroy();
            return;
        }
        if (Specialization is null && MultiClass is null)
        {
            Source?.Parent?.ClearAnalysis();
        }
        if (MultiClass is MultiClassInstantiation multiOwner && !multiOwner.Destroyed)
        {
            multiOwner.Destroy();
            return;
        }
        // Original syntax retires facts through ClearAnalysis above. Derived
        // syntax has a narrower owner, so retire its own semantic links here.
        Fact?.Destroy();
        DeclaredSymbol?.Destroy();
        ImplementedSymbol?.Destroy();
        Parent?.RemoveChild(this);
        while (Children.Count != 0)
        {
            RemoveChild(Children[Children.Count - 1]);
        }
        // Source membership is lifetime ownership, not a transferable edge.
        // Mark retirement only after all structural links have been detached.
        Destroyed = true;
        Source?.RemoveSyntaxNode(this);
        Specialization?.RemoveSyntaxNode(this);
        MultiClass?.RemoveSyntaxNode(this);
    }

    internal partial void Write(Utf8JsonWriter writer)
    {
        // Long left-associated chains are shallow to parse but deep as trees.
        // Explicit traversal frames avoid consuming the native call stack. These
        // are temporary algorithm references, not persistent model relationships.
        Stack<(SyntaxNode Node, int NextChild)> frames = new();
        frames.Push((this, -1));
        while (frames.Count != 0)
        {
            (SyntaxNode node, int nextChild) = frames.Pop();
            if (nextChild < 0)
            {
                if (node.Destroyed || node.Source is null)
                {
                    throw new InvalidOperationException("Cannot inspect detached syntax.");
                }
                writer.WriteStartObject();
                // Derive stable JSON spelling from the closed enum vocabulary.
                StringBuilder spelling = new();
                string name = node.Kind.ToString();
                for (int index = 0; index < name.Length; index += 1)
                {
                    char character = name[index];
                    if (index != 0 && char.IsUpper(character))
                    {
                        spelling.Append('-');
                    }
                    spelling.Append(char.ToLowerInvariant(character));
                }
                writer.WriteString("kind", spelling.ToString());
                if (node.TextOffset < 0)
                {
                    writer.WriteNull("text");
                }
                else
                {
                    writer.WriteString("text", node.Source.SourceText(node.TextOffset, node.TextLength));
                }
                writer.WriteStartArray("children");
                frames.Push((node, 0));
            }
            else if (nextChild < node.Children.Count)
            {
                frames.Push((node, nextChild + 1));
                frames.Push((node.Children[nextChild], -1));
            }
            else
            {
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
        }
    }

}

internal sealed partial class SourceFile
{
    internal partial void AddSyntaxNode(SyntaxNode node)
    {
        if (Destroyed || node.Destroyed || node.Source is not null)
        {
            throw new InvalidOperationException("Cannot attach syntax node.");
        }

        if (node.Specialization is null && node.MultiClass is null)
        {
            Parent?.ClearAnalysis();
        }
        else if ((node.Specialization is not null && node.Specialization.Analysis?.Compilation != Parent)
            || (node.MultiClass is not null && node.MultiClass.Analysis?.Compilation != Parent))
        {
            throw new InvalidOperationException("Derived syntax belongs to another compilation.");
        }
        SyntaxNodes.Add(node);
        node.Source = this;
    }

    internal partial void RemoveSyntaxNode(SyntaxNode node)
    {
        int index = SyntaxNodes.LastIndexOf(node);
        if (node.Source != this || index < 0 || !node.Destroyed
            || node.Parent is not null || node.ChildCount() != 0)
        {
            throw new InvalidOperationException("Only an unlinked destroyed syntax node can leave its source.");
        }

        SyntaxNodes.RemoveAt(index);
        node.Source = null;
    }

    internal partial void ClearSyntax(int keepCount)
    {
        if (keepCount < 0 || keepCount > SyntaxNodes.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(keepCount));
        }

        while (SyntaxNodes.Count > keepCount)
        {
            SyntaxNodes[SyntaxNodes.Count - 1].Destroy();
        }
    }

    internal partial string SourceText(int offset, int length)
    {
        return Encoding.UTF8.GetString(Bytes, offset, length);
    }

    internal partial string TokenText(int index)
    {
        Token token = Tokens[index];
        return Encoding.UTF8.GetString(Bytes, token.ByteOffset, token.ByteLength);
    }

    internal partial SyntaxNode? SyntaxRoot()
    {
        return SyntaxNodes.Count == 0 ? null : SyntaxNodes[0];
    }

    internal partial void WriteSyntax(TextWriter output)
    {
        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { MaxDepth = int.MaxValue }))
        {
            SyntaxNodes[0].Write(writer);
        }
        output.WriteLine(Encoding.UTF8.GetString(buffer.ToArray()));
    }
}

internal sealed partial class SyntaxFailure
{
    internal partial SyntaxFailure(Diagnostic diagnostic)
    {
        Diagnostic = diagnostic;
    }
}
