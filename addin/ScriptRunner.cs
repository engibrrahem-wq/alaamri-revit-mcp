// Compiles and runs C# snippets inside Revit with full API access (Roslyn scripting).
// Developed by Dr Ibrahim M.A. Alaamri. MIT License.
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace Alaamri.RevitMcp
{
    internal static class ScriptRunner
    {
        static ScriptOptions _options;
        static readonly ConcurrentDictionary<string, Script<object>> _cache = new ConcurrentDictionary<string, Script<object>>();

        static ScriptOptions Options()
        {
            if (_options != null) return _options;
            var refs = new List<Assembly>
            {
                typeof(object).Assembly, typeof(Enumerable).Assembly, typeof(List<>).Assembly,
                typeof(Document).Assembly, typeof(UIApplication).Assembly, typeof(Globals).Assembly,
                typeof(JsonSerializer).Assembly, typeof(System.IO.File).Assembly,
                typeof(System.Text.RegularExpressions.Regex).Assembly,
                Assembly.Load("System.Runtime"), Assembly.Load("System.Collections"), Assembly.Load("netstandard"),
            };
            _options = ScriptOptions.Default
                .WithReferences(refs.Distinct())
                .WithImports("System", "System.Linq", "System.Collections.Generic", "System.IO", "System.Text",
                    "Autodesk.Revit.DB", "Autodesk.Revit.DB.Architecture", "Autodesk.Revit.DB.Structure",
                    "Autodesk.Revit.DB.Mechanical", "Autodesk.Revit.DB.Plumbing", "Autodesk.Revit.DB.Electrical",
                    "Autodesk.Revit.UI", "Autodesk.Revit.UI.Selection")
                .WithEmitDebugInformation(false)
                .WithOptimizationLevel(Microsoft.CodeAnalysis.OptimizationLevel.Release);
            return _options;
        }

        /// <summary>Entry point called (through reflection) inside the isolated load context.</summary>
        public static string RunRaw(UIApplication uiapp, string code, bool useTransaction, string transactionName)
        {
            try { return Run(uiapp, new Job { Code = code, UseTransaction = useTransaction, TransactionName = transactionName }); }
            catch (Exception ex) { return Bridge.Err(ex.ToString()); }
        }

        public static string Run(UIApplication uiapp, Job job)
        {
            var sw = Stopwatch.StartNew();
            var g = new Globals { uiapp = uiapp, app = uiapp.Application, uidoc = uiapp.ActiveUIDocument };
            g.doc = g.uidoc?.Document;

            var script = _cache.GetOrAdd(job.Code, c => CSharpScript.Create<object>(c, Options(), typeof(Globals)));
            var diags = script.Compile();
            var errors = diags.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ToList();
            if (errors.Count > 0)
            {
                _cache.TryRemove(job.Code, out _);
                return JsonSerializer.Serialize(new { ok = false, error = "compile error", diagnostics = errors.Select(e => e.ToString()).ToArray() });
            }

            Transaction tx = null;
            bool useTx = job.UseTransaction && g.doc != null && !g.doc.IsReadOnly && !g.doc.IsModifiable;
            if (useTx)
            {
                tx = new Transaction(g.doc, "Claude: " + (string.IsNullOrWhiteSpace(job.TransactionName) ? "MCP command" : job.TransactionName));
                tx.Start();
            }
            try
            {
                var state = script.RunAsync(g).GetAwaiter().GetResult();
                object value = state.ReturnValue;
                if (tx != null)
                {
                    var st = tx.Commit();
                    if (st != TransactionStatus.Committed) throw new Exception("Transaction status: " + st);
                }
                Settings.Log($"exec ok ({sw.ElapsedMilliseconds} ms) tx={useTx}");
                return JsonSerializer.Serialize(new
                {
                    ok = true,
                    result = Ser.ToPlain(value, 0),
                    output = g.Output.ToString(),
                    transaction = tx != null ? tx.GetName() : null,
                    ms = sw.ElapsedMilliseconds,
                }, new JsonSerializerOptions { WriteIndented = false });
            }
            catch (Exception ex)
            {
                if (tx != null && tx.HasStarted() && !tx.HasEnded()) tx.RollBack();
                var inner = ex is AggregateException ae ? ae.InnerException ?? ex : ex;
                Settings.Log("exec error: " + inner.Message);
                return JsonSerializer.Serialize(new
                {
                    ok = false,
                    error = inner.GetType().Name + ": " + inner.Message,
                    output = g.Output.ToString(),
                    rolled_back = tx != null,
                    stack = inner.StackTrace,
                });
            }
            finally { tx?.Dispose(); }
        }
    }

    /// <summary>Turns Revit objects into JSON-friendly values.</summary>
    internal static class Ser
    {
        const int MaxItems = 2000;

        public static object ToPlain(object v, int depth)
        {
            if (v == null) return null;
            switch (v)
            {
                case string s: return s;
                case bool or int or long or short or byte or float or decimal: return v;
                case double d: return double.IsFinite(d) ? Math.Round(d, 9) : d.ToString();
                case Enum e: return e.ToString();
                case ElementId eid: return eid.Value;
                case XYZ p: return new[] { p.X, p.Y, p.Z };
                case UV uv: return new[] { uv.U, uv.V };
                case Element el: return ElementInfo(el);
                case Parameter prm: return ParamInfo(prm);
                case Guid g: return g.ToString();
                case DateTime dt: return dt.ToString("s");
            }
            if (depth > 4) return v.ToString();
            if (v is IDictionary dict)
            {
                var o = new Dictionary<string, object>();
                foreach (DictionaryEntry kv in dict) o[kv.Key?.ToString() ?? "null"] = ToPlain(kv.Value, depth + 1);
                return o;
            }
            if (v is IEnumerable en)
            {
                var list = new List<object>();
                foreach (var item in en)
                {
                    if (list.Count >= MaxItems) { list.Add($"... truncated at {MaxItems} items"); break; }
                    list.Add(ToPlain(item, depth + 1));
                }
                return list;
            }
            var t = v.GetType();
            if (t.Name.Contains("AnonymousType") || (t.IsValueType && t.Namespace?.StartsWith("System") == true && t.IsGenericType))
            {
                var o = new Dictionary<string, object>();
                foreach (var p in t.GetProperties()) o[p.Name] = ToPlain(p.GetValue(v), depth + 1);
                foreach (var f in t.GetFields()) o[f.Name] = ToPlain(f.GetValue(v), depth + 1);
                return o;
            }
            return v.ToString();
        }

        static object ElementInfo(Element el)
        {
            string type = null;
            try { var tid = el.GetTypeId(); if (tid != ElementId.InvalidElementId) type = el.Document.GetElement(tid)?.Name; } catch { }
            string level = null;
            try { if (el.LevelId != ElementId.InvalidElementId) level = el.Document.GetElement(el.LevelId)?.Name; } catch { }
            return new Dictionary<string, object>
            {
                ["id"] = el.Id.Value, ["name"] = SafeName(el), ["category"] = el.Category?.Name,
                ["class"] = el.GetType().Name, ["type"] = type, ["level"] = level, ["unique_id"] = el.UniqueId,
            };
        }

        static string SafeName(Element el) { try { return el.Name; } catch { return null; } }

        public static object ParamInfo(Parameter p)
        {
            object val = null;
            try
            {
                switch (p.StorageType)
                {
                    case StorageType.String: val = p.AsString(); break;
                    case StorageType.Integer: val = p.AsInteger(); break;
                    case StorageType.Double: val = p.AsDouble(); break;
                    case StorageType.ElementId: val = p.AsElementId().Value; break;
                }
            }
            catch { }
            return new Dictionary<string, object>
            {
                ["name"] = p.Definition?.Name, ["value"] = ToPlain(val, 5), ["display"] = p.AsValueString(),
                ["storage"] = p.StorageType.ToString(), ["read_only"] = p.IsReadOnly, ["shared"] = p.IsShared,
            };
        }
    }
}
