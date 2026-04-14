using System;
using System.Globalization;
using System.IO;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;

namespace MovimientoTierrasControl
{
    /// <summary>
    /// Herramientas de puntos COGO (topografía) para PSR.
    /// Importa/exporta CSV/TXT, crea puntos a partir de coordenadas y
    /// aplica códigos/descripciones por prefijo estándar PSR.
    /// </summary>
    public class PSRPointTools
    {
        /// <summary>
        /// Importa puntos COGO desde un CSV con formato:
        /// número,norte,este,z,descripción
        /// (separador coma, punto decimal).
        /// </summary>
        [CommandMethod("PSR_IMPORTAR_PUNTOS")]
        public void ImportarPuntosCsv()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            CivilDocument civDoc = CivilApplication.ActiveDocument;

            try
            {
                var opts = new PromptStringOptions("\nRuta del CSV (número,N,E,Z,desc): ");
                opts.AllowSpaces = true;
                var res = ed.GetString(opts);
                if (res.Status != PromptStatus.OK) return;
                string path = res.StringResult.Trim('"');
                if (!File.Exists(path))
                {
                    ed.WriteMessage("\n[PSR] Archivo no encontrado: " + path);
                    return;
                }

                int added = 0, skipped = 0;
                CogoPointCollection cogo = civDoc.CogoPoints;
                foreach (string raw in File.ReadAllLines(path))
                {
                    string line = raw.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;
                    string[] parts = line.Split(',');
                    if (parts.Length < 4) { skipped++; continue; }
                    double n, e, z;
                    if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out n) ||
                        !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out e) ||
                        !double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out z))
                    { skipped++; continue; }
                    string desc = parts.Length >= 5 ? parts[4] : "";
                    Point3d pt = new Point3d(e, n, z); // AutoCAD X=Este, Y=Norte
                    try
                    {
                        ObjectId id = cogo.Add(pt, true);
                        using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                        {
                            CogoPoint cp = (CogoPoint)tr.GetObject(id, OpenMode.ForWrite);
                            cp.RawDescription = desc;
                            int num;
                            if (int.TryParse(parts[0], out num)) cp.PointNumber = (uint)num;
                            tr.Commit();
                        }
                        added++;
                    }
                    catch { skipped++; }
                }
                ed.WriteMessage(string.Format("\n[PSR] Puntos añadidos: {0} | Omitidos: {1}", added, skipped));
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[PSR] Error en PSR_IMPORTAR_PUNTOS: " + ex.Message);
            }
        }

        /// <summary>
        /// Exporta todos los puntos COGO (o los de un grupo si se filtra
        /// por prefijo) a un CSV estándar para topografía de campo.
        /// </summary>
        [CommandMethod("PSR_EXPORTAR_PUNTOS")]
        public void ExportarPuntosCsv()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            Database db = doc.Database;
            CivilDocument civDoc = CivilApplication.ActiveDocument;

            try
            {
                var pref = ed.GetString("\nPrefijo de código (intro = todos): ");
                string prefix = pref.Status == PromptStatus.OK ? pref.StringResult : "";

                PSRCivilHelpers.EnsureOutputDir();
                string csv = Path.Combine(PSRConfig.CarpetaSalida,
                    string.Format("{0}_puntos_{1:yyyyMMdd-HHmm}.csv", PSRConfig.ProjectCode, DateTime.Now));

                var sb = new StringBuilder();
                sb.AppendLine("numero,norte,este,z,descripcion");
                int n = 0;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId id in civDoc.CogoPoints)
                    {
                        CogoPoint cp = tr.GetObject(id, OpenMode.ForRead) as CogoPoint;
                        if (cp == null) continue;
                        if (!string.IsNullOrEmpty(prefix) &&
                            (cp.RawDescription == null ||
                             !cp.RawDescription.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                            continue;
                        sb.AppendFormat(CultureInfo.InvariantCulture,
                            "{0},{1:F4},{2:F4},{3:F4},{4}\n",
                            cp.PointNumber, cp.Northing, cp.Easting, cp.Elevation,
                            cp.RawDescription ?? "");
                        n++;
                    }
                    tr.Commit();
                }
                File.WriteAllText(csv, sb.ToString());
                ed.WriteMessage(string.Format("\n[PSR] {0} puntos exportados: {1}", n, csv));
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[PSR] Error en PSR_EXPORTAR_PUNTOS: " + ex.Message);
            }
        }

        /// <summary>
        /// Crea un grupo de puntos filtrado por prefijo de descripción
        /// (por ejemplo PSR-EJE, PSR-BM, PSR-TN…).
        /// </summary>
        [CommandMethod("PSR_GRUPO_PUNTOS")]
        public void CrearGrupoPuntos()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            Database db = doc.Database;
            CivilDocument civDoc = CivilApplication.ActiveDocument;

            try
            {
                var p1 = ed.GetString("\nNombre del grupo: ");
                if (p1.Status != PromptStatus.OK) return;
                var p2 = ed.GetString("\nPrefijo RawDescription (p.ej. PSR-BM): ");
                if (p2.Status != PromptStatus.OK) return;

                StandardPointGroupQuery q = new StandardPointGroupQuery();
                q.IncludeRawDescriptions = p2.StringResult + "*";

                PointGroupCollection groups = civDoc.PointGroups;
                ObjectId gid = groups.Add(p1.StringResult);
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    PointGroup g = (PointGroup)tr.GetObject(gid, OpenMode.ForWrite);
                    g.SetQuery(q);
                    tr.Commit();
                }
                ed.WriteMessage(string.Format("\n[PSR] Grupo '{0}' creado con query '{1}*'",
                    p1.StringResult, p2.StringResult));
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[PSR] Error en PSR_GRUPO_PUNTOS: " + ex.Message);
            }
        }
    }
}
