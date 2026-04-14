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
    /// Verificación automática de un alineamiento y su perfil de proyecto
    /// contra la norma peruana DG-2018. Produce un CSV con el estado
    /// de cada curva horizontal, cada tramo de pendiente y cada curva
    /// vertical (OK / NO CUMPLE) y un log resumen en el editor.
    /// </summary>
    public class PSRDG2018Check
    {
        /// <summary>
        /// Pide velocidad de diseño, clase y terreno por prompt, y
        /// verifica el alineamiento seleccionado.
        /// </summary>
        [CommandMethod("PSR_CHECK_DG2018")]
        public void CheckDg2018()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            Database db = doc.Database;
            CivilDocument civDoc = CivilApplication.ActiveDocument;

            try
            {
                var vOpt = new PromptIntegerOptions("\nVelocidad de diseño (km/h) [30-130]: ");
                vOpt.DefaultValue = 60;
                vOpt.AllowNegative = false;
                vOpt.AllowZero = false;
                vOpt.LowerLimit = 30;
                vOpt.UpperLimit = 130;
                var vRes = ed.GetInteger(vOpt);
                if (vRes.Status != PromptStatus.OK) return;
                int V = vRes.Value;

                var cOpt = new PromptKeywordOptions("\nClase de carretera");
                cOpt.Keywords.Add("Autopista1");
                cOpt.Keywords.Add("Autopista2");
                cOpt.Keywords.Add("Carretera1");
                cOpt.Keywords.Add("Carretera2");
                cOpt.Keywords.Add("Carretera3");
                cOpt.Keywords.Default = "Carretera2";
                var cRes = ed.GetKeywords(cOpt);
                if (cRes.Status != PromptStatus.OK) return;
                PSRDG2018.ClaseCarretera clase = ParseClase(cRes.StringResult);

                var tOpt = new PromptKeywordOptions("\nTipo de terreno");
                tOpt.Keywords.Add("Plano");
                tOpt.Keywords.Add("Ondulado");
                tOpt.Keywords.Add("Accidentado");
                tOpt.Keywords.Add("Escarpado");
                tOpt.Keywords.Default = "Ondulado";
                var tRes = ed.GetKeywords(tOpt);
                if (tRes.Status != PromptStatus.OK) return;
                PSRDG2018.Terreno terr = (PSRDG2018.Terreno)
                    Enum.Parse(typeof(PSRDG2018.Terreno), tRes.StringResult, true);

                ObjectId alignId = PSRCivilHelpers.PromptAlignment(ed, civDoc,
                    "\nSelecciona alineamiento a verificar: ",
                    PSRConfig.AlineamientoPrincipal);
                if (alignId.IsNull) return;

                double pendMax = PSRDG2018.PendienteMaximaPct(clase, terr);
                double rMin = PSRDG2018.RadioMinimoNormativo(V);
                double peralteMax = PSRDG2018.PeralteMaximoPct[ClampSpeed(V)];
                double Dp = PSRDG2018.DistanciaParada(V, 0);
                double kConvexMin = PSRDG2018.KMinConvexa(V);
                double kConcaveMin = PSRDG2018.KMinConcava(V);

                int vMin = PSRDG2018.VelocidadDisenoMinima(clase, terr);
                if (V < vMin)
                    ed.WriteMessage(string.Format(
                        "\n[DG-2018] AVISO: V={0} km/h < V mín recomendada ({1}) para esta clase/terreno.",
                        V, vMin));

                PSRCivilHelpers.EnsureOutputDir();
                string csv = Path.Combine(PSRConfig.CarpetaSalida,
                    string.Format("{0}_DG2018_{1:yyyyMMdd-HHmm}.csv",
                        PSRConfig.ProjectCode, DateTime.Now));

                var sb = new StringBuilder();
                sb.AppendLine("# Verificación DG-2018 MTC Perú");
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "# V_diseno_kmh,{0}\n# clase,{1}\n# terreno,{2}\n", V, clase, terr);
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "# Rmin_m,{0}\n# pend_max_pct,{1}\n# peralte_max_pct,{2}\n",
                    rMin, pendMax, peralteMax);
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "# Dp_parada_m,{0:F2}\n# Kmin_convexa,{1}\n# Kmin_concava,{2}\n",
                    Dp, kConvexMin, kConcaveMin);
                sb.AppendLine();
                sb.AppendLine("seccion,tipo,pk_ini,pk_fin,valor,limite,estado,observacion");

                int okH = 0, koH = 0, okV = 0, koV = 0, okP = 0, koP = 0;

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    Alignment a = (Alignment)tr.GetObject(alignId, OpenMode.ForRead);

                    // ---- Curvas horizontales ----
                    foreach (AlignmentEntity ent in a.Entities)
                    {
                        if (ent.EntityType == AlignmentEntityType.Arc)
                        {
                            var arc = ent as AlignmentArc;
                            if (arc == null) continue;
                            double r = arc.Radius;
                            bool ok = r >= rMin;
                            sb.AppendFormat(CultureInfo.InvariantCulture,
                                "Horizontal,ARC,{0:F2},{1:F2},R={2:F2},Rmin={3:F2},{4},\n",
                                arc.StartStation, arc.EndStation, r, rMin,
                                ok ? "OK" : "NO CUMPLE");
                            if (ok) okH++; else koH++;
                        }
                        else if (ent.EntityType == AlignmentEntityType.Spiral)
                        {
                            var sp = ent as AlignmentSpiral;
                            if (sp == null) continue;
                            double lmin, amin;
                            PSRDG2018.ClotoideMinima(V, sp.RadiusIn > 0 ? sp.RadiusIn : sp.RadiusOut,
                                out amin, out lmin);
                            bool ok = sp.Length >= lmin;
                            sb.AppendFormat(CultureInfo.InvariantCulture,
                                "Horizontal,SPIRAL,{0:F2},{1:F2},L={2:F2},Lmin={3:F2},{4},A_min={5:F2}\n",
                                sp.StartStation, sp.EndStation, sp.Length, lmin,
                                ok ? "OK" : "NO CUMPLE", amin);
                            if (ok) okH++; else koH++;
                        }
                    }

                    // ---- Perfil longitudinal (proyecto) ----
                    Profile profPRY = null;
                    foreach (ObjectId pid in a.GetProfileIds())
                    {
                        Profile p = tr.GetObject(pid, OpenMode.ForRead) as Profile;
                        if (p != null && p.ProfileType == ProfileType.FG) { profPRY = p; break; }
                    }

                    if (profPRY != null)
                    {
                        foreach (ProfileEntity pe in profPRY.Entities)
                        {
                            if (pe.EntityType == ProfileEntityType.Tangent)
                            {
                                var t0 = pe as ProfileTangent;
                                if (t0 == null) continue;
                                double pendPct = Math.Abs(t0.Grade) * 100.0;
                                bool ok = pendPct <= pendMax + 1e-6 &&
                                          pendPct >= PSRDG2018.PendienteMinimaPct - 1e-6;
                                string obs = pendPct > pendMax
                                    ? "pendiente excede"
                                    : pendPct < PSRDG2018.PendienteMinimaPct
                                        ? "pendiente insuficiente (drenaje)"
                                        : "";
                                sb.AppendFormat(CultureInfo.InvariantCulture,
                                    "Perfil,TANGENTE,{0:F2},{1:F2},i={2:F2}%,imax={3:F2}%,{4},{5}\n",
                                    t0.StartStation, t0.EndStation, pendPct, pendMax,
                                    ok ? "OK" : "NO CUMPLE", obs);
                                if (ok) okP++; else koP++;
                            }
                            else if (pe.EntityType == ProfileEntityType.ParabolaSymmetric ||
                                     pe.EntityType == ProfileEntityType.ParabolaAsymmetric ||
                                     pe.EntityType == ProfileEntityType.Circular)
                            {
                                // PVC → PVT
                                double sIni = pe.StartStation;
                                double sFin = pe.EndStation;
                                double L = sFin - sIni;
                                double gradeIn = GetGrade(pe, true);
                                double gradeOut = GetGrade(pe, false);
                                double A = Math.Abs((gradeOut - gradeIn) * 100.0); // %
                                double K = A > 1e-9 ? L / A : double.PositiveInfinity;
                                bool convex = gradeIn > gradeOut; // cresta
                                double Kmin = convex ? kConvexMin : kConcaveMin;
                                bool ok = K >= Kmin;
                                sb.AppendFormat(CultureInfo.InvariantCulture,
                                    "Perfil,CV_{5},{0:F2},{1:F2},K={2:F2},Kmin={3:F2},{4},L={6:F2}\n",
                                    sIni, sFin, K, Kmin, ok ? "OK" : "NO CUMPLE",
                                    convex ? "CONVEXA" : "CONCAVA", L);
                                if (ok) okV++; else koV++;
                            }
                        }
                    }
                    else
                    {
                        ed.WriteMessage("\n[DG-2018] AVISO: alineamiento sin perfil de proyecto (FG).");
                    }

                    tr.Commit();
                }

                File.WriteAllText(csv, sb.ToString());
                ed.WriteMessage(string.Format(
                    "\n[DG-2018] Resultado: H {0}/{1} OK | Perfil {2}/{3} OK | CV {4}/{5} OK",
                    okH, okH + koH, okP, okP + koP, okV, okV + koV));
                ed.WriteMessage("\n[DG-2018] Reporte: " + csv);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n[PSR] Error en PSR_CHECK_DG2018: " + ex.Message);
            }
        }

        private static int ClampSpeed(int v)
        {
            if (v < 30) return 30;
            if (v > 130) return 130;
            // redondeo al múltiplo de 10 inferior más cercano presente en la tabla.
            int k = (v / 10) * 10;
            if (k < 30) k = 30;
            if (k > 130) k = 130;
            return k;
        }

        private static PSRDG2018.ClaseCarretera ParseClase(string kw)
        {
            switch (kw)
            {
                case "Autopista1": return PSRDG2018.ClaseCarretera.AutopistaPrimeraClase;
                case "Autopista2": return PSRDG2018.ClaseCarretera.AutopistaSegundaClase;
                case "Carretera1": return PSRDG2018.ClaseCarretera.CarreteraPrimeraClase;
                case "Carretera2": return PSRDG2018.ClaseCarretera.CarreteraSegundaClase;
                case "Carretera3": return PSRDG2018.ClaseCarretera.CarreteraTerceraClase;
                default: return PSRDG2018.ClaseCarretera.CarreteraSegundaClase;
            }
        }

        /// <summary>
        /// Obtiene la pendiente de entrada o salida de una entidad de
        /// perfil (tangente o curva vertical) en tanto por uno.
        /// </summary>
        private static double GetGrade(ProfileEntity pe, bool atStart)
        {
            try
            {
                if (pe is ProfileTangent pt) return pt.Grade;
                if (pe is ProfileParabolaSymmetric ps)
                    return atStart ? ps.GradeIn : ps.GradeOut;
                if (pe is ProfileParabolaAsymmetric pa)
                    return atStart ? pa.GradeIn : pa.GradeOut;
                if (pe is ProfileCircular pc)
                    return atStart ? pc.GradeIn : pc.GradeOut;
            }
            catch { }
            return 0;
        }
    }
}
