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
    /// Herramientas para corredores (obra lineal) en PSR.
    /// </summary>
    public class PSRCorridorTools
    {
        /// <summary>
        /// Lista todos los corredores con su alineamiento base, número
        /// de baselines, assemblies y rango de estaciones.
        /// </summary>
        [CommandMethod("PSR_LISTAR_CORREDORES")]
        public void ListarCorredores()
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
                    var corridors = civDoc.CorridorCollection;
                    ed.WriteMessage("\n[PSR] Corredores: " + corridors.Count);
                    foreach (ObjectId cid in corridors)
                    {
                        Corridor c = tr.GetObject(cid, OpenMode.ForRead) as Corridor;
                        if (c == null) continue;
                        ed.WriteMessage(string.Format(
                            "\n  • {0} | Baselines: {1}", c.Name, c.Baselines.Count));
                        foreach (Baseline bl in c.Baselines)
                        {
                            ed.WriteMessage(string.Format(
                                "\n      – BL {0} | PK {1:N2} → {2:N2} | Regiones: {3}",
                                bl.Name, bl.StartStation, bl.EndStation, bl.BaselineRegions.Count));
                        }
                    }
                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[PSR] Error en PSR_LISTAR_CORREDORES: " + ex.Message);
            }
        }

        /// <summary>
        /// Reconstruye (Rebuild) todos los corredores del proyecto.
        /// </summary>
        [CommandMethod("PSR_REBUILD_CORREDORES")]
        public void RebuildCorredores()
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
                    int n = 0;
                    foreach (ObjectId cid in civDoc.CorridorCollection)
                    {
                        Corridor c = tr.GetObject(cid, OpenMode.ForWrite) as Corridor;
                        if (c == null) continue;
                        c.Rebuild();
                        n++;
                    }
                    ed.WriteMessage(string.Format("\n[PSR] {0} corredores reconstruidos.", n));
                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[PSR] Error en PSR_REBUILD_CORREDORES: " + ex.Message);
            }
        }
    }
}
