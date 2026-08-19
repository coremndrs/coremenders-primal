# Setup — Git Migration & Repository Initialisation

One-off infrastructure task. Execute in order. This is not a gameplay build and does not appear in TDD §4.

**Target repo:** `https://github.com/coremndrs/coremenders-primal.git` (empty, never pushed)
**Outcome:** Unity project root becomes the git repo root; Claude tooling and design docs move out of `Assets/`; git is initialised with no LFS; first commit pushed.

Read `Docs/VersionControl.md` for the reasoning behind these choices. Do not deviate from the no-LFS rule.

---

## Phase 0 — Preconditions

- [ ] **Close the Unity Editor.** Files are being moved out of `Assets/`. If Unity is running it will reimport mid-move and may regenerate or orphan `.meta` files.
- [ ] **Take a full manual copy of the project folder before starting** (plain zip or file copy to a second drive). This task moves and deletes files and there is no version control yet to fall back on. Delete the copy once Phase 6 verifies clean.
- [ ] Confirm the project root — the folder containing `Assets/`, `Packages/`, and `ProjectSettings/` as siblings. Every path below is relative to it.
- [ ] Confirm the GitHub repo is genuinely empty. If it was created with a README, LICENSE, or .gitignore, note that — Phase 7 handles it.

---

## Phase 1 — Move Claude tooling and docs to the root

Target layout:

```
<project root>/
├── .claude/            ← moved from Assets/
├── CLAUDE.md           ← moved from Assets/Documentation/
├── Docs/               ← moved from Assets/Documentation/
│   ├── GDD.md
│   ├── TDD.md
│   ├── TODO.md
│   ├── ManualSteps.md
│   ├── CodeFiles.md
│   ├── VersionControl.md
│   └── Setup-Git-Migration.md   ← this file
├── Assets/
├── Packages/
└── ProjectSettings/
```

- [ ] Create `Docs/` at the project root.
- [ ] Move every `.md` from `Assets/Documentation/` into `Docs/`, **except** `CLAUDE.md`, which goes to the project root itself. Claude Code auto-loads `CLAUDE.md` from the working directory and its parents — it will not be found under `Docs/`.
- [ ] Move `.claude/` (and any `.claude/skills/`, `.claude/commands/`, settings files it contains) from under `Assets/` to the project root.
- [ ] **Do not move the `.meta` files.** Delete every `.meta` that belonged to a moved file, plus `Assets/Documentation.meta` and any `.claude`-related `.meta`. Meta files are meaningless outside `Assets/` and Unity will not clean them up for you once the source folder is gone.
- [ ] Remove the now-empty `Assets/Documentation/` folder.
- [ ] Place `VersionControl.md` and this file into `Docs/` if they are not already there.

---

## Phase 2 — Repoint documentation paths

- [ ] Replace `CLAUDE.md` at the project root with the updated version supplied alongside this task. It already references `Docs/` and carries the new Version Control section.
- [ ] Search every file in `Docs/` for the string `Assets/Documentation` and update each hit to `Docs`. Check `TDD.md` and `ManualSteps.md` in particular — they cross-reference sibling docs.
- [ ] Search `.claude/` (skills, commands, settings) for the same string and update. `CodeReviewSkill.md` is a likely hit.
- [ ] Search `Assets/**/*.cs` for any hardcoded path referencing the Documentation folder. Unlikely, but a stale path in an editor script would break silently.

---

## Phase 3 — Author `.gitignore`

Create at the project root, exactly this content:

