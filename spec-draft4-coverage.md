# Draft 4 reconstruction coverage

Source: [frozen Draft 3.3](drafts/06-before-editorial-reconstruction.md), SHA-256 `f92f931db4171e8d368e5a726f90be14ee8cc34fed9d29e276abe34738094015`.

This inventory accounts for every source subsection, including section introductions. A mapped heading is not itself proof of semantic preservation: the final review must inspect rules, examples, exclusions, and open decisions. Original and interface-redesign comparisons are recorded separately below.

## Source destinations

- Lines 9–10: **1. Principles / intro** → Draft 4 §1. Section opening integrated into purpose and status.
- Lines 11–16: **1. Principles / Keep it simple** → Draft 4 §1. Rule text preserved and relocated; numbered section references updated.
- Lines 17–24: **1. Principles / Make relationships explicit** → Draft 4 §1. Rule text preserved and relocated; numbered section references updated.
- Lines 25–36: **1. Principles / Make lifetime behavior predictable** → Draft 4 §1. Rule text preserved and relocated; numbered section references updated.
- Lines 37–40: **1. Principles / Distinguish design from evidence** → Draft 4 §1. Rule text preserved and relocated; numbered section references updated.
- Lines 41–72: **2. Source organization / intro** → Draft 4 §2. Rule text preserved and relocated; numbered section references updated.
- Lines 73–103: **3. Basic syntax / intro** → Draft 4 §3. Syntax separated from binding/value semantics; examples retained in functions and bindings sections.
- Lines 104–105: **4. Types and literals / intro** → Draft 4 §4. Integrated into the adjacent consolidated rules; no independent rule removed.
- Lines 106–128: **4. Types and literals / Scalars** → Draft 4 §4. Rule text preserved and relocated; numbered section references updated.
- Lines 129–142: **4. Types and literals / Unit and absence** → Draft 4 §4. Rule text preserved and relocated; numbered section references updated.
- Lines 143–160: **4. Types and literals / Composite types** → Draft 4 §4. Rule text preserved and relocated; numbered section references updated.
- Lines 161–162: **5. Structs, enums, and classes / intro** → Draft 4 §4. Integrated into the adjacent consolidated rules; no independent rule removed.
- Lines 163–184: **5. Structs, enums, and classes / Structs** → Draft 4 §4. Rule text preserved and relocated; numbered section references updated.
- Lines 185–197: **5. Structs, enums, and classes / Enums** → Draft 4 §4. Rule text preserved and relocated; numbered section references updated.
- Lines 198–228: **5. Structs, enums, and classes / Classes** → Draft 4 §4. Rule text preserved and relocated; numbered section references updated.
- Lines 229–245: **6. Relations / intro** → Draft 4 §9. Rule text preserved and relocated; numbered section references updated.
- Lines 246–267: **6. Relations / Single-child relationships** → Draft 4 §9. Rule text preserved and relocated; numbered section references updated.
- Lines 268–277: **6. Relations / Multiple parents** → Draft 4 §9. Rule text preserved and relocated; numbered section references updated.
- Lines 278–290: **6. Relations / Labels are scopes** → Draft 4 §9. Rule text preserved and relocated; numbered section references updated.
- Lines 291–298: **6. Relations / Representation is independent of lifetime** → Draft 4 §9. Rule text preserved and relocated; numbered section references updated.
- Lines 299–324: **7. Construction and mutation / intro** → Draft 4 §10. Rule text preserved and relocated; numbered section references updated.
- Lines 325–351: **7. Construction and mutation / Fallible construction** → Draft 4 §10. Rule text preserved and relocated; numbered section references updated.
- Lines 352–378: **8. Generated destruction and `final` / intro** → Draft 4 §11. Rule text preserved and relocated; numbered section references updated.
- Lines 379–386: **8. Generated destruction and `final` / Final-hook return and failure contract** → Draft 4 §11. Rule text preserved and relocated; numbered section references updated.
- Lines 387–400: **8. Generated destruction and `final` / Final-hook effects and reentrancy** → Draft 4 §11. Rule text preserved and relocated; numbered section references updated.
- Lines 401–402: **9. Declared lifetime policies / intro** → Draft 4 §11. Integrated into the adjacent consolidated rules; no independent rule removed.
- Lines 403–408: **9. Declared lifetime policies / Ordinary classes** → Draft 4 §11. Rule text preserved and relocated; numbered section references updated.
- Lines 409–418: **9. Declared lifetime policies / Value classes** → Draft 4 §11. Rule text preserved and relocated; numbered section references updated.
- Lines 419–422: **9. Declared lifetime policies / Generated releases are observable lifetime operations** → Draft 4 §11. Rule text preserved and relocated; numbered section references updated.
- Lines 423–436: **9. Declared lifetime policies / Value-finalizer timing** → Draft 4 §11. Rule text preserved and relocated; numbered section references updated.
- Lines 437–440: **9. Declared lifetime policies / Both categories** → Draft 4 §11. Rule text preserved and relocated; numbered section references updated.
- Lines 441–442: **10. Static destruction analysis / intro** → Draft 4 §12. Integrated into the adjacent consolidated rules; no independent rule removed.
- Lines 443–458: **10. Static destruction analysis / Class-wide invalidation** → Draft 4 §12. Rule text preserved and relocated; numbered section references updated.
- Lines 459–466: **10. Static destruction analysis / No large effect language by default** → Draft 4 §12. Rule text preserved and relocated; numbered section references updated.
- Lines 467–474: **10. Static destruction analysis / Track every held reference** → Draft 4 §12. Rule text preserved and relocated; numbered section references updated.
- Lines 475–486: **10. Static destruction analysis / Reacquire through surviving structure** → Draft 4 §12. Rule text preserved and relocated; numbered section references updated.
- Lines 487–492: **10. Static destruction analysis / Persistent references are a separate obligation** → Draft 4 §12. Rule text preserved and relocated; numbered section references updated.
- Lines 493–522: **11. Safe relationship iteration / intro** → Draft 4 §12. Rule text preserved and relocated; numbered section references updated.
- Lines 523–524: **12. Arrays and array references / intro** → Draft 4 §5. Integrated into the adjacent consolidated rules; no independent rule removed.
- Lines 525–530: **12. Arrays and array references / Elements and resizing** → Draft 4 §5. Rule text preserved and relocated; numbered section references updated.
- Lines 531–557: **12. Arrays and array references / Assignment has value semantics** → Draft 4 §5. Rule text preserved and relocated; numbered section references updated.
- Lines 558–586: **12. Arrays and array references / Ordinary value parameters** → Draft 4 §6. Rule text preserved and relocated; numbered section references updated.
- Lines 587–607: **12. Arrays and array references / Slices and borrowed ranges** → Draft 4 §5. Rule text preserved and relocated; numbered section references updated.
- Lines 608–635: **12. Arrays and array references / Inferred resize requirements at function boundaries** → Draft 4 §5. Rule text preserved and relocated; numbered section references updated.
- Lines 636–652: **12. Arrays and array references / Whole-window assignment** → Draft 4 §5. Rule text preserved and relocated; numbered section references updated.
- Lines 653–678: **12. Arrays and array references / Object reference versus slot reference** → Draft 4 §5. Rule text preserved and relocated; numbered section references updated.
- Lines 679–701: **12. Arrays and array references / Named references are fixed aliases** → Draft 4 §5. Rule text preserved and relocated; numbered section references updated.
- Lines 702–719: **12. Arrays and array references / Path identity across replacement and resizing** → Draft 4 §5. Rule text preserved and relocated; numbered section references updated.
- Lines 720–746: **12. Arrays and array references / References into optional and tagged payloads** → Draft 4 §5. Rule text preserved and relocated; numbered section references updated.
- Lines 747–774: **12. Arrays and array references / Reference parameters and escape** → Draft 4 §5. Rule text preserved and relocated; numbered section references updated.
- Lines 775–782: **12. Arrays and array references / Storage categories** → Draft 4 §5. Reconciles generic replacement wording with approved checked-path identity.
- Lines 783–788: **12. Arrays and array references / Optimization** → Draft 4 §5. Rule text preserved and relocated; numbered section references updated.
- Lines 789–796: **13. Functions, closures, and generators / intro** → Draft 4 §6. Rule text preserved and relocated; numbered section references updated.
- Lines 797–813: **13. Functions, closures, and generators / Nonescaping closures** → Draft 4 §6. Rule text preserved and relocated; numbered section references updated.
- Lines 814–835: **13. Functions, closures, and generators / Loop-only generators** → Draft 4 §6. Rule text preserved and relocated; numbered section references updated.
- Lines 836–844: **13. Functions, closures, and generators / Consumer control flow** → Draft 4 §6. Rule text preserved and relocated; numbered section references updated.
- Lines 845–850: **13. Functions, closures, and generators / Safety and initial limits** → Draft 4 §6. Rule text preserved and relocated; numbered section references updated.
- Lines 851–878: **14. Control flow and errors / intro** → Draft 4 §8. Opening control-flow rules moved to §7; Result contract retained here.
- Lines 879–893: **14. Control flow and errors / Result handling and explicit discard** → Draft 4 §8. Rule text preserved and relocated; numbered section references updated.
- Lines 894–899: **14. Control flow and errors / Concrete Status values** → Draft 4 §8. Rule text preserved and relocated; numbered section references updated.
- Lines 900–925: **14. Control flow and errors / Standard status codes** → Draft 4 §8. Rule text preserved and relocated; numbered section references updated.
- Lines 926–937: **14. Control flow and errors / Status representation and Result invariants** → Draft 4 §8. Rule text preserved and relocated; numbered section references updated.
- Lines 938–957: **14. Control flow and errors / Unambiguous tagged wrapping** → Draft 4 §4. Rule text preserved and relocated; numbered section references updated.
- Lines 958–969: **14. Control flow and errors / Checked programming failures and stack traces** → Draft 4 §8. Rule text preserved and relocated; numbered section references updated.
- Lines 970–991: **15. Interfaces over cooperating types / intro** → Draft 4 §13. Rule text preserved and relocated; numbered section references updated.
- Lines 992–1018: **15. Interfaces over cooperating types / Structural satisfaction** → Draft 4 §13. Rule text preserved and relocated; numbered section references updated.
- Lines 1019–1040: **15. Interfaces over cooperating types / Interface-qualified calls and ambiguity** → Draft 4 §13. Rule text preserved and relocated; numbered section references updated.
- Lines 1041–1102: **16. Abstract relations in interfaces / intro** → Draft 4 §13. Rule text preserved and relocated; numbered section references updated.
- Lines 1103–1110: **16. Abstract relations in interfaces / Composition** → Draft 4 §13. Single-parent extends/default substitution and rationale consolidated with Appendix C composition; constraint aliases remain deferred.
- Lines 1111–1135: **17. Relationship hints are interfaces / intro** → Draft 4 §13. Rule text preserved and relocated; numbered section references updated.
- Lines 1136–1141: **18. Operators and ordinary library behavior / intro** → Draft 4 §14. Rule text preserved and relocated; numbered section references updated.
- Lines 1142–1149: **18. Operators and ordinary library behavior / Floating-point values and arithmetic** → Draft 4 §14. Rule text preserved and relocated; numbered section references updated.
- Lines 1150–1157: **18. Operators and ordinary library behavior / Initial C behavior and preferred contraction implementation** → Draft 4 §14. Rule text preserved and relocated; numbered section references updated.
- Lines 1158–1171: **18. Operators and ordinary library behavior / Implicit floating-point conversions** → Draft 4 §14. Rule text preserved and relocated; numbered section references updated.
- Lines 1172–1181: **18. Operators and ordinary library behavior / Explicit floating-point conversions** → Draft 4 §14. Rule text preserved and relocated; numbered section references updated.
- Lines 1182–1212: **18. Operators and ordinary library behavior / Integer arithmetic and explicit wrapping** → Draft 4 §14. Rule text preserved and relocated; numbered section references updated.
- Lines 1213–1224: **18. Operators and ordinary library behavior / Implicit integer conversions and mixed arithmetic** → Draft 4 §14. Rule text preserved and relocated; numbered section references updated.
- Lines 1225–1232: **18. Operators and ordinary library behavior / Mixed integer comparisons** → Draft 4 §14. Rule text preserved and relocated; numbered section references updated.
- Lines 1233–1249: **18. Operators and ordinary library behavior / Explicit integer conversions** → Draft 4 §14. Rule text preserved and relocated; numbered section references updated.
- Lines 1250–1267: **18. Operators and ordinary library behavior / Integer shifts** → Draft 4 §14. Rule text preserved and relocated; numbered section references updated.
- Lines 1268–1285: **18. Operators and ordinary library behavior / Integer division and remainder** → Draft 4 §14. String/hash/container rules moved to library overview; arithmetic unchanged.
- Lines 1286–1287: **19. Thread safety and class locks / intro** → Draft 4 §15. Integrated into the adjacent consolidated rules; no independent rule removed.
- Lines 1288–1302: **19. Thread safety and class locks / One per-class lifetime and allocation lock** → Draft 4 §15. Rule text preserved and relocated; numbered section references updated.
- Lines 1303–1312: **19. Thread safety and class locks / Payload and relationship synchronization is separate** → Draft 4 §15. Rule text preserved and relocated; numbered section references updated.
- Lines 1313–1324: **19. Thread safety and class locks / Reference acquisition and use** → Draft 4 §15. Rule text preserved and relocated; numbered section references updated.
- Lines 1325–1338: **19. Thread safety and class locks / Stable class storage** → Draft 4 §15. Rule text preserved and relocated; numbered section references updated.
- Lines 1339–1348: **19. Thread safety and class locks / Cross-class requirements and acquisition proposal** → Draft 4 §15. Rule text preserved and relocated; numbered section references updated.
- Lines 1349–1354: **19. Thread safety and class locks / Same-thread destruction and safe loops** → Draft 4 §15. Rule text preserved and relocated; numbered section references updated.
- Lines 1355–1364: **19. Thread safety and class locks / Finalizers, blocking, and thread communication** → Draft 4 §15. Rule text preserved and relocated; numbered section references updated.
- Lines 1365–1375: **20. Compiler structure and explanations / intro** → Draft 4 §17. Rule text preserved and relocated; numbered section references updated.
- Lines 1376–1406: **20. Compiler structure and explanations / Syntax grammar and parser implementation** → Draft 4 §3. Parser-choice history retained in decision archive; compiler pass responsibilities moved to §17.
- Lines 1407–1448: **21. Verification / intro** → Draft 4 §18. Rule text preserved and relocated; numbered section references updated.
- Lines 1449–1462: **22. Remaining decisions and verification boundaries / intro** → Draft 4 §19. Stale open summaries replaced by current decision register, checked against grammar review and latest decisions. No approved numeric/reference/final-hook rules reopened.
- Lines 1463–1470: **23. Provenance and current status / intro** → Draft 4 §1. Chronological rewrite history retained in the frozen draft; current reasons and exclusions stated directly.
- Lines 1471–1474: **Appendix A. Lexical and expression baseline / intro** → Draft 4 §3. Compatibility-baseline status replaced by current-rule/proposal distinction in §1 and §3.
- Lines 1475–1498: **Appendix A. Lexical and expression baseline / Tokens and literals** → Draft 4 §3. Rule text preserved and relocated; numbered section references updated.
- Lines 1499–1521: **Appendix A. Lexical and expression baseline / Precedence** → Draft 4 §3. Rule text preserved and relocated; numbered section references updated.
- Lines 1522–1537: **Appendix A. Lexical and expression baseline / Indexing, slicing, and tuple access** → Draft 4 §3. Rule text preserved and relocated; numbered section references updated.
- Lines 1538–1539: **Appendix B. Bindings, construction expressions, and control flow / intro** → Draft 4 §7. Appendix introduction contained no independent rule; subsections integrated into §§4,5,7.
- Lines 1540–1553: **Appendix B. Bindings, construction expressions, and control flow / Bindings** → Draft 4 §5. Tuple example no longer suggests the superseded tuple-error protocol; destructuring semantics unchanged.
- Lines 1554–1561: **Appendix B. Bindings, construction expressions, and control flow / Construction expressions** → Draft 4 §4. Rule text preserved and relocated; numbered section references updated.
- Lines 1562–1593: **Appendix B. Bindings, construction expressions, and control flow / Conditionals and loops** → Draft 4 §7. Rule text preserved and relocated; numbered section references updated.
- Lines 1594–1616: **Appendix B. Bindings, construction expressions, and control flow / Patterns and optionals** → Draft 4 §7. Rule text preserved and relocated; numbered section references updated.
- Lines 1617–1620: **Appendix B. Bindings, construction expressions, and control flow / Unions** → Draft 4 §4. Rule text preserved and relocated; numbered section references updated.
- Lines 1621–1622: **Appendix C. Generics and implementation forms / intro** → Draft 4 §13. Appendix preamble had no independent rule; implementation forms integrated here and generic declarations in §6.
- Lines 1623–1644: **Appendix C. Generics and implementation forms / Generic declarations and constraints** → Draft 4 §6. Rule text preserved and relocated; numbered section references updated.
- Lines 1645–1668: **Appendix C. Generics and implementation forms / Endpoint labels in constraints and qualified calls** → Draft 4 §13. Rule text preserved and relocated; numbered section references updated.
- Lines 1669–1682: **Appendix C. Generics and implementation forms / Generic classes and partial impls** → Draft 4 §13. Rule text preserved and relocated; numbered section references updated.
- Lines 1683–1714: **Appendix C. Generics and implementation forms / Method, field, and inline bindings** → Draft 4 §13. Rule text preserved and relocated; numbered section references updated.
- Lines 1715–1737: **Appendix C. Generics and implementation forms / Ownership-bearing impls and cleanup templates** → Draft 4 §13. Rule text preserved and relocated; numbered section references updated.
- Lines 1738–1745: **Appendix C. Generics and implementation forms / Composition and static requirements** → Draft 4 §13. Rule text preserved and relocated; numbered section references updated.
- Lines 1746–1749: **Appendix D. Library surface to preserve and specify / intro** → Draft 4 §16. Historical-baseline status made explicit; no accidental API migration to Result.
- Lines 1750–1773: **Appendix D. Library surface to preserve and specify / Core and conversion** → Draft 4 §16. Rule text preserved and relocated; numbered section references updated.
- Lines 1774–1781: **Appendix D. Library surface to preserve and specify / Arrays** → Draft 4 §16. Rule text preserved and relocated; numbered section references updated.
- Lines 1782–1791: **Appendix D. Library surface to preserve and specify / Strings** → Draft 4 §16. Rule text preserved and relocated; numbered section references updated.
- Lines 1792–1799: **Appendix D. Library surface to preserve and specify / Containers and builders** → Draft 4 §16. Rule text preserved and relocated; numbered section references updated.
- Lines 1800–1822: **Appendix D. Library surface to preserve and specify / Files and processes** → Draft 4 §16. Rule text preserved and relocated; numbered section references updated.
- Lines 1823–1849: **Appendix E. Tests and toolchain contracts / intro** → Draft 4 §18. Test contract integrated here; command/formatter/build flags moved to §17. No test-runner behavior removed.
- Lines 1850–1854: **Appendix F. Completeness status / intro** → Draft 4 §19. Replaces stale list of allegedly unsettled numeric/reference choices with explicit completion standard and current register.

