# Report template

Write to `<DOC>/CodeReviews/Review_YYYY-MM-DD.md`.
Follow this structure exactly. Findings are numbered across the whole report,
ordered most severe first — not grouped by file.

---

```markdown
# Code Review — YYYY-MM-DD

**Scope:** <what was reviewed — "uncommitted changes", "Game.Networking", "build 0.2.9e">
**Files reviewed:** <n>
**Commit:** <short sha of HEAD>

| Severity | Confirmed | Plausible |
|---|---|---|
| Critical | 0 | 0 |
| High | 0 | 0 |
| Medium | 0 | 0 |
| Low | 0 | 0 |

**Verdict:** <one sentence — is this code safe to build on, and what is the single most important thing to fix>

---

## Findings

### 1. <Short title stating the defect, not the symptom>

- **Severity:** Critical | High | Medium | Low
- **Verdict:** Confirmed | Plausible
- **Location:** `<G>/Networking/Example.cs:142`
- **Rule:** <CLAUDE.md invariant or NGO rule number, if this violates one — otherwise omit>

**The code**

```csharp
// the offending lines, quoted verbatim, with just enough surrounding context
```

**Why it is wrong**

<Two or three sentences. What the code does versus what it must do.>

**How it fails**

<Concrete sequence: specific inputs or events → specific wrong outcome. Name the
actor — host, owning client, late joiner — and the observable symptom. If
Plausible, state exactly which branch you could not verify.>

**Proposed fix**

<The change, precisely enough to act on. A code sketch is fine. Do not apply it.>

---

### 2. <next finding>

...

---

## Sweeps run

| Check | Result |
|---|---|
| M1 — base calls on lifecycle overrides | clean / n hits → finding #x |
| M2 — unsubscribe-before-subscribe | clean / ... |
| M3 — despawn teardown symmetry | ... |
| M4 — StopAllCoroutines on despawn | ... |
| M5 — ResetStatics on statics | ... |
| M6 — Simulation purity | ... |
| M7 — strings on the wire | ... |
| M8 — WorldState vs worldOnly | ... |
| M9 — numbered fields | ... |
| M10 — layer boundaries | ... |
| M11 — namespace convention | ... |

<Add a row for any sweep skipped, with the reason. "Not run" is an honest entry;
omitting it is not.>

## Already tracked

<Issues found that are already logged in TODO.md — one line each with the TODO
reference. Not counted in the severity table.>

## Not reviewed

<Anything in the nominal scope that was skipped, and why — obsolete stub, legacy
file, generated code, too large to read in this pass.>
```

---

## Notes on writing findings

- **Title states the defect.** "Buff subscription never unsubscribed on despawn",
  not "Issue in DreamerBuffSync".
- **One finding per defect.** The same root cause in five files is one finding
  with five locations listed, not five findings.
- **Quote real lines.** If the quoted code does not match the file, the whole
  report becomes untrustworthy.
- **"How it fails" is mandatory.** A finding without a concrete failure path was
  not verified and should not be in the report.
- **A clean report is a valid report.** If nothing survived verification, say so,
  fill in the sweeps table, and keep it short.
