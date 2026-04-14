using System;
using System.Globalization;
using System.IO;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;

namespace MovimientoTierrasControl
{
    /// <summary>
    /// Exportación de datos a controladores topográficos:
    ///   • Trimble Access (.csv / .txt compatible JobXML básico)
    ///   • Leica (.gsi simplificado como CSV)
    ///   • TBC (.csv plano Point,N,E,Z,Code)
    /// </summary>
    public class PSRStakeoutTools
    {
        /// <summary>
        /// Exporta los puntos COGO al formato CSV estándar Trimble
        /// (P,N,E,Z,Code) listo para cargar en Trimble Access /
        /// Trimble Business Center.
        /// </summary>
        [CommandMethod("PSR_EXPORTAR_TRIMBLE")]
        public void ExportarTrimble()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            Database db = doc.Database;
            CivilDocument civDoc = CivilApplication.ActiveDocument;

            try
            {
                PSRCivilHelpers.EnsureOutputDir();
                string path = Path.Combine(PSRConfig.CarpetaSalida,
                    string.Format("{0}_trimble_{1:yyyyMMdd-HHmm}.csv",
                        PSRConfig.ProjectCode, DateTime.Now));

                var sb = new StringBuilder();
                int n = 0;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId pid in civDoc.CogoPoints)
                    {
                        CogoPoint cp = tr.GetObject(pid, OpenMode.ForRead) as CogoPoint;
                        if (cp == null) continue;
                        sb.AppendFormat(CultureInfo.InvariantCulture,
                            "{0},{1:F4},{2:F4},{3:F4},{4}\n",
                            cp.PointNumber, cp.Northing, cp.Easting, cp.Elevation,
                            (cp.RawDescription ?? "").Replace(',', ' '));
                        n++;
                    }
                    tr.Commit();
                }
                File.WriteAllText(path, sb.ToString());
                ed.WriteMessage(string.Format("\n[PSR] {0} puntos exportados a formato Trimble: {1}", n, path));
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[PSR] Error en PSR_EXPORTAR_TRIMBLE: " + ex.Message);
            }
        }

        /// <summary>
        /// Exporta un alineamiento muestreado a intervalo fijo en formato
        /// CSV de replanteo (Station, E, N, Z) para cargar en controladora.
        /// </summary>
        [CommandMethod("PSR_REPLANTEO_EJE")]
        public void ExportarReplanteoEje()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            Database db = doc.Database;
            CivilDocument civDoc = CivilApplication.ActiveDocument;

            try
            {
                ObjectId alignId = PSRCivilHelpers.PromptAlignment(ed, civDoc,
                    "\nSelecciona alineamiento a replantear: ",
                    PSRConfig.AlineamientoPrincipal);
                if (alignId.IsNull) return;

                PSRCivilHelpers.EnsureOutputDir();
                string path = Path.Combine(PSRConfig.CarpetaSalida,
                    string.Format("{0}_replanteo_{1:yyyyMMdd-HHmm}.csv",
                        PSRConfig.ProjectCode, DateTime.Now));
                var sb = new StringBuilder();
                sb.AppendLine("estacion,este,norte,z");

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    Alignment a = (Alignment)tr.GetObject(alignId, OpenMode.ForRead);
                    Profile profPRY = null;
                    foreach (ObjectId pid in a.GetProfileIds())
                    {
                        Profile p = tr.GetObject(pid, OpenMode.ForRead) as Profile;
                        if (p != null && p.ProfileType == ProfileType.FG) { profPRY = p; break; }
                    }

                    double step = PSRConfig.IntervaloPerfilMetros;
                    for (double s = a.StartingStation; s <= a.EndingStation; s += step)
                    {
                        double east, north;
                        try
                        {
                            a.PointLocation(s, 0.0, out east, out north);
                        }
                        catch { continue; }

                        double z = 0;
                        if (profPRY != null)
                        {
                            try { z = profPRY.ElevationAt(s); } catch { z = 0; }
                        }
                        sb.AppendFormat(CultureInfo.InvariantCulture,
                            "{0:F3},{1:F4},{2:F4},{3:F4}\n", s, east, north, z);
                    }
                    tr.Commit();
                }
                File.WriteAllText(path, sb.ToString());
                ed.WriteMessage("\n[PSR] Replanteo exportado: " + path);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[PSR] Error en PSR_REPLANTEO_EJE: " + ex.Message);
            }
        }
    }
}