## Review status

Semantic readback completed over the full candidate in three contiguous ranges, followed by readback of every changed source passage and every numbered/relative cross-reference. All 119 source subsections have destinations. All 72 source fenced example blocks are retained, split, or corrected with the exceptions below. These checks are editorial review, not parser execution or proof that examples compile.

The paragraph comparison initially found 474 paragraphs retained verbatim after section-number normalization and 58 changed/split passages. All 58 were read and reviewed. The number is a review aid, not a completeness score; the final editorial fixes change that exact count. The raw comparison is retained in `draft4-paragraph-review.txt` as historical audit input, not an active checklist.

## Reconciliations and example changes

- Old §3 syntax is integrated with Appendix A in new §3. `let`/`var` semantics belong in §5; `add` and `identity` examples belong in §6. A combined introductory code block was split without dropping the examples.
- Old §12 stack-reference prose vaguely said replacement could invalidate an interior reference. New §5 explicitly applies the approved checked-path replacement rule and separately retains permanent invalidation on owner destruction. It does not weaken either obligation.
- Old §14 combined control flow with errors. Control flow is now §7, the tagged-wrapping rule is §4, and Result/Status/fatal behavior is §8. Required cleanup was moved beside propagation, not removed.
- Old §§16 and C repeated interface composition. New §13 has one single-parent extends/defaults contract and preserves the reason for deferring constraint aliases. Structural capability checks do not inject storage; explicit relation/impl declarations do.
- Old §18's arithmetic example declared three values with let and then mutated them. New §14 uses var for those three bindings. No operator rule changed.
- The binding example now uses distinct local names and an ordinary pair rather than a suggestive `(value, err)` example. Tuple destructuring remains; tuples do not acquire Result propagation.
- The constructor-expression example now matches the adjacent one-argument Connection constructor. Printable and Container examples state their assumed interface context, and alternative impls are identified as alternatives rather than simultaneous conflicting definitions.
- Old Appendix A's operator table used unescaped pipes, which could break Markdown rendering. The numeric/bitwise operators are unchanged; table delimiters are escaped.
- Old §20's parser-choice chronology is retained in the frozen source and decision history. The new grammar section states current PEG behavior and proposal boundaries, while compiler pass responsibilities are in §17.
- Draft 3.3's shared/exclusive lifetime/allocation lock decision is preserved in §15. Older historical claims about exclusive payload access or a second allocator mutex are not reinstated. Payload guards and counted-update synchronization remain separate.
- Old §22 and Appendix F mixed settled decisions with open lists. New §19 names actual unfinished contracts, and distinguishes implementation verification from semantic decisions. Deferred features are not silently removed forever.
- Historical boolean/tuple library signatures remain labeled baseline inventory; this editorial pass does not silently migrate them to Result. Current error semantics remain normative for `?`.

