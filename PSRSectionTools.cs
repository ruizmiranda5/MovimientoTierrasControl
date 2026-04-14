using System;
using System.Collections.Generic;
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
    /// Herramientas de sample lines, secciones transversales y
    /// cubicación por estación para PSR.
    /// </summary>
    public class PSRSectionTools
    {
        /// <summary>
        /// Lista todos los sample line groups de un alineamiento y
        /// sus sample lines (nombre, estación, ancho izquierda/derecha).
        /// </summary>
        [CommandMethod("PSR_LISTAR_SAMPLELINES")]
        public void ListarSampleLines()
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

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    Alignment a = (Alignment)tr.GetObject(alignId, OpenMode.ForRead);
                    ObjectIdCollection slgIds = a.GetSampleLineGroupIds();
                    ed.WriteMessage(string.Format("\n[PSR] Alineamiento {0} tiene {1} grupos de sample lines.",
                        a.Name, slgIds.Count));

                    foreach (ObjectId slgId in slgIds)
                    {
                        SampleLineGroup slg = tr.GetObject(slgId, OpenMode.ForRead) as SampleLineGroup;
                        if (slg == null) continue;
                        ObjectIdCollection slIds = slg.GetSampleLineIds();
                        ed.WriteMessage(string.Format("\n  Grupo: {0} ({1} sample lines)", slg.Name, slIds.Count));
                        foreach (ObjectId slId in slIds)
                        {
                            SampleLine sl = tr.GetObject(slId, OpenMode.ForRead) as SampleLine;
                            if (sl == null) continue;
                            ed.WriteMessage(string.Format(
                                "\n    • {0} | PK {1:N2} | Izq:{2:N2} Der:{3:N2}",
                                sl.Name, sl.Station, sl.LeftSwathWidth, sl.RightSwathWidth));
                        }
                    }
                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[PSR] Error en PSR_LISTAR_SAMPLELINES: " + ex.Message);
            }
        }

        /// <summary>
        /// Crea sample lines cada PSRConfig.IntervaloSeccionesMetros sobre
        /// el alineamiento seleccionado, con anchos definidos en PSRConfig.
        /// </summary>
        [CommandMethod("PSR_CREAR_SAMPLELINES")]
        public void CrearSampleLinesIntervalo()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            Database db = doc.Database;
            CivilDocument civDoc = CivilApplication.ActiveDocument;

            try
            {
                ObjectId alignId = PSRCivilHelpers.PromptAlignment(ed, civDoc,
                    "\nSelecciona alineamiento para generar sample lines: ",
                    PSRConfig.AlineamientoPrincipal);
                if (alignId.IsNull) return;

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    Alignment a = (Alignment)tr.GetObject(alignId, OpenMode.ForWrite);
                    string grpName = string.Format("{0}-SLG-{1:yyyyMMdd-HHmm}",
                        PSRConfig.ProjectCode, DateTime.Now);

                    ObjectId slgId = SampleLineGroup.Create(grpName, alignId);
                    SampleLineGroup slg = (SampleLineGroup)tr.GetObject(slgId, OpenMode.ForWrite);

                    double step = PSRConfig.IntervaloSeccionesMetros;
                    int n = 0;
                    for (double s = a.StartingStation; s <= a.EndingStation + 1e-6; s += step)
                    {
                        double station = Math.Min(s, a.EndingStation);
                        string slName = string.Format("SL-{0:F2}", station);
                        try
                        {
                            slg.SampleLines.AddSampleLine(slName, station);
                            n++;
                        }
                        catch { /* sample line ya existente o fuera de rango */ }
                    }
                    ed.WriteMessage(string.Format("\n[PSR] {0} sample lines creadas en grupo {1}", n, grpName));
                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[PSR] Error en PSR_CREAR_SAMPLELINES: " + ex.Message);
            }
        }

        /// <summary>
        /// Calcula áreas de corte/relleno por sección transversal entre
        /// la superficie TN y la de proyecto, acumulando volúmenes por el
        /// método de las áreas medias, y exporta CSV listo para Bruckner.
        /// </summary>
        [CommandMethod("PSR_CUBICACION_SECCIONES")]
        public void CubicacionPorSecciones()
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

                var filas = new List<SectionRow>();

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    Alignment a = (Alignment)tr.GetObject(alignId, OpenMode.ForRead);
                    foreach (ObjectId slgId in a.GetSampleLineGroupIds())
                    {
                        SampleLineGroup slg = tr.GetObject(slgId, OpenMode.ForRead) as SampleLineGroup;
                        if (slg == null) continue;
                        foreach (ObjectId slId in slg.GetSampleLineIds())
                        {
                            SampleLine sl = tr.GetObject(slId, OpenMode.ForRead) as SampleLine;
                            if (sl == null) continue;

                            double aCut = 0, aFill = 0;
                            foreach (ObjectId secId in sl.GetSectionIds())
                            {
                                Section sec = tr.GetObject(secId, OpenMode.ForRead) as Section;
                                if (sec == null) continue;
                                try
                                {
                                    if (sec.SurfaceName != null &&
                                        sec.SurfaceName.IndexOf("VOL", StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        aCut += Math.Max(0, sec.CutArea);
                                        aFill += Math.Max(0, sec.FillArea);
                                    }
                                }
                                catch { }
                            }
                            filas.Add(new SectionRow { Station = sl.Station, CutArea = aCut, FillArea = aFill });
                        }
                    }
                    tr.Commit();
                }

                filas.Sort((x, y) => x.Station.CompareTo(y.Station));

                // Áreas medias para volúmenes entre secciones consecutivas.
                double vCutAcum = 0, vFillAcum = 0;
                var sb = new StringBuilder();
                sb.AppendLine("estacion,area_corte_m2,area_relleno_m2,vol_corte_tramo_m3,vol_relleno_tramo_m3,vol_corte_acum_m3,vol_relleno_acum_m3,masa_acum_m3");
                for (int i = 0; i < filas.Count; i++)
                {
                    double vc = 0, vf = 0;
                    if (i > 0)
                    {
                        double d = filas[i].Station - filas[i - 1].Station;
                        vc = 0.5 * (filas[i].CutArea + filas[i - 1].CutArea) * d;
                        vf = 0.5 * (filas[i].FillArea + filas[i - 1].FillArea) * d;
                        vCutAcum += vc;
                        vFillAcum += vf;
                    }
                    double masa = vCutAcum - vFillAcum;
                    sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                        "{0:F3},{1:F3},{2:F3},{3:F3},{4:F3},{5:F3},{6:F3},{7:F3}\n",
                        filas[i].Station, filas[i].CutArea, filas[i].FillArea,
                        vc, vf, vCutAcum, vFillAcum, masa);
                }

                string csv = Path.Combine(PSRConfig.CarpetaSalida,
                    string.Format("{0}_cubicacion_{1:yyyyMMdd-HHmm}.csv",
                        PSRConfig.ProjectCode, DateTime.Now));
                File.WriteAllText(csv, sb.ToString());
                ed.WriteMessage(string.Format(
                    "\n[PSR] Cubicación exportada: {0}\n  Vol corte total: {1:N2} m³ | Vol relleno total: {2:N2} m³ | Neto: {3:N2} m³",
                    csv, vCutAcum, vFillAcum, vCutAcum - vFillAcum));
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[PSR] Error en PSR_CUBICACION_SECCIONES: " + ex.Message);
            }
        }

        private class SectionRow
        {
            public double Station;
            public double CutArea;
            public double FillArea;
        }
    }
}
