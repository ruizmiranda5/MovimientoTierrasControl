using System;
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
    /// Herramientas de alineamientos y perfiles para PSR.
    /// </summary>
    public class PSRAlignmentTools
    {
        /// <summary>
        /// Lista todos los alineamientos del dibujo con longitud,
        /// estación inicial/final y número de perfiles.
        /// </summary>
        [CommandMethod("PSR_LISTAR_ALINEAMIENTOS")]
        public void ListarAlineamientos()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            Database db = doc.Database;
            CivilDocument civDoc = CivilApplication.ActiveDocument;

            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    ObjectIdCollection ids = civDoc.GetAlignmentIds();
                    ed.WriteMessage("\n[PSR] Alineamientos encontrados: " + ids.Count);
                    foreach (ObjectId id in ids)
                    {
                        Alignment a = tr.GetObject(id, OpenMode.ForRead) as Alignment;
                        if (a == null) continue;
                        int nProfiles = a.GetProfileIds().Count;
                        int nSLG = a.GetSampleLineGroupIds().Count;
                        ed.WriteMessage(string.Format(
                            "\n  • {0} | L={1:N2} m | PK {2:N2}→{3:N2} | Perfiles:{4} | SLG:{5}",
                            a.Name, a.Length, a.StartingStation, a.EndingStation,
                            nProfiles, nSLG));
                    }
                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[PSR] Error en PSR_LISTAR_ALINEAMIENTOS: " + ex.Message);
            }
        }

        /// <summary>
        /// Exporta elevaciones del perfil de terreno y del perfil de
        /// proyecto a un CSV, muestreando cada PSRConfig.IntervaloPerfilMetros.
        /// </summary>
        [CommandMethod("PSR_EXPORTAR_PERFIL")]
        public void ExportarPerfil()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            Database db = doc.Database;
            CivilDocument civDoc = CivilApplication.ActiveDocument;

            try
            {
                ObjectId alignId = PSRCivilHelpers.PromptAlignment(ed, civDoc,
                    "\nSelecciona alineamiento: ", PSRConfig.AlineamientoPrincipal);
                if (alignId.IsNull) return;

                PSRCivilHelpers.EnsureOutputDir();

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    Alignment a = (Alignment)tr.GetObject(alignId, OpenMode.ForRead);
                    ObjectIdCollection profIds = a.GetProfileIds();
                    if (profIds.Count == 0)
                    {
                        ed.WriteMessage("\n[PSR] El alineamiento no tiene perfiles.");
                        tr.Commit();
                        return;
                    }

                    Profile profTN = null;
                    Profile profPRY = null;
                    foreach (ObjectId pid in profIds)
                    {
                        Profile p = tr.GetObject(pid, OpenMode.ForRead) as Profile;
                        if (p == null) continue;
                        if (p.ProfileType == ProfileType.EG && profTN == null) profTN = p;
                        else if (p.ProfileType == ProfileType.FG && profPRY == null) profPRY = p;
                    }

                    string csvPath = Path.Combine(PSRConfig.CarpetaSalida,
                        string.Format("{0}_{1}_perfil.csv", PSRConfig.ProjectCode, a.Name));
                    var sb = new StringBuilder();
                    sb.AppendLine("estacion,z_tn,z_proy,delta");

                    double step = PSRConfig.IntervaloPerfilMetros;
                    for (double s = a.StartingStation; s <= a.EndingStation; s += step)
                    {
                        double ztn = SafeElev(profTN, s);
                        double zpry = SafeElev(profPRY, s);
                        double delta = (double.IsNaN(ztn) || double.IsNaN(zpry))
                            ? double.NaN : (zpry - ztn);
                        sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                            "{0:F3},{1},{2},{3}\n",
                            s,
                            double.IsNaN(ztn) ? "" : ztn.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                            double.IsNaN(zpry) ? "" : zpry.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                            double.IsNaN(delta) ? "" : delta.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
                    }

                    File.WriteAllText(csvPath, sb.ToString());
                    ed.WriteMessage("\n[PSR] Perfil exportado: " + csvPath);
                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[PSR] Error en PSR_EXPORTAR_PERFIL: " + ex.Message);
            }
        }

        private static double SafeElev(Profile p, double station)
        {
            if (p == null) return double.NaN;
            try { return p.ElevationAt(station); }
            catch { return double.NaN; }
        }
    }
}
