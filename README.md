# Alaamri Revit MCP

An [MCP](https://modelcontextprotocol.io) server that gives **Claude** (or any MCP client) **full Revit API access** inside a running **Autodesk Revit 2027** session: run any C# against the live model, plus ready tools to read, edit, select, delete, open, save and export.

It has two parts:

1. **Revit add-in** (`addin/`, prebuilt in `dist/`): adds an **Alaamri** ribbon tab with a **Start/Stop Claude** button (green = connected) and **About**. It opens a bridge on `127.0.0.1` only, protected by a per-session token, and runs every request on Revit's API thread through `ExternalEvent`. C# is compiled at runtime with Roslyn, isolated in its own load context so other add-ins' Roslyn versions don't clash.
2. **MCP server** (`server.py`): exposes the bridge as MCP tools over stdio.

> **بالعربي:** [القسم العربي](#بالعربية) · [برومبت التثبيت عبر Claude](prompts/install-with-claude.md) · تثبيت بنقرة واحدة: `install.bat`

## Requirements

- Windows, **Autodesk Revit 2027**
- Python 3.10 or newer

## Install

**Let Claude do it:** paste the prompt in [prompts/install-with-claude.md](prompts/install-with-claude.md) into Claude Code.

**Quick (Windows):** close Revit, then double-click `install.bat` (inside the cloned folder or on its own). It copies the add-in to `%APPDATA%\Autodesk\Revit\Addins\2027`, installs the server into `%LOCALAPPDATA%\Alaamri-Revit-MCP` and registers it in Claude Desktop (keeping a `.bak` of your config). Fully quit and reopen Claude Desktop, open Revit and choose **Always Load** for the add-in.

**Claude Code:**

```powershell
claude mcp add alaamri-revit -- "%LOCALAPPDATA%\Alaamri-Revit-MCP\.venv\Scripts\alaamri-revit-mcp.exe"
```

**Build the add-in from source** (.NET 10 SDK):

```powershell
dotnet build addin -c Release -o dist\AlaamriRevitMcp
```

## Tools

| Tool | What it does |
|---|---|
| `execute_csharp` | **Run any C# inside Revit** with globals `uiapp`, `app`, `uidoc`, `doc`, `print()`, `mm()`, `ft()`, `id()`. Runs in one named transaction (undo with Ctrl+Z), rolled back on error |
| `status` | Bridge, Revit version/build, active document and view, open documents |
| `document_info` | Title, path, units, levels, phases, worksharing, model element counts |
| `list_elements` | Elements by category or API class, optional active-view / name filter |
| `get_parameters` | All instance (and type) parameters of an element |
| `set_parameters` | Set one parameter on many elements or their types |
| `get_selection` / `select_elements` | Read or set the Revit selection (and zoom) |
| `delete_elements` | Delete elements (undoable) |
| `open_document` / `new_document` / `save_document` | File operations |
| `export_view_image` | Export the active view to PNG for visual checking |
| `about` | Version, author, license, links |

Example `execute_csharp` code:

```csharp
var level = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).First();
var w = Wall.Create(doc, Line.CreateBound(XYZ.Zero, new XYZ(ft(6000), 0, 0)), level.Id, false);
return w;   // -> {id, name, category, type, level}
```

## Safety

This tool gives an AI **unrestricted** control of the open Revit model. Work on a copy of real projects, keep backups, and review what the agent is doing. Each call is a single named transaction you can undo with **Ctrl+Z**; calls with `transaction=false` (open/save/close or self-managed transactions) are not wrapped. The bridge listens on `127.0.0.1` only, rejects browser requests, and needs the random token written to `%LOCALAPPDATA%\Alaamri-Revit-MCP\bridge.json` each time it starts. Click **Stop Claude** to switch it off; the choice is remembered.

## Author

Developed by **Dr Ibrahim M.A. Alaamri**: [LinkedIn](https://www.linkedin.com/in/dribrahim-alameri/) · Also: [Alaamri MS Project MCP](https://github.com/engibrrahem-wq/alaamri-msproject-mcp)

Feedback and ratings are welcome in [Issues](https://github.com/engibrrahem-wq/alaamri-revit-mcp/issues/new?title=Feedback&labels=feedback).

## License

MIT, see [LICENSE](LICENSE).

---

<div dir="rtl">

## بالعربية

**Alaamri Revit MCP** خادم MCP يمنح Claude (أو أي عميل MCP) **صلاحية كاملة على Revit API** داخل جلسة Revit 2027 المفتوحة: تنفيذ أي كود C# على النموذج مباشرة، إضافة إلى أدوات جاهزة للقراءة والتعديل والتحديد والحذف والفتح والحفظ والتصدير.

### المكوّنان
1. **إضافة Revit**: تضيف تبويب **Alaamri** فيه زر **Start/Stop Claude** (أخضر عند الاتصال) وزر **About**. الاتصال محلي فقط على `127.0.0.1` ومحمي برمز سري يتجدد في كل تشغيل.
2. **خادم MCP** (`server.py`) يعرض هذه الإمكانات أدواتٍ لـ Claude.

### التثبيت
- **عبر Claude:** الصق البرومبت الموجود في [prompts/install-with-claude.md](prompts/install-with-claude.md) في Claude Code وسيتولى التثبيت والاختبار.
- **بنقرة واحدة:** أغلق Revit ثم شغّل `install.bat`، ثم أعد فتح Claude Desktop، وافتح Revit واختر **Always Load**.

### الأدوات
`execute_csharp` (تنفيذ أي كود C# داخل Revit)، و`status`، و`document_info`، و`list_elements`، و`get_parameters`، و`set_parameters`، و`get_selection`، و`select_elements`، و`delete_elements`، و`open_document`، و`new_document`، و`save_document`، و`export_view_image`، و`about`.

### تنبيه
الأداة تمنح الذكاء الاصطناعي تحكماً **غير مقيّد** بالنموذج المفتوح، فاعمل على نسخة من المشاريع الحقيقية واحتفظ بنسخ احتياطية. كل أمر يُنفَّذ في Transaction واحدة باسم واضح يمكن التراجع عنها بـ **Ctrl+Z**، ويُلغى أثره تلقائياً عند الخطأ. لإيقاف الاتصال اضغط **Stop Claude**.

### المطوّر
تطوير **د. إبراهيم م. أ. العامري** (Dr Ibrahim M.A. Alaamri): [LinkedIn](https://www.linkedin.com/in/dribrahim-alameri/)

يسعدني تقييمك وملاحظاتك عبر [Issues](https://github.com/engibrrahem-wq/alaamri-revit-mcp/issues/new?title=Feedback&labels=feedback).

### الترخيص
MIT، راجع ملف [LICENSE](LICENSE).

</div>
