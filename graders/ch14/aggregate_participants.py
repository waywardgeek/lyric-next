#!/usr/bin/env python3
"""Chapter 14 ordinary ownership of participant aggregates: first retained vertical slice.

Supply COMPILER [ARG ...] after optional --soa and --runtime-debug flags.
The independent public tuple harness requires successful emission, C compilation
and execution at O0/O2, exact stdout, and empty stderr. Invalid source is never
executed. Chapter 13's historical scalar restriction fixtures remain unchanged.
"""
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "ch13"))
from participant_aggregates import CASES as OWNER_CONTROLS, helper
from tuples import main

# Ordinary containers retain each counted element, even when its source was a
# borrowed relationship link. Removal cannot finalize the retained child early.
CASES = [
    ("owning-tuple-removal-timing", helper('''
        let saved=(self.first!, self.first!)
        self.remove(saved.0)
        print("after")'''), b"liveafterNdone"),
    ("owning-array-removal-timing", helper('''
        let saved=[self.first!, self.first!]
        self.remove(saved[0])
        print("after")'''), b"liveafterNdone"),
    ("owning-annotated-tuple-removal-timing", helper('''
        let saved:(C,C)=(self.first!, self.first!)
        self.remove(saved.0)
        print("after")'''), b"liveafterNdone"),
    ("owning-nested-tuple-removal-timing", helper('''
        let saved=((self.first!, 1), true)
        self.remove(saved.0.0)
        print("after")'''), b"liveafterNdone"),
    ("owning-array-push", helper('''
        var saved:[C]=[]
        saved.push(self.first!)
        self.remove(saved[0])
        print("after")'''), b"liveafterNdone"),
    ("owning-array-element-store", helper('''
        var saved:[C?]=[null]
        saved[0]=self.first
        self.remove(saved[0]!)
        print("after")'''), b"liveafterNdone"),
    ("owning-tuple-element-store", helper('''
        var saved:(C?,i32)=(null,1)
        saved.0=self.first
        self.remove(saved.0!)
        print("after")'''), b"liveafterNdone"),
    ("owning-mixed-source-array", helper('''
        let owned:Node=self.first!
        let saved=[self.first!, owned]
        self.remove(saved[0])
        print("after")'''), b"liveafterNdone"),
    ("owning-array-copy", helper('''
        var original=[self.first!]
        let copied=original
        original=[]
        self.remove(copied[0])
        print("after")'''), b"liveafterNdone"),
    ("owning-tuple-copy", helper('''
        var original:(C?,i32)=(self.first,1)
        let copied=original
        original=(null,2)
        self.remove(copied.0!)
        print("after")'''), b"liveafterNdone"),
    *OWNER_CONTROLS,
]

# Aggregate helper signatures use ordinary ownership, unlike scalar participant
# helper parameters. Returning the value transfers its owned elements normally.
for name, annotation, literal, selection in (
        ("owning-array-helper-roundtrip", "[C]", "[self.first!]", "saved[0]"),
        ("owning-tuple-helper-roundtrip", "(C,C)",
         "(self.first!,self.first!)", "saved.0")):
    source = helper(f'''
        let saved=self.roundtrip({literal})
        self.remove({selection})
        print("after")''')
    source = source.replace("        first: C?",
        f"        func roundtrip(self, values:{annotation})->{annotation} {{ return values }}\n"
        + "        first: C?", 1)
    CASES.append((name, source, b"liveafterNdone"))

# Class fields containing class-handle arrays remain an older unsupported storage
# form (ordinary and contributed alike). Preserve its explicit compile rejection;
# local/container ownership support is not evidence of field-storage support.
field_source = helper('''
        self.held=[self.first!]
        self.remove(self.held[0])
        print("after")''')
field_source = field_source.replace("        first: C?",
                                   "        held:[C]\n        first: C?", 1)

# The temporary aggregate restriction must not be the diagnostic hiding an
# unrelated element-type error. This is compile-only, not a lifetime experiment.
NEGATIVE_CASES = [
    ("class-handle-array-field-still-unsupported", field_source, "LYC306"),
    ("participant-tuple-wrong-element-type", helper('''
        let saved:(C,i32)=(self.first!, true)
        self.remove(saved.0)'''), "LYC304"),
]

if __name__ == "__main__":
    options = []
    while len(sys.argv) > 1 and sys.argv[1] in ("--soa", "--runtime-debug"):
        option = sys.argv.pop(1)
        options.append("--debug" if option == "--runtime-debug" else option)
    sys.exit(main(CASES, NEGATIVE_CASES, "aggregate-participants",
                  emission_options=tuple(options)))