## Comparison with the original full specification

The original 3,450-line full specification was reread in full before this reconstruction. Its current language inventory is represented through the lexical/type/function/control sections, explicit relationships and bindings, library inventory, and toolchain/test contracts. Important departures remain deliberate:

- Directory-implies-package and shared mutable parameter/slice backing are replaced by logical packages and ordinary value semantics.
- `mut`, `let mut`, inferred class lifetime, permanent/root substitutes, tuple/error propagation, unchecked casts and implicit nullable-class dereference are not revived.
- The original generator state-machine account is not the initial loop-only single-yield contract; consumer control-flow and lifetime obligations are retained.
- Original concurrency surface forms do not confer safe publication, capture, or channel behavior; those contracts remain open.
- The new numeric rules deliberately replace backend-dependent cast/overflow behavior and the old bitwise precedence.
- Placeholder maps, dangling references, permissive parser bugs, old test longjmp recovery, and unverified performance claims are not requirements.
- Historical optional syntax and incomplete library items are named as choices instead of erased: dictionary literals, positional records, pipe closures, lyric wrappers, bare imports, implements, destructor shorthand, defaults/variadics, struct/primitive methods, string/array concatenation, stdlib import behavior, and I/O contracts.

These are source-document comparisons, not a source-code audit of the original compiler. The older reference document remains preserved; it is not an alternate authority overriding the full spec or later decisions.

