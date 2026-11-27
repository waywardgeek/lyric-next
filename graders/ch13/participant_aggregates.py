#!/usr/bin/env python3
"""Retained Ch13 scalar-only participant boundary; Ch14 lifts this restriction.

Public fixtures use the ordinary external tuple harness. Rejections must be
LYC304, not a crash or arbitrary failure. Explicit concrete owning values and
application containers remain legal. No invalid lifetime program is executed.
"""
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))
from relationships import CASES as RELATIONSHIP_CASES
from tuples import main

BASE = next(source for name, source, expected in RELATIONSHIP_CASES
            if name == 'refs-counted-membership-lifetime')


def helper(body):
    source = BASE.replace('        first: C?',
                          '        pub func remove_saved(self) {\n' + body +
                          '\n        }\n        first: C?', 1)
    return source.replace('graph.nodes.remove(graph.nodes.first!)',
                          'graph.nodes.remove_saved()')


NEGATIVE_CASES = [
    ('participant-tuple-literal', helper('''
        let saved=(self.first!, self.first!)
        self.remove(saved.0)
        print("after")'''), 'LYC304'),
    ('participant-array-literal', helper('''
        let saved=[self.first!, self.first!]
        self.remove(saved[0])
        print("after")'''), 'LYC304'),
    ('participant-tuple-annotation', helper('''
        let saved:(C,C)=(self.first!, self.first!)
        self.remove(saved.0)'''), 'LYC304'),
    ('participant-array-annotation', helper('''
        var saved:[C]=[]
        saved.push(self.first!)'''), 'LYC304'),
    ('participant-nested-tuple', helper('''
        let saved=((self.first!, 1), true)
        self.remove(saved.0.0)'''), 'LYC304'),
    ('participant-array-push', helper('''
        var saved:[Node]=[]
        saved.push(self.first!)'''), 'LYC304'),
    ('participant-array-element-store', helper('''
        var saved:[Node?]=[null]
        saved[0]=self.first'''), 'LYC304'),
    ('participant-tuple-element-store', helper('''
        var saved:(Node?,i32)=(null,1)
        saved.0=self.first'''), 'LYC304'),
]

NEGATIVE_CASES += [
    ('participant-aggregate-parameter', BASE.replace(
        '        first: C?',
        '        pub func inspect(self, children:[C]) {}\n        first: C?', 1), 'LYC304'),
    ('participant-aggregate-result', BASE.replace(
        '        first: C?',
        '        pub func inspect(self)->(C,C) { return (self.first!,self.first!) }\n        first: C?', 1), 'LYC304'),
    ('participant-aggregate-field', BASE.replace(
        '        first: C?', '        children:[C]\n        first: C?', 1), 'LYC304'),
]

# Dropping the role view explicitly creates an ordinary owner. An aggregate of
# those owners must retain its contents; removal must not invalidate that owner.
CASES = [
    ('concrete-owning-tuple-in-relationship', helper('''
        let owned:Node=self.first!
        let saved=(owned, owned)
        self.remove(saved.0)
        print("after")'''), b'liveafterNdone'),
    ('concrete-owning-array-in-relationship', helper('''
        let owned:Node=self.first!
        var saved:[Node]=[]
        saved.push(owned)
        self.remove(saved[0])
        print("after")'''), b'liveafterNdone'),
]

# Application reads acquire ordinary ownership even when a field carries a role
# view for lookup. Removing membership must not destroy an independently held child.
for name, literal, selection in (
        ('application-owning-tuple', '(graph.nodes.first!,)', 'saved.0'),
        ('application-owning-array', '[graph.nodes.first!]', 'saved[0]')):
    source = BASE.replace('graph.nodes.remove(graph.nodes.first!)',
                          'if true { let saved=' + literal +
                          '; graph.nodes.remove(' + selection + '); print("held") }')
    CASES.append((name, source, b'liveheldNdone'))


if __name__ == '__main__':
    runtime_debug = len(sys.argv) > 1 and sys.argv[1] == '--runtime-debug'
    if runtime_debug:
        del sys.argv[1]
    sys.exit(main(CASES, NEGATIVE_CASES, 'participant-aggregates',
                  emission_options=('--debug',) if runtime_debug else ()))
