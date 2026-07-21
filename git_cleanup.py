#!/usr/bin/env python3
"""
revert_empty_changes.py

Scans git status for modified files and reverts "empty" changes:
  - BOM added or removed
  - Line ending style changed (CRLF <-> LF)
  - File permission changes (e.g. 644 <-> 755)
  - Ghost diffs (0-byte changes from stat cache / index mismatches)

If a file has REAL content changes mixed in, only the metadata/encoding
noise is fixed — the actual edits are preserved.

Usage:
    cd /your/repo
    python revert_empty_changes.py
"""

import subprocess
import sys
import os


def run(cmd, check=True):
    """Run a shell command, return stdout as string."""
    result = subprocess.run(
        cmd, shell=True, capture_output=True, text=True, check=check
    )
    return result.stdout.strip()


def run_bytes(cmd, check=True):
    """Run a shell command, return stdout as raw bytes."""
    result = subprocess.run(
        cmd, shell=True, capture_output=True, check=check
    )
    return result.stdout


def get_modified_files():
    """Return list of filepaths that git considers modified (unstaged)."""
    output = run("git diff --name-only")
    if not output:
        return []
    return [f for f in output.splitlines() if f.strip()]


def get_staged_modified_files():
    """Return list of filepaths that are staged and modified."""
    output = run("git diff --cached --name-only")
    if not output:
        return []
    return [f for f in output.splitlines() if f.strip()]


def get_status_modified_files():
    """Return list of filepaths that git status reports as modified or deleted.

    This catches files invisible to 'git diff --name-only' — e.g. stat-cache
    mismatches caused by autocrlf or smudge/clean filters where the normalized
    content matches but the on-disk bytes differ from the index.
    """
    output = run("git status --porcelain")
    if not output:
        return []
    files = []
    for line in output.splitlines():
        if len(line) < 4:
            continue
        index_code = line[0]
        wt_code = line[1]
        path = line[3:].strip()
        if wt_code in ('M', 'D') or index_code in ('M', 'A', 'D'):
            files.append(path)
    return files


def get_original_bytes(filepath):
    """Get file content from HEAD as raw bytes."""
    try:
        return run_bytes(f'git show "HEAD:{filepath}"')
    except subprocess.CalledProcessError:
        return None  # new file


def get_working_bytes(filepath):
    """Read current working tree file as raw bytes."""
    with open(filepath, "rb") as f:
        return f.read()


# --- BOM helpers ---

BOM_UTF8 = b"\xef\xbb\xbf"


def has_bom(data):
    return data.startswith(BOM_UTF8)


def strip_bom(data):
    return data[3:] if has_bom(data) else data


# --- Line ending helpers ---

def detect_line_endings(data):
    """Return 'crlf', 'lf', or 'mixed'."""
    has_crlf = b"\r\n" in data
    # Check for bare LF (LF not preceded by CR)
    bare_lf = data.replace(b"\r\n", b"").count(b"\n") > 0
    if has_crlf and bare_lf:
        return "mixed"
    if has_crlf:
        return "crlf"
    return "lf"


def normalize_to_lf(data):
    return data.replace(b"\r\n", b"\n")


def convert_lf_to_crlf(data):
    """Convert LF line endings to CRLF (assumes no existing CRLF)."""
    return data.replace(b"\n", b"\r\n")


# --- Permission helpers ---

def get_permission_change(filepath):
    """Check git diff for permission-only changes. Returns (old, new) or None."""
    diff_output = run(f'git diff -- "{filepath}"')
    old_mode = new_mode = None
    for line in diff_output.splitlines():
        if line.startswith("old mode "):
            old_mode = line.split()[-1]
        elif line.startswith("new mode "):
            new_mode = line.split()[-1]
    if old_mode and new_mode and old_mode != new_mode:
        return (old_mode, new_mode)
    return None


def revert_permissions(filepath, old_mode):
    """Restore original file permissions."""
    if old_mode.endswith("755"):
        os.chmod(filepath, 0o755)
    else:
        os.chmod(filepath, 0o644)
    # Also tell git index about it
    run(f'git update-index --chmod={"+" if old_mode.endswith("755") else "-"}x "{filepath}"')


# --- Main logic ---

