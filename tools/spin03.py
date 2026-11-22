"""Chapter 3 local walkthrough, using the real parser's public inspection modes."""

import json
from pathlib import Path
import subprocess
import sys
import tempfile


def show_tree(root):
    """Render iteratively so the display does not impose another recursion limit."""
    pending = [(root, 0)]
    while pending:
        node, depth = pending.pop()
        anchor = '' if node['text'] is None else ': ' + repr(node['text'])
        print('  ' * depth + node['kind'] + anchor)
        for child in reversed(node['children']):
            pending.append((child, depth + 1))


command = [sys.argv[1], 'compiler/bin/Release/net10.0/Lyric.dll']
with tempfile.TemporaryDirectory(prefix='lyric-syntax-') as directory:
    source = Path(directory) / 'example.ly'
    examples = [
        ('Hello world syntax (not execution):', '--inspect-syntax',
         'func main() {\n    print("Hello, world!")\n}\n'),
        ('Multiplication groups inside addition:', '--inspect-expression',
         '1 + 2 * 3'),
    ]
    for label, mode, text in examples:
        source.write_text(text, encoding='utf-8')
        print(label, flush=True)
        result = subprocess.run(command + [mode, str(source)],
                                capture_output=True, text=True, check=True)
        if result.stderr:
            raise SystemExit('Unexpected diagnostic on valid input.')
        show_tree(json.loads(result.stdout))

    source.write_text('1 +', encoding='utf-8')
    print('An unfinished expression must fail without a partial tree:', flush=True)
    result = subprocess.run(command + ['--inspect-expression', str(source)],
                            capture_output=True, text=True)
    print(result.stderr, end='')
    if result.returncode != 1 or result.stdout or 'LYC201' not in result.stderr:
        raise SystemExit('Expected syntax diagnostic, exit 1, and empty stdout.')
