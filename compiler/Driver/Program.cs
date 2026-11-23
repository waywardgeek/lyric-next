using System;
using System.IO;
using System.Text.Json;

namespace Lyric;

// Top-level orchestration owns the compilation lifetime and external I/O. No
// functional module needs to discover console services or import driver helpers.
internal static class Program
{
    private static int Main(string[] args)
    {
        // Options precede the filename. Unsafe changes numeric checking as well
        // as emission; layout remains a representation-only backend choice.
        bool structureOfArrays = false;
        bool unsafeMode = false;
        if (args.Length >= 2 && args[0] is "--emit-c" or "--check" or "--check-library" or "--inspect-ir")
        {
            for (int i = 1; i < args.Length - 1; i += 1)
            {
                if (args[i] == "--unsafe" && !unsafeMode)
                {
                    unsafeMode = true;
                }
                else if (args[i] == "--soa" && args[0] == "--emit-c" && !structureOfArrays)
                {
                    structureOfArrays = true;
                }
                else
                {
                    Console.Error.WriteLine("error: unknown or duplicate compiler option.");
                    return 2;
                }
            }
            args = new[] { args[0], args[^1] };
        }

        if (args.Length == 1 && args[0] == "--unicode-version")
        {
            Console.Out.WriteLine(SourceRules.UnicodeVersion);
            return 0;
        }

        if (args.Length == 2 && args[0] == "--inspect-unicode")
        {
            return InspectUnicode(args[1]);
        }

        if (args.Length != 2 || (args[0] != "--inspect-source" && args[0] != "--inspect-tokens"
            && args[0] != "--inspect-ir" && args[0] != "--emit-c" && args[0] != "--check" && args[0] != "--check-library" && args[0] != "--inspect-syntax" && args[0] != "--inspect-expression"))
        {
            Console.Error.WriteLine("Usage: Lyric --inspect-source FILE | --unicode-version");
            return 2;
        }

        Compilation compilation = new();
        try
        {
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(args[1]);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException
                || error is ArgumentException)
            {
                // Do not echo OS messages containing uncontrolled path text.
                Console.Error.WriteLine("error LYC004: Cannot read source file.");
                return 1;
            }

            SourceFile source = new(compilation, args[1], bytes);
            bool inspectTokens = args[0] == "--inspect-tokens";
            bool inspectSyntax = args[0] == "--inspect-syntax" || args[0] == "--inspect-expression";
            Diagnostic? diagnostic;
            bool emitC = args[0] == "--emit-c";
            bool inspectIr = args[0] == "--inspect-ir";
            bool check = inspectIr || emitC || args[0] == "--check" || args[0] == "--check-library";
            if (inspectSyntax || check)
            {
                diagnostic = source.Parse(args[0] == "--inspect-expression");
            }
            else
            {
                diagnostic = inspectTokens ? source.Lex() : source.Scan(null);
            }
            if (diagnostic is null && check)
            {
                diagnostic = compilation.Check(emitC || args[0] == "--check", unsafeMode);
            }
            if (diagnostic is null && inspectIr)
            {
                diagnostic = compilation.LowerIr();
                if (diagnostic is null)
                {
                    compilation.WriteIr(Console.Out);
                }
            }
            if (diagnostic is null && emitC)
            {
                diagnostic = compilation.EmitC(Console.Out, structureOfArrays);
            }
            if (diagnostic is not null)
            {
                // JSON quoting makes even an unusual filename safe to display.
                string path = JsonSerializer.Serialize(source.Path);
                Console.Error.WriteLine(
                    $"{path}:{diagnostic.Line}:{diagnostic.Column}: error {diagnostic.Code}: " +
                    $"{diagnostic.Message} (byte {diagnostic.ByteOffset})");
                return 1;
            }

            if (check)
            {
                return 0;
            }
            if (inspectSyntax)
            {
                source.WriteSyntax(Console.Out);
            }
            else if (inspectTokens)
            {
                source.WriteTokens(Console.Out);
            }
            else
            {
                source.Scan(Console.Out);
            }
            return 0;
        }
        finally
        {
            compilation.Destroy();
        }
    }

    // Unicode inspection consumes scalar data, not Lyric source. Keeping this
    // boundary separate permits the official normalization vectors to contain
    // scalars that are deliberately forbidden when written literally in source.
    private static int InspectUnicode(string path)
    {
        string[] lines;
        try
        {
            lines = File.ReadAllLines(path);
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException
            || error is ArgumentException)
        {
            Console.Error.WriteLine("error LYC004: Cannot read source file.");
            return 1;
        }

        System.Collections.Generic.List<int[]> requests = new();
        try
        {
            // Validate the entire request before emitting any success records.
            foreach (string line in lines)
            {
                int[]? scalars = JsonSerializer.Deserialize<int[]>(line);
                if (scalars is null)
                {
                    throw new JsonException();
                }

                foreach (int scalar in scalars)
                {
                    if (!System.Text.Rune.IsValid(scalar))
                    {
                        throw new JsonException();
                    }
                }

                requests.Add(scalars);
            }
        }
        catch (JsonException)
        {
            Console.Error.WriteLine("error LYC005: Invalid Unicode inspection data.");
            return 1;
        }

        foreach (int[] scalars in requests)
        {
            System.Text.StringBuilder text = new();
            bool[] start = new bool[scalars.Length];
            bool[] continuation = new bool[scalars.Length];
            for (int i = 0; i < scalars.Length; i += 1)
            {
                text.Append(new System.Text.Rune(scalars[i]).ToString());
                start[i] = IdentifierRules.IsStart(scalars[i]);
                continuation[i] = IdentifierRules.IsContinue(scalars[i]);
            }

            string normalized = IdentifierRules.Normalize(text.ToString());
            System.Collections.Generic.List<int> nfc = new();
            foreach (System.Text.Rune rune in normalized.EnumerateRunes())
            {
                nfc.Add(rune.Value);
            }

            Console.Out.WriteLine(JsonSerializer.Serialize(new { nfc, start, continuation }));
        }

        return 0;
    }

}
