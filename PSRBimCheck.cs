using System;
using System.Collections.Generic;
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
    /// Control de calidad BIM: auditoría del modelo, detección de
    /// interferencias (clash) entre tuberías y superficies, reporte
    /// de nivel de desarrollo (LOD) por convención de capa y
    /// cubicaciones por material/layer.
    /// </summary>
    public class PSRBimCheck
    {
        /// <summary>
        /// Auditoría rápida del modelo Civil 3D: capas, bloques,
        /// XRefs, superficies, alineamientos y puntos COGO.
        /// Genera un CSV y un resumen en el editor.
        /// </summary>
        [CommandMethod("PSR_BIM_AUDITORIA")]
        public void AuditoriaModelo()
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
                    string.Format("{0}_auditoria_BIM_{1:yyyyMMdd-HHmm}.csv",
                        PSRConfig.ProjectCode, DateTime.Now));

                var sb = new StringBuilder();
                sb.AppendLine("concepto,valor");

                int layers = 0, blocks = 0, xrefs = 0;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
                    foreach (ObjectId id in lt) layers++;
                    BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    foreach (ObjectId id in bt)
                    {
                        BlockTableRecord br = (BlockTableRecord)tr.GetObject(id, OpenMode.ForRead);
                        if (br.IsFromExternalReference) xrefs++;
                        else if (!br.IsLayout && !br.IsAnonymous) blocks++;
                    }
                    tr.Commit();
                }

                int surfaces = civDoc.GetSurfaceIds().Count;
                int aligns = civDoc.GetAlignmentIds().Count;
                int sites = civDoc.GetSiteIds().Count;
                int nets = civDoc.GetPipeNetworkIds().Count;
                int corridors = civDoc.CorridorCollection.Count;

                int cogo = 0;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId pid in civDoc.CogoPoints) cogo++;
                    tr.Commit();
                }

                sb.AppendFormat("capas,{0}\n", layers);
                sb.AppendFormat("bloques,{0}\n", blocks);
                sb.AppendFormat("xrefs,{0}\n", xrefs);
                sb.AppendFormat("superficies,{0}\n", surfaces);
                sb.AppendFormat("alineamientos,{0}\n", aligns);
                sb.AppendFormat("sites,{0}\n", sites);
                sb.AppendFormat("redes_tuberias,{0}\n", nets);
                sb.AppendFormat("corredores,{0}\n", corridors);
                sb.AppendFormat("puntos_cogo,{0}\n", cogo);

                File.WriteAllText(csv, sb.ToString());
                ed.WriteMessage(string.Format(
                    "\n[PSR-BIM] Auditoría: {0} capas | {1} bloques | {2} xrefs | {3} superf | {4} alineam | {5} redes | {6} corredores | {7} puntos",
                    layers, blocks, xrefs, surfaces, aligns, nets, corridors, cogo));
                ed.WriteMessage("\n[PSR-BIM] Reporte: " + csv);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[PSR-BIM] Error en PSR_BIM_AUDITORIA: " + ex.Message);
            }
        }

        /// <summary>
        /// Clash check entre tuberías de todas las redes y una superficie
        /// (típicamente el terreno natural). Verifica recubrimiento mínimo
        /// (cover = Z_terreno − Z_clave_tubo) y lista los tramos sin
        /// cobertura suficiente. Pide recubrimiento mínimo por prompt.
        /// </summary>
        [CommandMethod("PSR_BIM_CLASH_TUBERIAS")]
        public void ClashTuberiasVsSuperficie()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            Database db = doc.Database;
            CivilDocument civDoc = CivilApplication.ActiveDocument;

            try
            {
                ObjectId sId = PSRCivilHelpers.PromptSurface(ed, civDoc,
                    "\nSelecciona superficie de TERRENO: ", PSRConfig.SurfaceTerrenoNatural);
                if (sId.IsNull) return;

                var pCov = ed.GetDouble("\nRecubrimiento mínimo (m) [0.80]: ");
                double coverMin = pCov.Status == PromptStatus.OK ? pCov.Value : 0.80;

                PSRCivilHelpers.EnsureOutputDir();
                string csv = Path.Combine(PSRConfig.CarpetaSalida,
                    string.Format("{0}_BIM_clash_{1:yyyyMMdd-HHmm}.csv",
                        PSRConfig.ProjectCode, DateTime.Now));
                var sb = new StringBuilder();
                sb.AppendLine("red,tubo,z_clave_ini,z_clave_fin,z_terr_ini,z_terr_fin,cover_min,estado");

                int ok = 0, ko = 0;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    var surf = tr.GetObject(sId, OpenMode.ForRead) as Autodesk.Civil.DatabaseServices.Surface;
                    foreach (ObjectId nid in civDoc.GetPipeNetworkIds())
                    {
                        Network net = tr.GetObject(nid, OpenMode.ForRead) as Network;
                        if (net == null) continue;
                        foreach (ObjectId pid in net.GetPipeIds())
                        {
                            Pipe p = tr.GetObject(pid, OpenMode.ForRead) as Pipe;
                            if (p == null) continue;
                            double zIni = p.StartPoint.Z + p.InnerDiameterOrWidth / 2.0;
                            double zFin = p.EndPoint.Z + p.InnerDiameterOrWidth / 2.0;
                            double zTerrIni, zTerrFin;
                            try
                            {
                                zTerrIni = surf.FindElevationAtXY(p.StartPoint.X, p.StartPoint.Y);
                                zTerrFin = surf.FindElevationAtXY(p.EndPoint.X, p.EndPoint.Y);
                            }
                            catch { continue; }
                            double cover = Math.Min(zTerrIni - zIni, zTerrFin - zFin);
                            bool good = cover >= coverMin;
                            sb.AppendFormat(CultureInfo.InvariantCulture,
                                "{0},{1},{2:F3},{3:F3},{4:F3},{5:F3},{6:F3},{7}\n",
                                net.Name, p.Name, zIni, zFin, zTerrIni, zTerrFin, cover,
                                good ? "OK" : "NO CUMPLE");
                            if (good) ok++; else ko++;
                        }
                    }
                    tr.Commit();
                }
                File.WriteAllText(csv, sb.ToString());
                ed.WriteMessage(string.Format(
                    "\n[PSR-BIM] Clash tuberías: {0} OK | {1} NO CUMPLE | cover_min={2:F2} m\n  Reporte: {3}",
                    ok, ko, coverMin, csv));
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[PSR-BIM] Error en PSR_BIM_CLASH_TUBERIAS: " + ex.Message);
            }
        }

        /// <summary>
        /// Inventario de LOD (Level of Development) agrupando bloques
        /// por el sufijo de su capa (LOD100, LOD200, LOD300, LOD350,
        /// LOD400, LOD500). Convención PSR.
        /// </summary>
        [CommandMethod("PSR_BIM_LOD")]
        public void ReporteLod()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            try
            {
                var counts = new Dictionary<string, int>();
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord ms = (BlockTableRecord)tr.GetObject(
                        bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                    foreach (ObjectId id in ms)
                    {
                        Entity e = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (e == null) continue;
                        string lod = ExtractLod(e.Layer);
                        if (lod == null) lod = "SIN_LOD";
                        if (!counts.ContainsKey(lod)) counts[lod] = 0;
                        counts[lod]++;
                    }
                    tr.Commit();
                }

                PSRCivilHelpers.EnsureOutputDir();
                string csv = Path.Combine(PSRConfig.CarpetaSalida,
                    string.Format("{0}_LOD_{1:yyyyMMdd-HHmm}.csv",
                        PSRConfig.ProjectCode, DateTime.Now));
                var sb = new StringBuilder();
                sb.AppendLine("lod,entidades");
                foreach (var kv in counts)
                    sb.AppendFormat("{0},{1}\n", kv.Key, kv.Value);
                File.WriteAllText(csv, sb.ToString());

                ed.WriteMessage("\n[PSR-BIM] Reporte LOD: " + csv);
                foreach (var kv in counts)
                    ed.WriteMessage(string.Format("\n  {0}: {1}", kv.Key, kv.Value));
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[PSR-BIM] Error en PSR_BIM_LOD: " + ex.Message);
            }
        }

        /// <summary>
        /// Quantity Takeoff (QTO) por capa: suma áreas de sólidos 3D,
        /// áreas de regiones y longitudes de polilíneas agrupadas por
        /// capa. Útil para cubicaciones BIM preliminares.
        /// </summary>
        [CommandMethod("PSR_BIM_QTO")]
        public void QuantityTakeoff()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            try
            {
                var vols = new Dictionary<string, double>();
                var areas = new Dictionary<string, double>();
                var lens = new Dictionary<string, double>();

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord ms = (BlockTableRecord)tr.GetObject(
                        bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                    foreach (ObjectId id in ms)
                    {
                        Entity e = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (e == null) continue;
                        string layer = e.Layer;
                        Solid3d sol = e as Solid3d;
                        if (sol != null)
                        {
                            try
                            {
                                double v = sol.MassProperties.Volume;
                                if (!vols.ContainsKey(layer)) vols[layer] = 0;
                                vols[layer] += v;
                            }
                            catch { }
                            continue;
                        }
                        Region reg = e as Region;
                        if (reg != null)
                        {
                            double aVal = reg.Area;
                            if (!areas.ContainsKey(layer)) areas[layer] = 0;
                            areas[layer] += aVal;
                            continue;
                        }
                        Curve cv = e as Curve;
                        if (cv != null)
                        {
                            try
                            {
                                double L = cv.GetDistanceAtParameter(cv.EndParam)
                                         - cv.GetDistanceAtParameter(cv.StartParam);
                                if (!lens.ContainsKey(layer)) lens[layer] = 0;
                                lens[layer] += L;
                            }
                            catch { }
                        }
                    }
                    tr.Commit();
                }

                PSRCivilHelpers.EnsureOutputDir();
                string csv = Path.Combine(PSRConfig.CarpetaSalida,
                    string.Format("{0}_BIM_QTO_{1:yyyyMMdd-HHmm}.csv",
                        PSRConfig.ProjectCode, DateTime.Now));
                var sb = new StringBuilder();
                sb.AppendLine("capa,tipo,cantidad,unidad");
                foreach (var kv in vols)
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "{0},solido,{1:F3},m3\n", kv.Key, kv.Value);
                foreach (var kv in areas)
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "{0},region,{1:F3},m2\n", kv.Key, kv.Value);
                foreach (var kv in lens)
                    sb.AppendFormat(CultureInfo.InvariantCulture,
                        "{0},polilinea,{1:F3},m\n", kv.Key, kv.Value);
                File.WriteAllText(csv, sb.ToString());
                ed.WriteMessage("\n[PSR-BIM] QTO exportado: " + csv);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[PSR-BIM] Error en PSR_BIM_QTO: " + ex.Message);
            }
        }

        private static string ExtractLod(string layer)
        {
            if (string.IsNullOrEmpty(layer)) return null;
            string upper = layer.ToUpperInvariant();
            string[] tags = { "LOD100", "LOD200", "LOD300", "LOD350", "LOD400", "LOD500" };
            foreach (string t in tags) if (upper.Contains(t)) return t;
            return null;
        }
    }
}
