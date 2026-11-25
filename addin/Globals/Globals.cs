// Variables visible to every C# script run by Alaamri Revit MCP.
// Developed by Dr Ibrahim M.A. Alaamri. MIT License.
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Alaamri.RevitMcp
{
    public class Globals
    {
        public UIApplication uiapp;
        public Autodesk.Revit.ApplicationServices.Application app;
        public UIDocument uidoc;
        public Document doc;
        public StringBuilder Output = new StringBuilder();
        /// <summary>Print a line to the output returned to Claude.</summary>
        public void print(object o) => Output.AppendLine(o?.ToString() ?? "null");
        /// <summary>Feet to millimetres (Revit internal units are feet).</summary>
        public double mm(double feet) => feet * 304.8;
        /// <summary>Millimetres to feet.</summary>
        public double ft(double millimetres) => millimetres / 304.8;
        public ElementId id(long v) => new ElementId(v);
    }
}
