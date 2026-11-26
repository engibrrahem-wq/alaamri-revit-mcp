"""Alaamri Revit MCP: lets Claude (or any MCP client) control Autodesk Revit with full API access.

The Revit add-in (folder `addin/`) opens a token-protected bridge on 127.0.0.1 and runs
C# snippets on Revit's API thread. This server exposes that bridge as MCP tools.

Developed by Dr Ibrahim M.A. Alaamri. Licensed under the MIT License.
"""

import json
import os
import urllib.error
import urllib.request

from mcp.server.mcpserver import MCPServer

__version__ = "0.2.0"
AUTHOR = "Dr Ibrahim M.A. Alaamri"
LINKEDIN_URL = "https://www.linkedin.com/in/dribrahim-alameri/"
REPO_URL = "https://github.com/engibrrahem-wq/alaamri-revit-mcp"
INFO_PATH = os.path.join(os.environ.get("LOCALAPPDATA", ""), "Alaamri-Revit-MCP", "bridge.json")

mcp = MCPServer(
    "alaamri-revit",
    title="Alaamri Revit MCP",
    website_url=LINKEDIN_URL,
    version=__version__,
    instructions=(
        "Controls the running Autodesk Revit session with full Revit API access. "
        "Call `status` first. `execute_csharp` runs any C# against the live model: globals are "
        "uiapp, app, uidoc, doc, print(obj), mm(feet), ft(mm), id(long). Revit internal units are FEET. "
        "Each call runs in its own named transaction (undo with Ctrl+Z in Revit), and is rolled back on error. "
        "Pass transaction=false only when your code manages its own Transaction/TransactionGroup or opens/closes documents. "
        "Prefer one larger script over many small calls. Return values are converted to JSON "
        "(Elements become {id,name,category,type,level}). "
        f"Developed by {AUTHOR} - {LINKEDIN_URL}"
    ),
)


class BridgeError(RuntimeError):
    pass


def _info():
    try:
        with open(INFO_PATH, encoding="utf-8") as f:
            return json.load(f)
    except FileNotFoundError:
        raise BridgeError(
            "Revit bridge is not running. Open Revit (with the Alaamri Revit MCP add-in installed) and "
            "click Alaamri tab > Claude MCP > 'Start Claude'."
        )


def _call(path, payload=None, timeout=130):
    info = _info()
    url = f"http://127.0.0.1:{info['port']}{path}"
    data = json.dumps(payload).encode("utf-8") if payload is not None else None
    req = urllib.request.Request(url, data=data, method="POST" if data else "GET")
    req.add_header("X-Alaamri-Token", info["token"])
    if data:
        req.add_header("Content-Type", "application/json")
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return json.loads(r.read().decode("utf-8"))
    except urllib.error.HTTPError as e:
        try:
            return json.loads(e.read().decode("utf-8"))
        except Exception:
            raise BridgeError(f"HTTP {e.code} from Revit bridge")
    except (urllib.error.URLError, ConnectionError, TimeoutError) as e:
        raise BridgeError(
            f"Cannot reach the Revit bridge on port {info['port']} ({e}). Revit may be closed or the bridge "
            "stopped: click 'Start Claude' on the Alaamri tab."
        )


def _exec(code, transaction=True, name=None, timeout_s=120):
    return _call(
        "/exec",
        {"code": code, "transaction": transaction, "transaction_name": name, "timeout_s": timeout_s},
        timeout=timeout_s + 10,
    )


def _out(res):
    return json.dumps(res, ensure_ascii=False, indent=1)


def _cs(s):
    """C# string literal."""
    return json.dumps(s, ensure_ascii=False)


# ---------------------------------------------------------------- tools


@mcp.tool()
def about() -> str:
    """Server name, version, author, license and links."""
    return _out({"name": "Alaamri Revit MCP", "version": __version__, "author": AUTHOR,
                 "license": "MIT", "repo": REPO_URL, "linkedin": LINKEDIN_URL})


