using System.IO;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;

namespace MovimientoTierrasControl
{
    /// <summary>
    /// Helpers compartidos por las herramientas PSR: búsqueda de
    /// superficies/alineamientos por nombre, prompts al usuario y
    /// preparación de carpetas de salida.
    /// </summary>
    internal static class PSRCivilHelpers
    {
        /// <summary>
        /// Busca una superficie por nombre (case-insensitive).
        /// Devuelve ObjectId.Null si no se encuentra.
        /// </summary>
        public static ObjectId FindSurfaceByName(CivilDocument civDoc, string name)
        {
            if (civDoc == null || string.IsNullOrEmpty(name)) return ObjectId.Null;
            Database db = civDoc.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in civDoc.GetSurfaceIds())
                {
                    var s = tr.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Surface;
                    if (s != null && string.Equals(s.Name, name, System.StringComparison.OrdinalIgnoreCase))
                    {
                        tr.Commit();
                        return id;
                    }
                }
                tr.Commit();
            }
            return ObjectId.Null;
        }

        /// <summary>
        /// Busca un alineamiento por nombre (case-insensitive).
        /// </summary>
        public static ObjectId FindAlignmentByName(CivilDocument civDoc, string name)
        {
            if (civDoc == null || string.IsNullOrEmpty(name)) return ObjectId.Null;
            Database db = civDoc.Database;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in civDoc.GetAlignmentIds())
                {
                    var a = tr.GetObject(id, OpenMode.ForRead) as Alignment;
                    if (a != null && string.Equals(a.Name, name, System.StringComparison.OrdinalIgnoreCase))
                    {
                        tr.Commit();
                        return id;
                    }
                }
                tr.Commit();
            }
            return ObjectId.Null;
        }

        /// <summary>
        /// Intenta primero localizar la superficie por nombre por defecto.
        /// Si no existe, pide al usuario que la seleccione en pantalla.
        /// </summary>
        public static ObjectId PromptSurface(Editor ed, CivilDocument civDoc,
            string prompt, string defaultName)
        {
            ObjectId id = FindSurfaceByName(civDoc, defaultName);
            if (!id.IsNull)
            {
                ed.WriteMessage("\n[PSR] Usando superficie por defecto: " + defaultName);
                return id;
            }

            var opts = new PromptEntityOptions(prompt);
            opts.SetRejectMessage("\nDebe ser una superficie Civil 3D.");
            opts.AddAllowedClass(typeof(TinSurface), true);
            opts.AddAllowedClass(typeof(GridSurface), true);
            opts.AddAllowedClass(typeof(TinVolumeSurface), true);
            opts.AddAllowedClass(typeof(GridVolumeSurface), true);
            var res = ed.GetEntity(opts);
            return res.Status == PromptStatus.OK ? res.ObjectId : ObjectId.Null;
        }

        /// <summary>
        /// Intenta localizar un alineamiento por nombre, si no, lo pide.
        /// </summary>
        public static ObjectId PromptAlignment(Editor ed, CivilDocument civDoc,
            string prompt, string defaultName)
        {
            ObjectId id = FindAlignmentByName(civDoc, defaultName);
            if (!id.IsNull)
            {
                ed.WriteMessage("\n[PSR] Usando alineamiento por defecto: " + defaultName);
                return id;
            }

            var opts = new PromptEntityOptions(prompt);
            opts.SetRejectMessage("\nDebe ser un alineamiento Civil 3D.");
            opts.AddAllowedClass(typeof(Alignment), true);
            var res = ed.GetEntity(opts);
            return res.Status == PromptStatus.OK ? res.ObjectId : ObjectId.Null;
        }

        public static void EnsureOutputDir()
        {
            if (!Directory.Exists(PSRConfig.CarpetaSalida))
                Directory.CreateDirectory(PSRConfig.CarpetaSalida);
        }
    }
}
