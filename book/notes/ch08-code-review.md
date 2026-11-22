# Chapter 8 privileged author-reference review

## Conclusion

Two required implementation corrections remain in the lifetime graph. Both are reproducible with the existing Debug and Release compilers despite the retained suite passing. They concern conditional-result validity across cleanup and counted-array occupancy after replacement through an alias. Neither finding requires changing the chapter's acceptance contract. Add focused public regressions with the fixes; this report does not require a further review/approval round.

I made no source, grader, or shared-assembly changes, rebuilt nothing, installed nothing, used no network, and recruited no reviewers. Scratch sources were temporary and removed. This was an authorized reference review, not student reconstruction.

## Required corrections

### 1. Conditional value tails can revive an ordinary reference invalidated by branch cleanup

**Locations:** `compiler/Semantics/Check.cs:882–890` (fresh expression object identity), `BuildIf` beginning at line 1140, and `BuildBlock` at lines 1243–1255 and 1316 (tail transfer and cleanup). Compare the explicit-return protection at lines 1297–1310.

Use this shared prelude for the reproductions below:

```lyric
class D {
    x:i32
    func D(self) { self.x=1 }
}
value class V {
    func V(self) {}
    final func finish(self) {
        let dead=D()
        dead.destroy()
    }
}
```

This body is incorrectly accepted:

```lyric
func main() {
    let result=if true { let v=V(); D() } else { D() }
    assert(result.x==1)
}
```

So is the class-leaf array equivalent:

```lyric
func main() {
    let result=if true { let v=V(); [D()] } else { [D()] }
    assert(result[0].x==1)
}
```

The tail acquires its ordinary object before cleanup releases `v`. That release includes `V.finish`'s class-wide destruction effect on `D`. The saved result therefore must not emerge as a valid newly acquired object after cleanup. However, expression completion creates a fresh identity after `BuildIf` has built its branch cleanup, and the transferred tail is not checked afterward. The explicit-return path already contains precisely the relevant safeguard: build and preserve the result, run cleanup, then `UseObject` on the preserved result.

**Observed evidence:** Both bodies returned exit 0 with empty stdout/stderr under `--check` in both existing configurations. Earlier bounded probes also successfully emitted them for both AoS and SoA. As a control, `func get()->D { let v=V(); return D() }` rejected with `LYC330` in both configurations.

**Required behavior:** Preserve/check the tail's lifetime across cleanup and across the branch join rather than treating transfer as acquisition after cleanup. Cover scalar and class-leaf-array tails and retain a safe transfer control. This follows `book/chapter-08.md:149–171` and the specification's class-wide invalidation/held-reference rules: cleanup effects count, copying cannot restore validity, and validity must hold on every incoming path.

**Consequence and limit:** This is a demonstrated conservative-checking contract violation. In this deliberately minimal example the final hook destroys a different actual `D`; I am not claiming that executing this exact source demonstrates a physical dangling dereference. The contract expressly does not require proving instance identity.

### 2. Whole-array replacement through a local reference alias fails to populate the owning cleanup holder

**Locations:** `compiler/Semantics/Check.cs:1207–1240`, especially the holder lookup at 1218–1222, fallback `Release` at 1226, and indexed-destination occupancy update at 1228–1231. Relevant supporting helpers are `RegisterHolder` around 690 and `Cleanup` around 742.

With the same prelude:

```lyric
func main() {
    var d:D
    if true {
        var a:[V]=[]
        let r=ref a
        r=[V()]
        d=D()
    } else {
        d=D()
    }
    assert(d.x==1)
}
```

**Observed evidence:** Both existing Debug and Release compilers accepted this silently under `--check`; earlier probes also emitted both layouts. Replacing `r=[V()]` with `a=[V()]` produces the expected `LYC330` rejection in both configurations.

The selected storage is owned by `a`, initially known empty. The assignment looks up `target.Fact.Symbol` (`r`) in `localHolders`, rather than updating the holder belonging to the selected owner. The fallback emits a release effect at assignment, and the RHS temporary is moved, but the owning local's occupancy remains empty. The indexed-store special case does use the selected root; a named reference alias does not. Consequently the eventual release of `a` is missing its finalization effect, although emission still owns and releases the inserted element. Constructing `d` after the erroneous early effect makes the missed cleanup visible to checking.

**Required behavior:** Replacement must account for the previous contents and install occupancy on the actual selected owner, including alias spellings. Do not repair this by unconditionally finalizing every empty array: the chapter requires possible occupancy to remain separate from declared type and definite initialization. Retain the direct-owner control and add the alias case to the public checking grader; exercise analogous selected-owner paths as appropriate.

**Contract:** `book/chapter-08.md:163–171` requires occupancy updates when initially empty arrays become populated and alignment of cleanup graph scopes with emission. The specification also explicitly says references do not change an array's owner.

**Consequence and limit:** Generated finalization becomes invisible at its real cleanup point. As with finding 1, the minimal test proves failure of the documented class-wide checking policy, not actual same-instance use-after-free.

