# VersionControl.md

How source control and backups work for Coremenders — Primal Frost.

Repository: `https://github.com/coremndrs/coremenders-primal.git`

---

## The Model in One Paragraph

**Git holds the project's text and its small binaries. Archives hold everything.** Git is the history of how the game was built — code, scenes, prefabs, settings, docs. Archives are the disaster-recovery and heavy-asset store — a full, self-contained copy of the project folder at a point in time. Git LFS is **not** used.

The critical consequence: **a `git clone` alone does not produce a working project.** Large binary assets are deliberately absent from git. The archive is the restore path; git is the code history. A green `git push` is not a backup.

---

## Why Not LFS

Considered and rejected, deliberately:

- Large assets in this project churn heavily during development. LFS stores **every version of every binary permanently** — a re-exported 400 MB model is 400 MB of quota, every time.
- LFS objects **cannot be selectively deleted**. Per GitHub's own documentation, removing files from LFS leaves the objects on remote storage counting against quota; the only remedy is deleting and recreating the entire repository.
- GitHub's free tier is 1 GB storage and 1 GB/month bandwidth. High-churn binaries exhaust that in weeks with no way back.

Archives are prunable. LFS is not. That asymmetry is the whole reason for this design.

**Rule for all contributors, human and AI: do not run `git lfs install`, do not add `filter=lfs` entries to `.gitattributes`, do not `git lfs track` anything.** If the asset strategy ever changes, that is a deliberate decision recorded here first — not a convenience step taken mid-task.

---

## What Git Tracks

Everything needed to reason about, review, and merge the project:

- All `.cs` source, `.asmdef` assembly definitions
- `.unity` scenes, `.prefab`, `.mat`, `.asset`, `.controller`, `.anim` — these are YAML text under Force Text serialization and must stay diffable and mergeable
- `ProjectSettings/` and `Packages/` (`manifest.json`, `packages-lock.json`) — without these the project does not reproduce
- `.meta` files for every tracked asset — GUID stability depends on them
- Small game-ready binaries: `.png`, `.jpg` UI icons and sprites, small fonts
- `Docs/`, `CLAUDE.md`, `.claude/` — **except `.claude/settings.local.json`**, which holds machine-specific permission paths and is personal, not shared

## What Git Ignores

Two categories, for different reasons.

**Regenerable Unity output** — `Library/`, `Temp/`, `Obj/`, `Logs/`, `Build/`, `UserSettings/`, IDE project files. These rebuild themselves and are enormous. `Library/` alone can be tens of gigabytes.

**Heavy binary content** — source art (`.psd`, `.blend`, Substance files), models (`.fbx`, `.obj`), audio (`.wav`, `.ogg`, `.mp3`), video (`.mp4`, `.mov`), large image formats (`.exr`, `.tga`, `.tif`). These live in `Assets/` on disk and load normally in the Editor; they are simply absent from git and restored from archive.

**Ignored assets have their `.meta` files ignored too.** They travel together. The archive is authoritative for both, which keeps GUIDs consistent on restore. Never commit one without the other.

If a specific heavy asset is small and stable enough to be worth tracking, add an explicit `!` exception in `.gitignore` with a comment saying why — don't loosen the general rule.

## Empty Folders

