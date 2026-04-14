using System;
using System.Collections.Generic;
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
    /// Herramientas de control de calidad (QA/QC) topográfico.
    /// Compara puntos levantados en campo con una superficie de
    /// proyecto y reporta desviaciones.
    /// </summary>
    public class PSRQAQCTools
    {
        /// <summary>
        /// Compara las elevaciones de los puntos COGO (o de un CSV) con
        /// la elevación de la superficie de proyecto en su misma X,Y
        /// y genera un reporte CSV con desviaciones y estadísticas.
        /// </summary>
        [CommandMethod("PSR_QAQC_PUNTOS")]
        public void CompararPuntosVsSuperficie()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            Database db = doc.Database;
            CivilDocument civDoc = CivilApplication.ActiveDocument;

            try
            {
                ObjectId sid = PSRCivilHelpers.PromptSurface(ed, civDoc,
                    "\nSelecciona superficie de PROYECTO de referencia: ",
                    PSRConfig.SurfaceProyecto);
                if (sid.IsNull) return;

                var pref = ed.GetString("\nPrefijo de RawDescription para filtrar (intro = todos): ");
                string prefix = pref.Status == PromptStatus.OK ? pref.StringResult : "";

                var deltas = new List<double>();
                PSRCivilHelpers.EnsureOutputDir();
                string csv = Path.Combine(PSRConfig.CarpetaSalida,
                    string.Format("{0}_qaqc_{1:yyyyMMdd-HHmm}.csv",
                        PSRConfig.ProjectCode, DateTime.Now));

                var sb = new StringBuilder();
                sb.AppendLine("numero,este,norte,z_campo,z_proy,delta_m");

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    var surf = tr.GetObject(sid, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Surface;
                    foreach (ObjectId pid in civDoc.CogoPoints)
                    {
                        CogoPoint cp = tr.GetObject(pid, OpenMode.ForRead) as CogoPoint;
                        if (cp == null) continue;
                        if (!string.IsNullOrEmpty(prefix) &&
                            (cp.RawDescription == null ||
                             !cp.RawDescription.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                            continue;

                        double zProy;
                        try { zProy = surf.FindElevationAtXY(cp.Easting, cp.Northing); }
                        catch { continue; }
                        double delta = cp.Elevation - zProy;
                        deltas.Add(delta);
                        sb.AppendFormat(CultureInfo.InvariantCulture,
                            "{0},{1:F4},{2:F4},{3:F4},{4:F4},{5:F4}\n",
                            cp.PointNumber, cp.Easting, cp.Northing,
                            cp.Elevation, zProy, delta);
                    }
                    tr.Commit();
                }

                double min = double.MaxValue, max = double.MinValue, sum = 0, sumAbs = 0;
                foreach (double d in deltas)
                {
                    if (d < min) min = d;
                    if (d > max) max = d;
                    sum += d;
                    sumAbs += Math.Abs(d);
                }
                double mean = deltas.Count > 0 ? sum / deltas.Count : 0;
                double meanAbs = deltas.Count > 0 ? sumAbs / deltas.Count : 0;
                double rms = 0;
                foreach (double d in deltas) rms += (d - mean) * (d - mean);
                rms = deltas.Count > 0 ? Math.Sqrt(rms / deltas.Count) : 0;

                sb.AppendLine();
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "# muestras,{0}\n# delta_min,{1:F4}\n# delta_max,{2:F4}\n# media,{3:F4}\n# media_abs,{4:F4}\n# rms,{5:F4}\n",
                    deltas.Count, min, max, mean, meanAbs, rms);

                File.WriteAllText(csv, sb.ToString());
                ed.WriteMessage(string.Format(
                    "\n[PSR] QAQC: {0} puntos | δmin={1:F3} δmax={2:F3} media={3:F3} rms={4:F3} m\n  Reporte: {5}",
                    deltas.Count, min, max, mean, rms, csv));
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[PSR] Error en PSR_QAQC_PUNTOS: " + ex.Message);
            }
        }
    }
}
