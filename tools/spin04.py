"""Chapter 4 local walkthrough: check meaning without executing Lyric source."""

from pathlib import Path
import subprocess
import sys
import tempfile


command = [sys.argv[1], 'compiler/bin/Release/net10.0/Lyric.dll']
examples = [
    ('Hello world resolves; it is not executed.',
     'func main() { println("Hello, world!") }'),
    ('The return type supplies integer context.',
     'func seven() -> u8 { return 1 + 2 * 3 }\nfunc main() { seven() }'),
    ('Only the initialized branch reaches the read.',
     'func show(flag: bool) {\n'
     '    let text: string\n'
     '    if flag { return } else { text = "ready" }\n'
     '    println(text)\n'
     '}\nfunc main() { show(false) }'),
]
with tempfile.TemporaryDirectory(prefix='lyric-semantics-') as directory:
    source = Path(directory) / 'example.ly'
    for label, text in examples:
        print(label, flush=True)
        print(text, flush=True)
        source.write_text(text, encoding='utf-8')
        result = subprocess.run(command + ['--check', str(source)],
                                capture_output=True, text=True, timeout=15)
        if result.returncode != 0 or result.stdout or result.stderr:
            raise SystemExit(f'Expected silent success: {result}')
        print('Check passed (no program execution).\n', flush=True)

    text = ('func main() {\n'
            '    let text: string\n'
            '    if true { text = "ready" }\n'
            '    println(text)\n'
            '}\n')
    print('One assigning branch is not sufficient:', flush=True)
    print(text, flush=True)
    source.write_text(text, encoding='utf-8')
    result = subprocess.run(command + ['--check', str(source)],
                            capture_output=True, text=True, timeout=15)
    print(result.stderr, end='', flush=True)
    if (result.returncode != 1 or result.stdout
            or "error LYC307: Local 'text' is not definitely assigned." not in result.stderr):
        raise SystemExit('Expected the located unassigned-read diagnostic, exit 1, and empty stdout.')
    print('Expected rejection observed; temporary files are removed.')
