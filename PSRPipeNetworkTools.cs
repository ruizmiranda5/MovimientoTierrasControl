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
    /// Herramientas para redes de tuberías (drenaje, saneamiento)
    /// del proyecto PSR.
    /// </summary>
    public class PSRPipeNetworkTools
    {
        /// <summary>
        /// Lista todas las redes, pozos y tubos del dibujo con su
        /// longitud, diámetro y cotas.
        /// </summary>
        [CommandMethod("PSR_LISTAR_REDES")]
        public void ListarRedes()
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
                    string.Format("{0}_redes_{1:yyyyMMdd-HHmm}.csv",
                        PSRConfig.ProjectCode, DateTime.Now));
                var sb = new StringBuilder();
                sb.AppendLine("red,tipo,nombre,longitud,diametro,cota_ini,cota_fin,profundidad");

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId nid in civDoc.GetPipeNetworkIds())
                    {
                        Network net = tr.GetObject(nid, OpenMode.ForRead) as Network;
                        if (net == null) continue;
                        ed.WriteMessage("\n[PSR] Red: " + net.Name);

                        foreach (ObjectId pid in net.GetPipeIds())
                        {
                            Pipe p = tr.GetObject(pid, OpenMode.ForRead) as Pipe;
                            if (p == null) continue;
                            sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                                "{0},pipe,{1},{2:F3},{3:F3},{4:F3},{5:F3},\n",
                                net.Name, p.Name, p.Length3DCenterToCenter,
                                p.InnerDiameterOrWidth,
                                p.StartPoint.Z, p.EndPoint.Z);
                        }
                        foreach (ObjectId sid in net.GetStructureIds())
                        {
                            Structure s = tr.GetObject(sid, OpenMode.ForRead) as Structure;
                            if (s == null) continue;
                            sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                                "{0},struct,{1},,{2:F3},{3:F3},{4:F3},{5:F3}\n",
                                net.Name, s.Name, 0.0,
                                s.RimElevation, s.SumpElevation,
                                s.RimElevation - s.SumpElevation);
                        }
                    }
                    tr.Commit();
                }
                File.WriteAllText(csv, sb.ToString());
                ed.WriteMessage("\n[PSR] Redes exportadas: " + csv);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[PSR] Error en PSR_LISTAR_REDES: " + ex.Message);
            }
        }
    }
}
