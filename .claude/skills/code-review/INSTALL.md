# code-review skill — install

Already installed for this project. It lives at the **project root**, alongside
`CLAUDE.md` and `Docs/`:

    Coremenders - Primal Frost\
      CLAUDE.md
      Assets\Game\
      Docs\
      .claude\skills\code-review\SKILL.md
      .claude\skills\code-review\references\checklist.md
      .claude\skills\code-review\references\report-template.md

To install it into another project, unzip so that the `.claude` folder lands at
that project's root. It merges with an existing `.claude`; nothing is
overwritten unless a skill named `code-review` is already present.

Turn on "Hidden items" in Explorer's View tab to see `.claude`. If your unzip
tool wraps everything in a `code-review-skill\` folder, move `.claude` up one
level so it sits directly at the project root.

## Usage

Start Claude Code from the project root, then:

    /code-review                       # uncommitted changes (default)
    /code-review all                   # every .cs under Assets\Game\
    /code-review Networking            # one assembly
    /code-review Assets/Game/Networking/SkipManager.cs
    /code-review 0.2.9e                # a TDD build id

Output: `Docs\CodeReviews\Review_YYYY-MM-DD.md`, plus a short summary in chat.
The skill never edits code — findings only.

The skill resolves its own paths at the start of each run (SKILL.md Step 0).

`.claude\skills\` is committed, so the skill travels with the project.
