using System;

namespace Lyric;

internal sealed partial class Compilation
{
    internal partial Compilation()
    {
    }

    internal partial void AddSource(SourceFile source)
    {
        if (Destroyed || source.Destroyed || source.Parent is not null || Sources.Contains(source))
        {
            throw new InvalidOperationException("Cannot attach source to this compilation.");
        }

        // List growth can fail; establish the backlink only after it succeeds.
        Sources.Add(source);
        source.Parent = this;
    }

    internal partial void RemoveSource(SourceFile source)
    {
        int index = Sources.IndexOf(source);
        if (source.Parent != this || index < 0)
        {
            throw new InvalidOperationException("Source does not belong to this compilation.");
        }

        Sources.RemoveAt(index);
        source.Parent = null;
    }

    internal partial void Destroy()
    {
        if (Destroyed)
        {
            return;
        }

        Destroyed = true;
        while (Sources.Count != 0)
        {
            // The child removes its own membership. Do not pre-detach it here.
            Sources[Sources.Count - 1].Destroy();
        }
    }
}

internal sealed partial class SourceFile
{
    internal partial SourceFile(Compilation parent, string path, byte[] bytes)
    {
        Path = path;
        Bytes = bytes;
        parent.AddSource(this);
    }

    internal partial void Destroy()
    {
        if (Destroyed)
        {
            return;
        }

        Parent?.RemoveSource(this);
        Destroyed = true;
    }
}

internal sealed partial class Diagnostic
{
    internal partial Diagnostic(string code, string message, int byteOffset, int line, int column)
    {
        Code = code;
        Message = message;
        ByteOffset = byteOffset;
        Line = line;
        Column = column;
    }
}