@mcp.tool()
def status() -> str:
    """Is Revit running with the bridge on? Revit version, active document, open documents."""
    ping = _call("/ping", timeout=10)
    if not ping.get("ok"):
        return _out(ping)
    res = _exec(
        """
        var docs = app.Documents.Cast<Document>().Select(d => new { d.Title, d.PathName, d.IsFamilyDocument, d.IsWorkshared, modified = d.IsModified }).ToList();
        return new { active = doc?.Title, active_view = uidoc?.ActiveView?.Name, documents = docs };
        """,
        transaction=False, timeout_s=30,
    )
    ping["session"] = res.get("result", res)
    try:
        u = _update_state()
        if u["update_available"]:
            ping["update"] = f"Version {u['latest']} is available ({u['notes']}). Tell the user; install only if they agree."
    except Exception:
        pass
    return _out(ping)


@mcp.tool()
def execute_csharp(code: str, transaction: bool = True, transaction_name: str = "", timeout_s: int = 120) -> str:
    """Run any C# code inside Revit with FULL Revit API access (the main tool).

    Write a C# script body (statements, optional `return value;`). Available globals:
    uiapp (UIApplication), app (Application), uidoc (UIDocument), doc (active Document),
    print(obj) to add output lines, mm(feet)->mm, ft(mm)->feet, id(long)->ElementId.
    Namespaces already imported: System, System.Linq, System.Collections.Generic, System.IO, System.Text,
    Autodesk.Revit.DB (+ .Architecture .Structure .Mechanical .Plumbing .Electrical), Autodesk.Revit.UI(.Selection).
    Revit internal units are feet. With transaction=true (default) the code runs inside one named
    transaction that is committed on success and rolled back on any exception; the user can undo it with Ctrl+Z.
    Use transaction=false for read-only code or when the code opens/closes/saves documents or manages
    its own transactions. Example: `return new FilteredElementCollector(doc).OfClass(typeof(Wall)).Count();`
    """
    return _out(_exec(code, transaction, transaction_name or "execute_csharp", timeout_s))


@mcp.tool()
def document_info() -> str:
    """Active document: title, path, units, levels, phases, worksharing, element counts by category."""
    return _out(_exec(
        """
        if (doc == null) return "No active document";
        var levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>()
            .OrderBy(l => l.Elevation).Select(l => new { id = l.Id.Value, l.Name, elevation_mm = Math.Round(mm(l.Elevation), 1) }).ToList();
        var counts = new FilteredElementCollector(doc).WhereElementIsNotElementType()
            .Where(e => e.Category != null && e.Category.CategoryType == CategoryType.Model)
            .GroupBy(e => e.Category.Name).OrderByDescending(g => g.Count())
            .ToDictionary(g => g.Key, g => g.Count());
        return new {
            title = doc.Title, path = doc.PathName, is_family = doc.IsFamilyDocument, workshared = doc.IsWorkshared,
            length_unit = doc.GetUnits().GetFormatOptions(SpecTypeId.Length).GetUnitTypeId().TypeId,
            project = doc.ProjectInformation?.Name, number = doc.ProjectInformation?.Number,
            levels, phases = doc.Phases.Cast<Phase>().Select(p => p.Name).ToList(), model_counts = counts };
        """, transaction=False))