```gitignore
# ─────────────────────────────────────────────────────────────
# Unity — generated / regenerable. Never commit these.
# ─────────────────────────────────────────────────────────────
[Ll]ibrary/
[Tt]emp/
[Oo]bj/
[Bb]uild/
[Bb]uilds/
[Ll]ogs/
[Uu]ser[Ss]ettings/
[Mm]emoryCaptures/
[Rr]ecordings/
sysinfo.txt
crashlytics-build.properties

# Never ignore these — the project does not reproduce without them
!Packages/
!ProjectSettings/

# ─────────────────────────────────────────────────────────────
# IDE / tooling
# ─────────────────────────────────────────────────────────────
.vs/
.vscode/
.idea/
*.csproj
*.unityproj
*.sln
*.suo
*.user
*.userprefs
*.pidb
*.booproj
*.svd
*.pdb
*.mdb
*.opendb
*.VC.db
*.tmp

# ─────────────────────────────────────────────────────────────
# OS cruft
# ─────────────────────────────────────────────────────────────
.DS_Store*
Thumbs.db
desktop.ini

# ─────────────────────────────────────────────────────────────
# Build output
# ─────────────────────────────────────────────────────────────
*.apk
*.aab
*.app
*.unitypackage

# ─────────────────────────────────────────────────────────────
# Heavy binary content — lives in restic archives, not in git.
# See Docs/VersionControl.md. Git LFS is deliberately NOT used.
#
# The trailing * on each pattern also catches the sibling .meta
# file: "*.psd*" matches both hero.psd and hero.psd.meta.
# Ignored assets and their metas always travel together.
# ─────────────────────────────────────────────────────────────

# Source art
*.psd*
*.psb*
*.blend*
*.max*
*.ztl*
*.spp*
*.sbs*
*.xcf*

# Maya — listed explicitly, NOT as *.ma* / *.mb*. Those short patterns
# would also swallow Unity's own .mat, .mask and .mixer assets, which
# must stay tracked.
*.ma
*.ma.meta
*.mb
*.mb.meta

# Large image formats
*.exr*
*.tga*
*.tif*
*.tiff*
*.hdr*

# Models
*.fbx*
*.obj*
*.dae*
*.3ds*

# Audio
*.wav*
*.aif*
*.aiff*
*.mp3*
*.ogg*
*.flac*

# Video
*.mp4*
*.mov*
*.avi*
*.webm*

# ─────────────────────────────────────────────────────────────
# Exceptions: small, stable assets worth tracking.
# Add entries here with a comment explaining why. Do not loosen
# the rules above instead.
# ─────────────────────────────────────────────────────────────
# Example:
# !Assets/Audio/UI/click.wav
# !Assets/Audio/UI/click.wav.meta
```

Note what is deliberately **absent** from the ignore list: `.png` and `.jpg` stay tracked. They are the game-ready form, usually small, and useful to diff. `.unity`, `.prefab`, `.mat`, `.asset`, `.controller`, `.anim` stay tracked — they are YAML text and must remain mergeable.

---

## Phase 4 — Author `.gitattributes`

Create at the project root. **No `filter=lfs` lines. Verify this before committing.**

```gitattributes
# Normalise line endings in the repo, check out native on each platform.
* text=auto

# Unity writes LF in its YAML and meta files on every platform.
*.unity      text eol=lf
*.prefab     text eol=lf
*.asset      text eol=lf
*.mat        text eol=lf
*.controller text eol=lf
*.anim       text eol=lf
*.meta       text eol=lf
*.asmdef     text eol=lf
*.asmref     text eol=lf

*.cs   text diff=csharp
*.json text
*.md   text
*.yml  text

# Unity SmartMerge (UnityYAMLMerge) — see Phase 8 for the git config
*.unity      merge=unityyamlmerge
*.prefab     merge=unityyamlmerge
*.asset      merge=unityyamlmerge
*.mat        merge=unityyamlmerge
*.controller merge=unityyamlmerge
*.anim       merge=unityyamlmerge

# Tracked small binaries — no diff, no EOL conversion
*.png  binary
*.jpg  binary
*.jpeg binary
*.gif  binary
*.ico  binary
*.ttf  binary
*.otf  binary
*.dll  binary
*.so   binary
*.pdf  binary
```

---

## Phase 5 — Initialise the repository

- [ ] `git init -b main` at the project root.
- [ ] `git remote add origin https://github.com/coremndrs/coremenders-primal.git`
- [ ] `git add -A`
- [ ] **Do not commit yet.** Phase 6 runs first.

---

## Phase 6 — Verification gate (do not skip)

The first push is the one decision that is expensive to undo. Everything below must pass before committing.

- [ ] **`Library/` is not staged.** `git ls-files | grep -i "^Library/"` must return nothing. If it returns anything, the `.gitignore` was created after `git add` — run `git rm -r --cached Library` and re-check.
- [ ] **`ProjectSettings/` and `Packages/` are staged.** Both `git ls-files ProjectSettings/` and `git ls-files Packages/` must return files. If either is empty the project will not reproduce from a clone. `Packages/manifest.json` and `Packages/packages-lock.json` must both be present.
- [ ] **No LFS anywhere.** `.gitattributes` contains no `filter=lfs`, and `git lfs ls-files` returns nothing (or reports LFS is not installed — that is the desired state).
- [ ] **Total staged size is sane.** `git count-objects -vH` — `size-pack` should be in the low tens of MB for a project at this stage. Hundreds of MB means something heavy slipped through.
- [ ] **No individual file over 10 MB.** List the largest staged files and inspect anything unexpected:

  PowerShell:
  ```powershell
  git ls-files | ForEach-Object { Get-Item $_ } |
    Sort-Object Length -Descending | Select-Object -First 20 Length, FullName
  ```
  Bash:
  ```bash
  git ls-files -z | xargs -0 du -h 2>/dev/null | sort -rh | head -20
  ```