Git cannot track an empty directory, but Unity generates a `.meta` with a GUID for every folder. A folder that is empty in git (because its contents are gitignored, or because it's a placeholder for future content) ships its `.meta` with no folder to accompany it — on a fresh clone Unity logs *"meta file exists but its asset can't be found"* and deletes the meta, losing the folder GUID.

**Put a `.gitkeep` in any folder that would otherwise be empty in git.** Unity ignores files beginning with a dot, so no additional `.meta` is generated. This matters most for the heavy-content folders (`Art/Models`, `Art/Textures`, `Audio`), which are empty in git permanently by design.

## Verifying No LFS

Check with `git check-attr`, not with `git lfs` commands:

```
git ls-files -z | xargs -0 git check-attr filter | grep "filter: lfs"
```

Empty output means nothing routes through an LFS filter. Note that **running any `git lfs` command in this repo writes an `[lfs] repositoryformatversion` marker into `.git/config`** — harmless, but it means `git lfs ls-files` dirties the thing it is being used to inspect. Use the check above instead.

Git for Windows bundles git-lfs and defines `filter.lfs.*` in global config. That is inert here: LFS only activates via a `filter=lfs` entry in `.gitattributes`, and there are none.

---

## The Archive Ritual

Archives use **restic** — content-addressed, deduplicated, incremental, encrypted. Deduplication is what makes high-churn assets affordable: re-exporting a large model with minor edits stores only the changed chunks, not another full copy.

### Snapshot cadence

**At every build boundary, tag and archive together.** This is the rule that preserves reproducibility.

When a build from TDD §4 is complete and its acceptance criteria pass:

```
git tag build-0.2.3
git push origin build-0.2.3
restic -r <repo> backup <project> --tag build-0.2.3
```

Then record the restic snapshot ID next to the build entry in `ManualSteps.md`. The git tag and the archive snapshot are one coherent pair — together they answer "restore build 0.2.3 exactly," which neither can answer alone.

Take additional archives whenever convenient (daily, or after a heavy art session) for ordinary disaster recovery. Those are prunable. **Build-boundary snapshots are never pruned.**

### Setup

```
restic init --repo G:\CoremendersBackup
```

Set `RESTIC_PASSWORD` (or `RESTIC_PASSWORD_FILE`) in the environment. **Store the password somewhere outside the backup** — a lost restic password means an unrecoverable repository. A password manager, not a file in the project.

### Taking a snapshot

```
restic backup "F:\Latest\Coremenders - Primal Frost\Coremenders - Primal Frost" ^
  --exclude-file "F:\Latest\Coremenders - Primal Frost\Coremenders - Primal Frost\Docs\restic-excludes.txt" ^
  --tag build-0.2.3
```

`RESTIC_REPOSITORY` and `RESTIC_PASSWORD_FILE` are set as persistent **User** environment
variables on this machine, so `-r` and a password prompt are both unnecessary.

**The exclude list lives in `Docs/restic-excludes.txt`, not in inline flags** — inline flags are
where mistakes hide. The first snapshot (`68561dd6`) passed `--exclude Obj` and archived 14.8 MiB
of the lowercase `obj` MSBuild folder regardless, because restic's patterns are case-sensitive;
it also never excluded `.vs`, which was another 172.7 MiB of Visual Studio cache. 70% of that
snapshot was regenerable junk. A tracked exclude file is reviewable, travels with the repo, and
can be fixed once for every machine.

**Include `.git/` in the archive.** Excluding `Library/` is what keeps snapshots small; including `.git/` is what makes each snapshot a complete, self-contained project that opens without a separate clone step.

### Retention

```
restic -r G:\CoremendersBackup forget --keep-tag build --keep-daily 7 --keep-weekly 8 --prune
```

`--keep-tag build` is doing the important work: it protects every build-boundary snapshot from pruning regardless of age. Ordinary snapshots roll off.

### Two destinations, always

A single USB stick is not a backup — same desk, same fire, same theft, and unpowered flash degrades over time. Run the same command against a second repo:

```
restic -r b2:coremenders-backup:/ backup ...
```

Backblaze B2 runs a few dollars a month at this scale — less than the LFS data packs this design avoids.

---

## Restore Procedure

```
restic -r G:\CoremendersBackup snapshots
restic -r G:\CoremendersBackup restore <snapshot-id> --target C:\Restore\Coremenders
```

Open the restored folder in Unity 6 LTS. Unity rebuilds `Library/` on first open — expect a long import.

**Verification checklist** — a restore is only proven when all four pass:

- [ ] Project opens with no compile errors in the Console
- [ ] No "missing script" or "missing reference" warnings on the main scene
- [ ] `git status` inside the restored folder is clean and `git log` shows the expected history
- [ ] Enter Play mode and confirm the game reaches its normal starting state

### Test a restore now, and once per cluster after that

Backup schemes fail silently. Run the full restore-and-verify once immediately after setup, while the project is still small, and again at each cluster boundary. An untested backup is not a backup.

---

## Integrity

```
restic -r G:\CoremendersBackup check
```

Run occasionally — it verifies repository structure and catches bit rot before you need the data.

---

## Rules Summary

1. Git LFS is never used. No `filter=lfs` in `.gitattributes`, no `git lfs track`.
2. Unity's Asset Serialization stays on **Force Text** and Version Control Mode on **Visible Meta Files**. Binary serialization makes scenes and prefabs unmergeable.
3. Ignored assets and their `.meta` files are ignored together, never separately.
4. `ProjectSettings/` and `Packages/` are always tracked. A repo without them does not reproduce the project.
5. Every build boundary gets a git tag **and** a tagged restic snapshot, with the snapshot ID recorded in `ManualSteps.md`.
6. Two archive destinations, minimum. One of them off-site.
7. Never commit a file over ~10 MB without a deliberate reason. If it's large, it belongs in the archive.