@mcp.tool()
def list_elements(category: str = "", class_name: str = "", view_only: bool = False,
                  name_contains: str = "", limit: int = 200) -> str:
    """List element instances filtered by category name (e.g. 'Walls', 'Doors') or API class (e.g. 'Wall',
    'FamilyInstance'), optionally only in the active view and/or by name substring."""
    code = f"""
    var col = {('new FilteredElementCollector(doc, doc.ActiveView.Id)' if view_only else 'new FilteredElementCollector(doc)')}.WhereElementIsNotElementType();
    IEnumerable<Element> q = col;
    var cat = {_cs(category)}; var cls = {_cs(class_name)}; var nm = {_cs(name_contains)};
    if (cat != "") q = q.Where(e => e.Category != null && e.Category.Name.Equals(cat, StringComparison.OrdinalIgnoreCase));
    if (cls != "") q = q.Where(e => e.GetType().Name.Equals(cls, StringComparison.OrdinalIgnoreCase));
    if (nm != "") q = q.Where(e => (e.Name ?? "").IndexOf(nm, StringComparison.OrdinalIgnoreCase) >= 0);
    var all = q.ToList();
    return new {{ total = all.Count, items = all.Take({int(limit)}).ToList() }};
    """
    return _out(_exec(code, transaction=False))


@mcp.tool()
def get_parameters(element_id: int, include_type: bool = True) -> str:
    """All instance parameters of one element (and its type parameters if include_type)."""
    code = f"""
    var e = doc.GetElement(id({int(element_id)}));
    if (e == null) throw new Exception("Element not found");
    var inst = e.Parameters.Cast<Parameter>().OrderBy(p => p.Definition?.Name).ToList();
    var t = {str(include_type).lower()} ? doc.GetElement(e.GetTypeId()) : null;
    return new {{ element = e, instance = inst, type = t?.Parameters.Cast<Parameter>().OrderBy(p => p.Definition?.Name).ToList() }};
    """
    return _out(_exec(code, transaction=False))


@mcp.tool()
def set_parameters(element_ids: list[int], parameter: str, value: str, on_type: bool = False) -> str:
    """Set one parameter on many elements (or on their types). Value is text: numbers for Double params
    use display units via SetValueString (e.g. '3000' mm), Integer/Yes-No as '1'/'0', ElementId as the id."""
    ids = ",".join(str(int(i)) for i in element_ids)
    code = f"""
    var done = new List<long>(); var failed = new Dictionary<string,string>();
    foreach (var raw in new long[] {{ {ids} }}) {{
        Element e = doc.GetElement(id(raw));
        if (e != null && {str(on_type).lower()}) e = doc.GetElement(e.GetTypeId());
        var p = e?.LookupParameter({_cs(parameter)});
        if (p == null) {{ failed[raw.ToString()] = "parameter not found"; continue; }}
        if (p.IsReadOnly) {{ failed[raw.ToString()] = "read-only"; continue; }}
        var v = {_cs(value)}; bool ok;
        switch (p.StorageType) {{
            case StorageType.String: ok = p.Set(v); break;
            case StorageType.Integer: ok = p.Set(int.Parse(v)); break;
            case StorageType.ElementId: ok = p.Set(id(long.Parse(v))); break;
            default: ok = p.SetValueString(v); break;
        }}
        if (ok) done.Add(raw); else failed[raw.ToString()] = "Set returned false";
    }}
    return new {{ updated = done.Count, done, failed }};
    """
    return _out(_exec(code, True, f"Set {parameter}"))


@mcp.tool()
def get_selection() -> str:
    """Elements currently selected in Revit."""
    return _out(_exec("return uidoc.Selection.GetElementIds().Select(i => doc.GetElement(i)).ToList();",
                      transaction=False))


@mcp.tool()
def select_elements(element_ids: list[int], zoom: bool = True) -> str:
    """Select elements in Revit (and zoom to them)."""
    ids = ",".join(str(int(i)) for i in element_ids)
    code = f"""
    var ids = new long[] {{ {ids} }}.Select(v => id(v)).Where(i => doc.GetElement(i) != null).ToList();
    uidoc.Selection.SetElementIds(ids);
    if ({str(zoom).lower()} && ids.Count > 0) uidoc.ShowElements(ids);
    return ids.Count;
    """
    return _out(_exec(code, transaction=False))


