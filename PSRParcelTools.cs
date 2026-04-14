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
    /// Herramientas de parcelas (afecciones, expropiaciones, ocupaciones).
    /// </summary>
    public class PSRParcelTools
    {
        /// <summary>
        /// Lista todas las parcelas del dibujo con área, perímetro
        /// y número, exportándolas a CSV.
        /// </summary>
        [CommandMethod("PSR_LISTAR_PARCELAS")]
        public void ListarParcelas()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            Database db = doc.Database;
            CivilDocument civDoc = CivilApplication.ActiveDocument;

            try
            {
                PSRCivilHelpers.EnsureOutputDir();
                string csv = Path.Combine(PSRConfig.CarpetaSalida,
                    string.Format("{0}_parcelas_{1:yyyyMMdd-HHmm}.csv",
                        PSRConfig.ProjectCode, DateTime.Now));
                var sb = new StringBuilder();
                sb.AppendLine("site,numero,nombre,area_m2,perimetro_m");

                int total = 0;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId sid in civDoc.GetSiteIds())
                    {
                        Site site = tr.GetObject(sid, OpenMode.ForRead) as Site;
                        if (site == null) continue;
                        foreach (ObjectId pid in site.GetParcelIds())
                        {
                            Parcel p = tr.GetObject(pid, OpenMode.ForRead) as Parcel;
                            if (p == null) continue;
                            sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                                "{0},{1},{2},{3:F3},{4:F3}\n",
                                site.Name, p.Number, p.Name, p.Area, p.Perimeter2D);
                            total++;
                        }
                    }
                    tr.Commit();
                }
                File.WriteAllText(csv, sb.ToString());
                ed.WriteMessage(string.Format("\n[PSR] {0} parcelas exportadas: {1}", total, csv));
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[PSR] Error en PSR_LISTAR_PARCELAS: " + ex.Message);
            }
        }
    }
}