- [ ] **Scene and prefab files are staged as text.** `git diff --cached --stat -- "*.unity"` shows line counts, not "Bin". If it shows binary, Unity's Asset Serialization is not set to Force Text — stop and fix that first (Phase 8), then re-add.
- [ ] **The four assembly definitions are staged** — `Game.Simulation`, `Game.Persistence`, `Game.Networking`, `Game.Presentation`. `git ls-files "*.asmdef"` should list all four.
- [ ] **`CLAUDE.md` is at the project root**, not under `Docs/`.
- [ ] **No stray `.meta` files under `Docs/`** left over from Phase 1.

---

## Phase 7 — First commit and push

- [ ] `git commit -m "Initial commit — Unity 6 project, Cluster 2 in progress"`
- [ ] `git push -u origin main`
- [ ] If the push is rejected because GitHub initialised the repo with a README or LICENSE: `git pull --rebase origin main`, resolve, then push. Do not use `git push -f` unless you have confirmed the remote holds nothing you want.
- [ ] Confirm on GitHub that `Assets/`, `Packages/`, `ProjectSettings/`, `Docs/`, and `CLAUDE.md` are all present, and `Library/` is not.

### Optional guard — pre-commit size check

Worth adding. It catches a large file at commit time rather than at push time. Create `.git/hooks/pre-commit` (this file is local-only and not versioned):

```bash
#!/bin/sh
limit=$((10*1024*1024))
fail=0
for f in $(git diff --cached --name-only --diff-filter=ACM); do
  [ -f "$f" ] || continue
  size=$(wc -c < "$f")
  if [ "$size" -gt "$limit" ]; then
    echo "BLOCKED: $f is $((size/1024/1024))MB (>10MB). Heavy assets belong in the restic archive, not git."
    fail=1
  fi
done
exit $fail
```

Make it executable. Bypass deliberately with `git commit --no-verify` when a large file genuinely belongs in git.

---

## Phase 8 — Manual Editor and local-tooling steps

These require the Unity Editor GUI or machine-local configuration. **Append them to `Docs/ManualSteps.md` under a new `## Setup — Git & Archives` section** as unchecked `- [ ]` items, per the standard workflow in `CLAUDE.md`.

- [ ] **Verify Asset Serialization.** Unity → Edit → Project Settings → Editor. *Asset Serialization Mode* must be **Force Text**; *Version Control Mode* must be **Visible Meta Files**. Unity 6 defaults to both — confirm rather than assume. Binary serialization makes every scene and prefab an unmergeable blob.
- [ ] **Reopen the project after Phase 1** and confirm the Console is clean — no missing script references, no broken asset references from the Documentation folder removal.
- [ ] **Configure UnityYAMLMerge** so the `merge=unityyamlmerge` attributes have a driver. Path varies by Unity install location:
  ```
  git config merge.unityyamlmerge.name "Unity SmartMerge"
  git config merge.unityyamlmerge.driver "'C:/Program Files/Unity/Hub/Editor/6000.x.x/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p %O %B %A %A"
  git config merge.unityyamlmerge.recursive binary
  ```
- [ ] **Install restic** and initialise the primary archive repo per `Docs/VersionControl.md`. Store the restic password in a password manager — *not* in the project folder, and not only inside the backup it protects.
- [ ] **Initialise the second, off-site archive destination.** One local, one remote.
- [ ] **Run the first full restore test** — restore to a scratch folder, open in Unity, and work through the four-point verification checklist in `Docs/VersionControl.md`. Do this now, while the project is small.

---

## Done When

- [ ] `https://github.com/coremndrs/coremenders-primal` shows the project with `Library/` absent and `ProjectSettings/` present
- [ ] `CLAUDE.md` sits at the repo root and Claude Code loads it without being pointed at it
- [ ] No `.gitattributes` entry mentions LFS
- [ ] Two restic repos exist and a restore test has passed
- [ ] `Docs/ManualSteps.md` carries the Phase 8 section with its items ticked
- [ ] The Phase 0 manual copy has been deleted