@mcp.tool()
def delete_elements(element_ids: list[int]) -> str:
    """Delete elements (one undoable transaction). Returns all ids Revit removed, including dependents."""
    ids = ",".join(str(int(i)) for i in element_ids)
    code = f"""
    var del = doc.Delete(new long[] {{ {ids} }}.Select(v => id(v)).ToList());
    return new {{ deleted_count = del.Count, deleted = del.Select(i => i.Value).ToList() }};
    """
    return _out(_exec(code, True, "Delete elements"))


@mcp.tool()
def open_document(path: str, activate: bool = True) -> str:
    """Open an .rvt/.rfa file (activates it in the UI by default)."""
    code = f"""
    var p = {_cs(path)};
    if ({str(activate).lower()}) {{ var u = uiapp.OpenAndActivateDocument(p); return u.Document.Title; }}
    return app.OpenDocumentFile(p).Title;
    """
    return _out(_exec(code, transaction=False, timeout_s=600))


@mcp.tool()
def new_document(template_path: str = "") -> str:
    """Create a new project from a template (default template if empty) and activate it."""
    code = f"""
    var t = {_cs(template_path)};
    if (t == "") t = app.DefaultProjectTemplate;
    var d = app.NewProjectDocument(t);
    var tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Alaamri_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".rvt");
    d.SaveAs(tmp); var path = d.PathName; d.Close(false);
    return uiapp.OpenAndActivateDocument(path).Document.PathName;
    """
    return _out(_exec(code, transaction=False, timeout_s=600))


@mcp.tool()
def save_document(save_as_path: str = "", overwrite: bool = False) -> str:
    """Save the active document, or Save As a new path."""
    code = f"""
    var p = {_cs(save_as_path)};
    if (p == "") {{ doc.Save(); return "saved " + doc.PathName; }}
    doc.SaveAs(p, new SaveAsOptions {{ OverwriteExistingFile = {str(overwrite).lower()} }});
    return "saved as " + doc.PathName;
    """
    return _out(_exec(code, transaction=False, timeout_s=600))


@mcp.tool()
def export_view_image(path: str, pixel_width: int = 1600) -> str:
    """Export the active view as a PNG image to `path`, so the result can be checked visually."""
    code = f"""
    var o = new ImageExportOptions {{
        FilePath = {_cs(os.path.splitext(path)[0])}, ExportRange = ExportRange.CurrentView,
        HLRandWFViewsFileType = ImageFileType.PNG, ShadowViewsFileType = ImageFileType.PNG,
        ZoomType = ZoomFitType.FitToPage, PixelSize = {int(pixel_width)}, ImageResolution = ImageResolution.DPI_150 }};
    doc.ExportImage(o);
    var dir = System.IO.Path.GetDirectoryName(o.FilePath); var stem = System.IO.Path.GetFileName(o.FilePath);
    return System.IO.Directory.GetFiles(dir, stem + "*.png").OrderByDescending(f => System.IO.File.GetLastWriteTime(f)).FirstOrDefault();
    """
    return _out(_exec(code, transaction=False, timeout_s=300))


# ---------------------------------------------------------------- updates
# The author publishes a new version by updating `version.json` on the main branch (see tools/make_release.py).
# Installed copies only READ that file; nothing is installed without the user's own go-ahead.

UPDATE_MANIFEST_URL = "https://raw.githubusercontent.com/engibrrahem-wq/alaamri-revit-mcp/main/version.json"
ALLOWED_DOWNLOAD_PREFIX = "https://github.com/engibrrahem-wq/alaamri-revit-mcp/"
UPDATE_CACHE = os.path.join(os.path.dirname(INFO_PATH), "update-check.json")


def _ver(s):
    return tuple(int(p) for p in str(s).lstrip("v").split(".") if p.isdigit())


