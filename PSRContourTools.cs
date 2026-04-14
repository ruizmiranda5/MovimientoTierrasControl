using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using Autodesk.Civil.DatabaseServices.Styles;

namespace MovimientoTierrasControl
{
    /// <summary>
    /// Herramientas de curvas de nivel y visualización de superficies.
    /// </summary>
    public class PSRContourTools
    {
        /// <summary>
        /// Aplica un estilo de curvas de nivel a una superficie y fuerza
        /// un Rebuild para regenerar las líneas.
        /// </summary>
        [CommandMethod("PSR_ACTUALIZAR_SUPERFICIE")]
        public void RebuildSurface()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            Database db = doc.Database;
            CivilDocument civDoc = CivilApplication.ActiveDocument;

            try
            {
                ObjectId sid = PSRCivilHelpers.PromptSurface(ed, civDoc,
                    "\nSelecciona superficie a actualizar: ", PSRConfig.SurfaceTerrenoNatural);
                if (sid.IsNull) return;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    var surf = tr.GetObject(sid, OpenMode.ForWrite) as Autodesk.Civil.DatabaseServices.Surface;
                    if (surf != null)
                    {
                        surf.Rebuild();
                        ed.WriteMessage("\n[PSR] Superficie actualizada: " + surf.Name);
                    }
                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[PSR] Error en PSR_ACTUALIZAR_SUPERFICIE: " + ex.Message);
            }
        }

        /// <summary>
        /// Extrae información estadística de una superficie: pendientes,
        /// elevaciones, número de puntos y triángulos.
        /// </summary>
        [CommandMethod("PSR_ESTADISTICAS_SUPERFICIE")]
        public void EstadisticasSuperficie()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            Database db = doc.Database;
            CivilDocument civDoc = CivilApplication.ActiveDocument;

            try
            {
                ObjectId sid = PSRCivilHelpers.PromptSurface(ed, civDoc,
                    "\nSelecciona superficie: ", PSRConfig.SurfaceTerrenoNatural);
                if (sid.IsNull) return;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    var surf = tr.GetObject(sid, OpenMode.ForRead) as TinSurface;
                    if (surf == null)
                    {
                        ed.WriteMessage("\n[PSR] No es una TIN Surface.");
                        tr.Commit();
                        return;
                    }
                    var gen = surf.GetGeneralProperties();
                    var terr = surf.GetTerrainProperties();
                    ed.WriteMessage(string.Format(
                        "\n[PSR] Superficie: {0}" +
                        "\n  Puntos       : {1}" +
                        "\n  Triángulos   : {2}" +
                        "\n  Zmin / Zmax  : {3:N3} / {4:N3}" +
                        "\n  Área 2D / 3D : {5:N2} / {6:N2} m²" +
                        "\n  Pend. mín/máx/media : {7:N2}% / {8:N2}% / {9:N2}%",
                        surf.Name,
                        gen.NumberOfPoints,
                        gen.NumberOfTriangles,
                        gen.MinimumElevation, gen.MaximumElevation,
                        gen.Area2D, gen.Area3D,
                        terr.MinimumGrade * 100,
                        terr.MaximumGrade * 100,
                        terr.MeanGrade * 100));
                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[PSR] Error en PSR_ESTADISTICAS_SUPERFICIE: " + ex.Message);
            }
        }
    }
}
