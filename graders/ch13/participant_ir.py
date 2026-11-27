#!/usr/bin/env python3
"""Public Ch13 borrowed participant parameter IR contract.

Run from repository root with COMPILER [ARG ...]. Inspection only; no invalid
program is executed. Uses the public relationship fixture, never target source.
"""
import json
from pathlib import Path
import subprocess
import sys
import tempfile
from relationships import CASES


def main():
    compiler = sys.argv[1:]
    if not compiler:
        raise SystemExit("supply compiler command")
    cases = [case for case in CASES if case[0] == "refs-counted-membership-lifetime"]
    assert len(cases) == 1
    with tempfile.TemporaryDirectory(prefix="lyric-participant-ir-") as directory:
        source = Path(directory) / "case.ly"
        source.write_text(cases[0][1].replace(
            "        first: C?",
            "        pub func same_child(self, child: C?)->bool { var saved: C? = child; if true { saved = child }; let slot = ref saved; return slot! == saved! }\n        first: C?",
            1))
        result = subprocess.run(compiler + ["--inspect-ir", str(source)],
                                capture_output=True, timeout=30)
        if result.returncode != 0 or result.stderr:
            print("FAIL inspection", result.stderr.decode())
            return 1
        functions = json.loads(result.stdout)["functions"]
        removes = [function for function in functions if function["name"] == "remove"]
        assert len(removes) == 1
        nodes = removes[0]["nodes"]
        parameters = [node for node in nodes
                      if node["kind"] == "Local" and node["symbol"] == "child"]
        assert len(parameters) == 1
        child = parameters[0]["id"]
        releases = [node for node in nodes if node["kind"] == "MembershipRelease"]
        assert len(releases) == 1 and releases[0]["operands"] == [child]
        if any(node["kind"] in ("Unref", "Move") and child in node["operands"]
               for node in nodes):
            print("FAIL participant parameter still released/transferred as an owner")
            return 1
        appends = [function for function in functions if function["name"] == "append"]
        assert len(appends) == 1
        append_nodes = appends[0]["nodes"]
        append_children = [node for node in append_nodes
                           if node["kind"] == "Local" and node["symbol"] == "child"]
        assert len(append_children) == 1
        append_child = append_children[0]["id"]
        if (any(node["kind"] == "Ref" and node["operands"] == [child]
                for node in nodes)
                or any(node["kind"] == "Ref" and node["operands"] == [append_child]
                       for node in append_nodes)):
            print("FAIL borrowed participant read creates an owning copy")
            return 1
        structural_fields = {node["id"] for node in append_nodes
                             if node["kind"] == "SelectField"}
        if any(node["kind"] == "Ref"
               and any(operand in structural_fields for operand in node["operands"])
               for node in append_nodes):
            print("FAIL relationship structural field read creates an owner")
            return 1
        borrowed = {node["id"] for node in append_nodes
                    if node["kind"] == "Load" and node["operands"] == [append_child]}
        for _ in range(len(append_nodes)):
            for node in append_nodes:
                if (node["kind"] in ("WrapNullable", "UnwrapNullable")
                        and len(node["operands"]) == 1
                        and node["operands"][0] in borrowed):
                    borrowed.add(node["id"])
        if any(node["kind"] in ("Ref", "Move", "Unref")
               and any(operand in borrowed for operand in node["operands"])
               for node in append_nodes):
            print("FAIL structural nullable conversion creates or releases ownership")
            return 1
        helpers = [function for function in functions if function["name"] == "same_child"]
        assert len(helpers) == 1
        helper_nodes = helpers[0]["nodes"]
        locals_ = [node for node in helper_nodes
                   if node["kind"] == "Local" and node["symbol"] == "saved"]
        assert len(locals_) == 1
        saved = locals_[0]["id"]
        if any(node["kind"] in ("Ref", "Move", "Unref")
               and saved in node["operands"] for node in helper_nodes):
            print("FAIL participant local acquires or releases ownership")
            return 1
        stores = [node for node in helper_nodes
                  if node["kind"] == "Store" and node["operands"][0] == saved]
        assert len(stores) == 2
        definitions = {node["id"]: node for node in helper_nodes}
        if any(definitions[node["operands"][1]]["kind"] == "Move" for node in stores):
            print("FAIL participant local store consumes ownership")
            return 1
        unwraps = [node for node in helper_nodes if node["kind"] == "UnwrapNullable"]
        assert len(unwraps) == 2
        definitions = {node["id"]: node for node in helper_nodes}
        if any(definitions[node["operands"][0]]["kind"] != "Load" for node in unwraps):
            print("FAIL nullable participant unwrap consumes an owner")
            return 1
        borrowed_unwraps = {node["id"] for node in unwraps}
        if any(node["kind"] in ("Ref", "Move", "Unref")
               and any(operand in borrowed_unwraps for operand in node["operands"])
               for node in helper_nodes):
            print("FAIL borrowed nullable unwrap gains ownership or cleanup")
            return 1
        print("PASS borrowed participant wrapping and unwrapping (2/2)")
        return 0


if __name__ == "__main__":
    sys.exit(main())