## Architecture and implementation assessment

I inspected Chapter 8, Chapter 7 and the relevant array/capacity, class lifetime, invalidation, assertion and unsafe contracts; Common semantic declarations/types; semantic model, symbol, fact, function/member-scope and class-dependency operations; the checker’s class/initialization/lifetime paths; the emitter’s object storage, arrays, builtins and control-flow ownership; Driver integration; the five public Chapter 8 grader entry points and relevant prerequisite updates.

The persistent nominal model is substantially sound in the reviewed paths:

- Common owns shared declarations and persistent relationships; spoke implementations supply partial member bodies. Emission consumes checked semantic identities, not a second name/type model. Its maps and ownership inventory are temporary emission work.
- Class identity survives array depth in the value-like semantic type. Stored nominal dependencies have reverse registrations through typed symbols and typed facts, rather than spelling-based reconstruction.
- `Semantics/ClassModel.cs:9–109` maintains member-scope and nominal-dependency relationships. `SetType` validates before detach and reserves the new reverse-list capacity before changing the old relationship.
- `SemanticSymbol.cs:121–180` and `SemanticFact.cs:33–76` establish relationships and unwind failed construction through destruction. Symbol teardown destroys owned member/function scopes and dependent facts/symbols and removes its memberships through the relationship operations. I found no additional missing backlink or duplicated authoritative class owner in those reviewed paths.
- The checker separates member discovery, body/initialization checking, and fixed-point effects. Fresh constructor-self field writes retain local borrow checking without exporting them as mutation of every existing instance. Field summaries use actual field symbols and distinguish descriptor movement from payload invalidation.
- AoS and SoA share semantic acceptance. Generated storage separates live slots from free slots; counted arrays recursively copy/drop descriptors and retain/release counted leaves, while ordinary handles do not become counted merely by being array elements. `len`, selected `capacity`, nondefaultable object-array growth, and unsafe assertion behavior have explicit implementations and retained coverage. I found no additional concrete layout or builtin defect during this review.

These observations are bounded source-review findings, not a proof that every graph path is correct. The two counterexamples show why extensive passing tests do not establish lifetime completeness.

## Teaching preference (not a separate blocking correction)

`Semantics/Check.cs:381` stores graph operations as integers, and the worklist around 512–579 interprets values such as 8, 9, 10 and 11. A small named event vocabulary or a nearby complete operation table would make occupancy, cleanup and invalidation much easier for students to reconstruct. Prefer that local clarification over a new framework or pass-through abstraction. Existing ownership comments are useful; preserve them.

**Lesson:** The lifetime identity of a transferred value must survive cleanup, and counted occupancy belongs to selected storage, not to whichever alias spells a write. Matching final output and aggregate reference-count balance cannot by themselves establish those two facts.

## Evidence and verification limits

- `book/notes/ch08-checks/object-arrays/cumulative.txt` contains successful Debug and Release cumulative receipts. Chapter 8 summaries are 145/145 checking, 212/212 layout executions, 4/4 free-slot allocation comparisons, and 12/12 owned-field/array balance comparisons in each configuration (Debug summary lines 728, 941, 946, 959; Release 1685, 1898, 1903, 1916). The cumulative Make target retains the earlier chapter suites and refreshed unsafe checks. I inspected the receipts rather than rerunning unchanged large Unicode work.
- `book/notes/ch08-checks/object-arrays/peg.txt` reports 143/143 grammar fixtures.
- The actual sanitizer receipt is `book/notes/ch08-checks/object-arrays/lyric-object-array-sanitized.txt`. It reports **208/208**, not the final 212. Do not describe it as sanitizer coverage of all final cases.
- `book/notes/ch08-checks/spin08.txt` records the scripted walkthrough for both layouts. That is retained scripted evidence, not a new human walkthrough during this review.
- `/tmp/lyric-ch08-diagnostic-build.txt` records a clean Debug build, zero warnings/errors; `/tmp/lyric-ch08-final-static.txt` ends with 145/145 checking passes. Current `compiler/Emission/Compilation.cs:560` emits `LYR012: object reference count overflow`, leaving assertion failure on `LYR010`. The reported emitted-code spelling probe is not an actual 2^64 retain/count-overflow runtime probe. The post-cumulative follow-up is narrower than a fresh full cumulative run.
- My new bounded probes used the existing `/Users/bill/.local/share/ensemble-dotnet10/dotnet` and existing Debug/Release assemblies. Three problematic cases accepted in both; direct-owner and explicit-return controls rejected in both with `LYC330`. No shared build or grader was changed to obtain these results.
- I did not establish a runtime memory-safety exploit, exercise count exhaustion, rerun every retained test, or exhaust all recursive/interprocedural occupancy combinations. No additional defects are asserted on those untested grounds.

The required corrections are implementation defects with public regression coverage gaps, not evidence that the existing grader harness comparisons are wrong and not a setup failure. Existing passing expectations should remain retained.
