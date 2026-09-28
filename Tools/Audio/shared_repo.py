"""Materialize single files from the team audio repo without touching its sparse checkout.

The repo `~/Documents/music-cell-shared-assets` is a partial clone (blob:none) with a cone-mode sparse
checkout and Git LFS. A file outside the sparse set is fetched as:

    git show HEAD:<path> | git lfs smudge -- <path> > out

`git lfs smudge` downloads the object once into the repo's own `.git/lfs/objects` cache, so later runs are
local. The repo's `.meta` files are never copied (they would duplicate GUIDs in our Unity project).
"""
import os
import shutil
import subprocess
from concurrent.futures import ThreadPoolExecutor

REPO = os.path.expanduser("~/Documents/music-cell-shared-assets")
ASSETS_PREFIX = "music-cell-shared-assets/Assets/"
FFPROBE = "/opt/homebrew/bin/ffprobe"


def repo_path(rel: str) -> str:
    """`rel` is relative to the shared Unity project's Assets folder (e.g. 'Universal Sound FX/...wav')."""
    return ASSETS_PREFIX + rel


def cache_file(cache_dir: str, rel: str) -> str:
    return os.path.join(cache_dir, rel)


def probe(path: str) -> dict:
    """Return codec/sample-rate/channels/duration via ffprobe; raises if the file is not real audio."""
    out = subprocess.run(
        [FFPROBE, "-v", "error", "-show_entries", "stream=codec_name,sample_rate,channels,bits_per_sample,duration",
         "-of", "default=noprint_wrappers=1", path],
        capture_output=True, text=True, check=True).stdout
    info = dict(line.split("=", 1) for line in out.strip().splitlines() if "=" in line)
    if not info.get("codec_name") or float(info.get("duration", 0) or 0) <= 0:
        raise RuntimeError(f"not decodable audio: {path}")
    return info


def _is_lfs_pointer(path: str) -> bool:
    with open(path, "rb") as fh:
        head = fh.read(64)
    return head.startswith(b"version https://git-lfs")


def fetch(rel: str, cache_dir: str) -> str:
    """Materialize one file into the cache (skips when a valid copy exists). Returns the local path."""
    if rel.endswith(".meta"):
        raise ValueError("never copy .meta files from the shared repo")
    dest = cache_file(cache_dir, rel)
    if os.path.exists(dest) and os.path.getsize(dest) > 1024 and not _is_lfs_pointer(dest):
        return dest
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    src = repo_path(rel)
    checked_out = os.path.join(REPO, src)
    tmp = dest + ".part"
    if os.path.exists(checked_out) and not _is_lfs_pointer(checked_out):
        shutil.copyfile(checked_out, tmp)  # inside the sparse set (e.g. NEX/Sound effect): already real on disk
    else:
        show = subprocess.Popen(["git", "-C", REPO, "show", f"HEAD:{src}"], stdout=subprocess.PIPE)
        with open(tmp, "wb") as out:
            smudge = subprocess.run(["git", "-C", REPO, "lfs", "smudge", "--", src], stdin=show.stdout, stdout=out,
                                    stderr=subprocess.DEVNULL)
        show.stdout.close()
        if show.wait() != 0 or smudge.returncode != 0:
            os.remove(tmp)
            raise RuntimeError(f"LFS fetch failed: {rel}")
    if _is_lfs_pointer(tmp):
        os.remove(tmp)
        raise RuntimeError(f"got an LFS pointer instead of audio: {rel}")
    probe(tmp)
    os.replace(tmp, dest)
    return dest


def fetch_all(rels, cache_dir: str, jobs: int = 8) -> dict:
    """Fetch many files in parallel (the cost is network latency, ~6 s per uncached file)."""
    rels = sorted(set(rels))
    with ThreadPoolExecutor(max_workers=jobs) as pool:
        paths = list(pool.map(lambda r: fetch(r, cache_dir), rels))
    return dict(zip(rels, paths))


def prune_cache(cache_dir: str, keep_rels) -> int:
    """Delete cached files that are no longer picked (re-fetching later is local: the LFS objects stay in the repo)."""
    keep = {os.path.normpath(cache_file(cache_dir, r)) for r in keep_rels}
    removed = 0
    for base, _, files in os.walk(cache_dir, topdown=False):
        for f in files:
            path = os.path.normpath(os.path.join(base, f))
            if path not in keep:
                os.remove(path)
                removed += 1
        if base != cache_dir and not os.listdir(base):
            os.rmdir(base)
    return removed
