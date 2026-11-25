// Loads a private copy of this add-in plus its Roslyn DLLs into an isolated AssemblyLoadContext,
// so other add-ins that ship a different Microsoft.CodeAnalysis version cannot break script execution.
// Developed by Dr Ibrahim M.A. Alaamri. MIT License.
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Autodesk.Revit.UI;

namespace Alaamri.RevitMcp
{
    internal class IsolatedContext : AssemblyLoadContext
    {
        readonly string _dir;
        public IsolatedContext(string dir) : base("AlaamriRevitMcp.Engine") { _dir = dir; }

        protected override Assembly Load(AssemblyName name)
        {
            // Our own DLL and the Roslyn stack come from our folder; Revit and .NET from the default context.
            // On .NET 8 (Revit 2025/2026) Roslyn also needs newer System.Collections.Immutable and
            // System.Reflection.Metadata than the framework has; they ship next to us only in that case.
            if (name.Name == "AlaamriRevitMcp" || name.Name.StartsWith("Microsoft.CodeAnalysis")
                || name.Name == "System.Collections.Immutable" || name.Name == "System.Reflection.Metadata")
            {
                string p = Path.Combine(_dir, name.Name + ".dll");
                if (File.Exists(p)) return LoadFromAssemblyPath(p);
            }
            return null;
        }
    }

    internal static class Engine
    {
        static Func<UIApplication, string, bool, string, string> _run;

        public static string Run(UIApplication uiapp, Job job)
        {
            if (_run == null)
            {
                // Load the shared Globals assembly in the default context first, so the isolated engine
                // and the compiled scripts both resolve this single copy.
                _ = typeof(Globals).Assembly;
                string dir = Path.GetDirectoryName(typeof(Engine).Assembly.Location);
                var ctx = new IsolatedContext(dir);
                var asm = ctx.LoadFromAssemblyName(new AssemblyName("AlaamriRevitMcp"));
                var mi = asm.GetType("Alaamri.RevitMcp.ScriptRunner").GetMethod("RunRaw", BindingFlags.Public | BindingFlags.Static);
                _run = (Func<UIApplication, string, bool, string, string>)Delegate.CreateDelegate(
                    typeof(Func<UIApplication, string, bool, string, string>), mi);
            }
            return _run(uiapp, job.Code, job.UseTransaction, job.TransactionName);
        }
    }
}
