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
        if (args.Length == 1 && args[0] == "--unicode-version")
        {
            Console.Out.WriteLine(SourceRules.UnicodeVersion);
            return 0;
        }

        if (args.Length != 2 || args[0] != "--inspect-source")
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
            Diagnostic? diagnostic = source.Scan(null);
            if (diagnostic is not null)
            {
                // JSON quoting makes even an unusual filename safe to display.
                string path = JsonSerializer.Serialize(source.Path);
                Console.Error.WriteLine(
                    $"{path}:{diagnostic.Line}:{diagnostic.Column}: error {diagnostic.Code}: " +
                    $"{diagnostic.Message} (byte {diagnostic.ByteOffset})");
                return 1;
            }

            source.Scan(Console.Out);
            return 0;
        }
        finally
        {
            compilation.Destroy();
        }
    }
}