## Comparison with the multi-class interface redesign

The redesign's full 1,834 lines were reread, including its later implementation addenda. Draft 4 preserves complete participant/endpoint-role binding identity, scopes rather than textual-prefix dispatch, labeled constraints/calls, explicit whole-relation mappings, structural matching, partial impls, inline adapters, field bindings, owns/refs cleanup templates, visible Collection requirements, and interface defaults.

It does not revive early choices superseded in the current design: implicit scope-parent projection, mandatory zero-argument property invocation, banning explicit generic method arguments, field injection from constraint use, prohibiting legitimate cross-side updates through explicit arguments, or requiring text-substitution as the compiler's authoritative binding identity. Single-parent extends/defaults remain the chosen composition mechanism, not the early constraint-alias design.

The redesign's header says unadopted while its later sections report shipped and deferred phases. Those status tags do not establish current implementation completeness. Its later default-method-emission limitation reinforces the requirement to carry every participant and role into specialization. Single's concrete zero-or-one contract now fills its deferred single-link gap.

## Remaining limits

No compiler, PEG engine, runtime, safety proof, lock benchmark, or new numeric experiment was run. Document checks cannot prove program behavior. Coverage review preserves current obligations and exposes inherited gaps; it does not settle them. Syntax proposals D1/D2/D4/D5/D6 and header-body braces remain proposals; generic-first PEG is settled. Region syntax/order, foreign contracts, runtime finalization boundaries, library edge behavior, and cross-call safety composition remain explicit work.

## Promotion

Draft 4 was promoted to `lyric-language-spec.md` after the full reconstruction review and document checks. Only the candidate status/title paragraph changed during promotion; reviewed language rules did not. `lyric-language-spec-draft4.md` is the identical reconstruction snapshot at promotion, not a second current editing target. Draft 3.3 remains frozen. The candidate receipt is preserved; `draft4-promotion-check.json` records final equality and preservation checks.