def _manifest(force=False):
    import time
    if not force:
        try:
            with open(UPDATE_CACHE, encoding="utf-8") as f:
                c = json.load(f)
            if time.time() - c.get("checked", 0) < 24 * 3600:
                return c["manifest"]
        except Exception:
            pass
    req = urllib.request.Request(UPDATE_MANIFEST_URL, headers={"User-Agent": "alaamri-revit-mcp/" + __version__})
    with urllib.request.urlopen(req, timeout=10) as r:
        m = json.loads(r.read().decode("utf-8"))
    try:
        os.makedirs(os.path.dirname(UPDATE_CACHE), exist_ok=True)
        with open(UPDATE_CACHE, "w", encoding="utf-8") as f:
            json.dump({"checked": time.time(), "manifest": m}, f)
    except OSError:
        pass
    return m


def _update_state(force=False):
    m = _manifest(force)
    newer = _ver(m.get("version", "0")) > _ver(__version__)
    return {"installed": __version__, "latest": m.get("version"), "update_available": newer,
            "notes": m.get("notes", ""), "released": m.get("released", "")}


@mcp.tool()
def check_update() -> str:
    """Check GitHub for a newer Alaamri Revit MCP version. Read-only: downloads nothing.
    If an update is available, show the user the version and notes and ask before calling install_update."""
    try:
        return _out(_update_state(force=True))
    except Exception as e:
        return _out({"installed": __version__, "error": f"Could not reach GitHub: {e}"})


@mcp.tool()
def install_update(user_confirmed: bool = False) -> str:
    """Download the latest release from the official GitHub repo, verify its SHA-256, and open the installer
    in its own window. ONLY call with user_confirmed=true after the user explicitly said yes in chat.
    The installer waits until Revit and Claude Desktop are closed, then updates both the add-in and this server."""
    if not user_confirmed:
        return _out({"ok": False, "error": "Ask the user first, then call again with user_confirmed=true."})
    import hashlib, subprocess, tempfile, zipfile
    st = _update_state(force=True)
    if not st["update_available"]:
        return _out({"ok": True, "message": "Already up to date.", **st})
    m = _manifest()
    url, sha = m.get("zip", ""), m.get("sha256", "").lower()
    if not url.startswith(ALLOWED_DOWNLOAD_PREFIX) or len(sha) != 64:
        return _out({"ok": False, "error": "Update manifest is invalid; not installing."})
    tmp = tempfile.mkdtemp(prefix="alaamri-revit-mcp-update-")
    zpath = os.path.join(tmp, "release.zip")
    try:
        req = urllib.request.Request(url, headers={"User-Agent": "alaamri-revit-mcp/" + __version__})
        with urllib.request.urlopen(req, timeout=120) as r, open(zpath, "wb") as f:
            f.write(r.read())
    except Exception:
        # Python's certificate store can differ from Windows'; fall back to Windows' own downloader
        cmd = f"Invoke-WebRequest -UseBasicParsing -Uri '{url}' -OutFile '{zpath}'"
        if subprocess.run(["powershell", "-NoProfile", "-Command", cmd], capture_output=True).returncode or not os.path.exists(zpath):
            return _out({"ok": False, "error": "Download failed (check the internet connection)."})
    with open(zpath, "rb") as f:
        got = hashlib.sha256(f.read()).hexdigest()
    if got != sha:
        return _out({"ok": False, "error": "Checksum mismatch: download rejected.", "expected": sha, "got": got})
    with zipfile.ZipFile(zpath) as z:
        z.extractall(tmp)
    root = next((os.path.join(tmp, d) for d in os.listdir(tmp) if os.path.isfile(os.path.join(tmp, d, "install.bat"))), None)
    if not root:
        return _out({"ok": False, "error": "install.bat not found in the release."})
    env = dict(os.environ, AMCP_WAIT="1")
    subprocess.Popen(["cmd", "/c", "start", "Alaamri Revit MCP update", os.path.join(root, "install.bat")],
                     cwd=root, env=env, creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
    return _out({"ok": True, "updating_to": st["latest"], "notes": st["notes"],
                 "next": "An installer window opened. Save your work, close Revit and fully quit Claude Desktop; "
                         "the installer continues by itself, then reopen both."})


def main():
    mcp.run()


if __name__ == "__main__":
    main()