def fix_file(filepath):
    """
    Fix empty/noise changes in a single file.
    Returns list of fix descriptions, empty if nothing to fix.
    """
    fixes = []

    original = get_original_bytes(filepath)
    if original is None:
        return fixes  # new file, skip

    working = get_working_bytes(filepath)

    # 1) Permission changes
    perm = get_permission_change(filepath)
    if perm:
        old_mode, new_mode = perm
        revert_permissions(filepath, old_mode)
        fixes.append(f"permissions {new_mode} → {old_mode}")

    # Re-read in case perms were the only diff
    working = get_working_bytes(filepath)
    if working == original:
        return fixes

    # 2) Analyze BOM difference
    orig_has_bom = has_bom(original)
    work_has_bom = has_bom(working)
    bom_changed = orig_has_bom != work_has_bom

    # 3) Analyze line-ending difference
    orig_le = detect_line_endings(strip_bom(original))
    work_le = detect_line_endings(strip_bom(working))
    le_changed = orig_le != work_le

    # 4) Check if content is actually different after normalizing BOM + LE
    orig_content = normalize_to_lf(strip_bom(original))
    work_content = normalize_to_lf(strip_bom(working))

    if orig_content == work_content:
        # ALL differences are BOM/LE noise — just restore original bytes entirely
        with open(filepath, "wb") as f:
            f.write(original)
        if bom_changed:
            fixes.append(f"BOM {'added' if work_has_bom else 'removed'} → reverted")
        if le_changed:
            fixes.append(f"line endings {orig_le} → {work_le} → reverted")
        return fixes

    # 5) There ARE real content changes. Fix only the BOM/LE noise around them.
    modified = working

    # Fix BOM: match original
    if bom_changed:
        if orig_has_bom and not work_has_bom:
            # Original had BOM, dropped-in file doesn't — restore BOM
            modified = BOM_UTF8 + strip_bom(modified)
            fixes.append("BOM was removed → restored")
        elif not orig_has_bom and work_has_bom:
            # Original had no BOM, dropped-in file added one — remove it
            modified = strip_bom(modified)
            fixes.append("BOM was added → removed")

    # Fix line endings: convert working content to match original style
    if le_changed and orig_le in ("crlf", "lf"):
        content_part = strip_bom(modified) if has_bom(modified) else modified
        bom_part = BOM_UTF8 if has_bom(modified) else b""

        if orig_le == "crlf":
            # Original was CRLF, working is LF — convert back
            content_part = convert_lf_to_crlf(normalize_to_lf(content_part))
            fixes.append("line endings LF → restored to CRLF")
        else:
            # Original was LF, working is CRLF — convert to LF
            content_part = normalize_to_lf(content_part)
            fixes.append("line endings CRLF → restored to LF")

        modified = bom_part + content_part

    # Write back only if we actually changed something
    if modified != working:
        with open(filepath, "wb") as f:
            f.write(modified)

    return fixes


def kill_ghost_diffs():
    """
    Second pass: remove ghost diffs (files git marks modified but with
    no actual content difference — 0-byte changes).
    """
    remaining_files = list(set(get_status_modified_files()))
    if not remaining_files:
        return 0

    ghost_count = 0

    for filepath in sorted(remaining_files):
        if not os.path.isfile(filepath):
            continue

        # Check if git diff actually shows any content hunks
        diff_output = run(f'git diff -- "{filepath}"', check=False)
        cached_output = run(f'git diff --cached -- "{filepath}"', check=False)
        has_hunks = (
            any(line.startswith("@@") for line in diff_output.splitlines()) or
            any(line.startswith("@@") for line in cached_output.splitlines())
        )

        if not has_hunks:
            # No real diff — this is a ghost (stat-cache mismatch, autocrlf, etc.)

            # Method 1: refresh stat cache
            run(f'git update-index --refresh -- "{filepath}"', check=False)

            # Re-check via git status (not git diff, which can miss autocrlf ghosts)
            status_check = run(f'git status --porcelain -- "{filepath}"', check=False)
            if status_check.strip():
                # Method 2: force checkout from index
                run(f'git checkout -- "{filepath}"', check=False)

                # Re-check again
                status_check2 = run(f'git status --porcelain -- "{filepath}"', check=False)
                if status_check2.strip():
                    # Method 3: nuclear — re-add and checkout from HEAD
                    run(f'git add "{filepath}"', check=False)
                    run(f'git checkout HEAD -- "{filepath}"', check=False)

            ghost_count += 1
            print(f"  👻 {filepath} — ghost diff removed")

    return ghost_count


def main():
    # Verify we're in a git repo
    try:
        run("git rev-parse --git-dir")
    except subprocess.CalledProcessError:
        print("ERROR: Not inside a git repository.", file=sys.stderr)
        sys.exit(1)

    # Collect all modified files using git status (catches autocrlf ghosts too)
    files = list(set(get_status_modified_files()))
    if not files:
        print("No modified files found in git status.")
        return

    print(f"Scanning {len(files)} modified file(s) for empty changes...\n")

    fixed_count = 0
    fully_reverted = 0

    for filepath in sorted(files):
        if not os.path.isfile(filepath):
            continue  # deleted or missing

        fixes = fix_file(filepath)
        if fixes:
            fixed_count += 1
            print(f"  ✓ {filepath}")
            for f in fixes:
                print(f"      • {f}")

            # Check if file is now identical to HEAD (fully reverted)
            if get_working_bytes(filepath) == get_original_bytes(filepath):
                fully_reverted += 1
                print(f"      → no real changes remain, fully reverted")
            print()

    print(f"{'─' * 60}")
    print(f"Pass 1 — content noise:")
    print(f"  Files with noise fixed:    {fixed_count}")
    print(f"  Fully reverted (no diff):  {fully_reverted}")
    print(f"  Files with real changes:   {fixed_count - fully_reverted}")
    print()

    # --- Second pass: ghost diffs ---
    print("Pass 2 — ghost diffs (0-byte changes)...\n")
    ghost_count = kill_ghost_diffs()

    if ghost_count:
        print(f"\n  Cleared {ghost_count} ghost diff(s).\n")
    else:
        print("  No ghost diffs found.\n")

    # Final summary
    print("─" * 60)
    remaining_status = run("git status --porcelain", check=False)
    all_remaining = set()
    for line in remaining_status.splitlines():
        if len(line) >= 4:
            code = line[:2]
            if code.strip():  # has any status code (not untracked '??')
                path = line[3:].strip()
                if code != '??':
                    all_remaining.add(path)
    print(f"Final: {len(all_remaining)} file(s) still showing as modified.")


if __name__ == "__main__":
    main()