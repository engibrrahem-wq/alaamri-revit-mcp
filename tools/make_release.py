"""Author-only: package a release and write version.json.

Usage:  python tools/make_release.py 0.2.0 "Short release notes"
Then (after review):
  git add -A && git commit -m "Release v0.2.0"
  gh release create v0.2.0 build/release/alaamri-revit-mcp-v0.2.0.zip -t v0.2.0 -n "notes"
  git push            # version.json on main is what installed copies read
Upload the release asset BEFORE pushing version.json, so nobody is pointed at a missing file.
"""
import datetime, hashlib, json, os, re, subprocess, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
REPO = "https://github.com/engibrrahem-wq/alaamri-revit-mcp"

ver, notes = sys.argv[1].lstrip("v"), (sys.argv[2] if len(sys.argv) > 2 else "")
for path, pat, rep in [("server.py", r'__version__ = "[^"]+"', f'__version__ = "{ver}"'),
                       ("pyproject.toml", r'^version = "[^"]+"', f'version = "{ver}"')]:
    p = os.path.join(ROOT, path)
    s = open(p, encoding="utf-8").read()
    open(p, "w", encoding="utf-8", newline="").write(re.sub(pat, rep, s, count=1, flags=re.M))

out_dir = os.path.join(ROOT, "build", "release")
os.makedirs(out_dir, exist_ok=True)
name = f"alaamri-revit-mcp-v{ver}"
zpath = os.path.join(out_dir, name + ".zip")
# working tree incl. uncommitted version bump, tracked files only
subprocess.check_call(["git", "add", "-A"], cwd=ROOT)
tree = subprocess.check_output(["git", "write-tree"], cwd=ROOT, text=True).strip()
subprocess.check_call(["git", "archive", "--format=zip", f"--prefix={name}/", "-o", zpath, tree], cwd=ROOT)
sha = hashlib.sha256(open(zpath, "rb").read()).hexdigest()

manifest = {"version": ver, "released": datetime.date.today().isoformat(), "notes": notes,
            "zip": f"{REPO}/archive/refs/tags/v{ver}.zip", "sha256": "FILL: sha256 of that tag archive after pushing the tag"}
open(os.path.join(ROOT, "version.json"), "w", encoding="utf-8").write(json.dumps(manifest, ensure_ascii=False, indent=1) + "\n")
print(json.dumps(manifest, indent=1))
