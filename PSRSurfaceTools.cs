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
    /// Herramientas de superficies y cálculo de volúmenes para PSR.
    /// Usa la API nativa de Civil 3D (Autodesk.Civil.DatabaseServices).
    /// </summary>
    public class PSRSurfaceTools
    {
        /// <summary>
        /// Crea una TIN Volume Surface entre la superficie de terreno natural
        /// y la de proyecto, y reporta volúmenes de corte/relleno (ajustados
        /// con factores PSR).
        /// </summary>
        [CommandMethod("PSR_VOLUMEN")]
        public void CalcularVolumenEntreSuperficies()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            Database db = doc.Database;
            CivilDocument civDoc = CivilApplication.ActiveDocument;

            try
            {
                ObjectId baseId = PSRCivilHelpers.PromptSurface(ed, civDoc,
                    "\nSelecciona superficie BASE (terreno natural): ",
                    PSRConfig.SurfaceTerrenoNatural);
                if (baseId.IsNull) return;

                ObjectId compId = PSRCivilHelpers.PromptSurface(ed, civDoc,
                    "\nSelecciona superficie COMPARACIÓN (proyecto): ",
                    PSRConfig.SurfaceProyecto);
                if (compId.IsNull) return;

                string volName = string.Format("{0}-{1:yyyyMMdd-HHmmss}",
                    PSRConfig.SurfaceVolumenPrefix, DateTime.Now);

                ObjectId volId;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    volId = TinVolumeSurface.Create(volName, baseId, compId);
                    tr.Commit();
                }

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    TinVolumeSurface vol = tr.GetObject(volId, OpenMode.ForRead) as TinVolumeSurface;
                    if (vol == null)
                    {
                        ed.WriteMessage("\n[PSR] No se pudo crear la superficie de volumen.");
                        return;
                    }

                    var props = vol.GetVolumeProperties();
                    double corte = props.UnadjustedCutVolume;
                    double relleno = props.UnadjustedFillVolume;
                    double neto = props.UnadjustedNetVolume;

                    double corteAjustado = corte * PSRConfig.FactorCompactacionCorte;
                    double rellenoAjustado = relleno * PSRConfig.FactorExpansionRelleno;
                    double netoAjustado = corteAjustado - rellenoAjustado;

                    var sb = new StringBuilder();
                    sb.AppendLine();
                    sb.AppendLine("======== PSR - VOLÚMENES ========");
                    sb.AppendFormat("Superficie volumen : {0}\n", volName);
                    sb.AppendFormat("Corte (sin ajustar): {0:N2} m³\n", corte);
                    sb.AppendFormat("Relleno (sin aj.)  : {0:N2} m³\n", relleno);
                    sb.AppendFormat("Neto (sin ajustar) : {0:N2} m³\n", neto);
                    sb.AppendLine("-- Ajustado PSR --");
                    sb.AppendFormat("Corte x {0:N2}      : {1:N2} m³\n",
                        PSRConfig.FactorCompactacionCorte, corteAjustado);
                    sb.AppendFormat("Relleno x {0:N2}    : {1:N2} m³\n",
                        PSRConfig.FactorExpansionRelleno, rellenoAjustado);
                    sb.AppendFormat("Neto ajustado      : {0:N2} m³\n", netoAjustado);
                    sb.AppendLine("=================================");
                    ed.WriteMessage(sb.ToString());

                    PSRCivilHelpers.EnsureOutputDir();
                    string csv = Path.Combine(PSRConfig.CarpetaSalida,
                        string.Format("{0}_volumen.csv", volName));
                    File.WriteAllText(csv,
                        "concepto,m3\n" +
                        string.Format("corte,{0:F2}\nrelleno,{1:F2}\nneto,{2:F2}\n" +
                                      "corte_ajustado,{3:F2}\nrelleno_ajustado,{4:F2}\nneto_ajustado,{5:F2}\n",
                            corte, relleno, neto, corteAjustado, rellenoAjustado, netoAjustado));
                    ed.WriteMessage("\n[PSR] CSV exportado: " + csv);

                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[PSR] Error en PSR_VOLUMEN: " + ex.Message);
            }
        }

        /// <summary>
        /// Lista todas las superficies del dibujo con sus propiedades
        /// geométricas básicas (área 2D/3D, mín/máx elevación).
        /// </summary>
        [CommandMethod("PSR_LISTAR_SUPERFICIES")]
        public void ListarSuperficies()
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
                    ObjectIdCollection ids = civDoc.GetSurfaceIds();
                    ed.WriteMessage("\n[PSR] Superficies encontradas: " + ids.Count);
                    foreach (ObjectId id in ids)
                    {
                        var surf = tr.GetObject(id, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Surface;
                        if (surf == null) continue;
                        var gen = surf.GetGeneralProperties();
                        ed.WriteMessage(string.Format(
                            "\n  • {0} | 2D:{1:N2} m² | 3D:{2:N2} m² | Zmin:{3:N2} | Zmax:{4:N2}",
                            surf.Name,
                            gen.Area2D,
                            gen.Area3D,
                            gen.MinimumElevation,
                            gen.MaximumElevation));
                    }
                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[PSR] Error en PSR_LISTAR_SUPERFICIES: " + ex.Message);
            }
        }
    }
}
