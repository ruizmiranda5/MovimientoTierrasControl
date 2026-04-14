// ============================================================================
// Integración con Trimble Business Center (TBC) SDK.
//
// Este archivo solo se compila cuando el símbolo TBC_SDK está definido en
// el .csproj. TBC exige las DLLs de "Trimble.Vce.*" instaladas en:
//   %ProgramFiles%\Trimble\Trimble Business Center\
// Se añaden referencias con:
//   <Reference Include="Trimble.Vce.Core" />
//   <Reference Include="Trimble.Vce.Data" />
//   <Reference Include="Trimble.Vce.Alignment" />
//   <Reference Include="Trimble.Vce.Surface" />
//   <Reference Include="Trimble.Vce.GeometricObjects" />
//   <Reference Include="Trimble.Vce.Command" />
//
// Fuera de TBC (modo Civil 3D) se mantienen los exportadores CSV/LandXML
// como puente documentado.
// ============================================================================
#if TBC_SDK
using System;
using System.Globalization;
using System.IO;
using System.Text;
using Trimble.Vce.Alignment;
using Trimble.Vce.Command;
using Trimble.Vce.Core;
using Trimble.Vce.Data;
using Trimble.Vce.GeometricObjects;
using Trimble.Vce.Surface;

namespace MovimientoTierrasControl.TBC
{
    /// <summary>
    /// Macro para TBC que replica las herramientas PSR_* usando el
    /// SDK nativo de Trimble Business Center. Este código solo se compila
    /// en un proyecto paralelo con el símbolo TBC_SDK y las referencias
    /// Trimble.Vce.* resueltas.
    /// </summary>
    public class PSR_TBC_Macro : Macro
    {
        public override void Execute()
        {
            Project project = Project.Current;
            if (project == null)
            {
                Messages.Info("[PSR-TBC] No hay proyecto activo.");
                return;
            }

            ExportarPuntosCSV(project);
            ExportarAlineamientosCSV(project);
            ExportarSuperficieEstadisticas(project);
            CalcularVolumenEntreSuperficies(project);
        }

        /// <summary>
        /// Exporta todos los puntos del proyecto TBC al formato estándar
        /// Trimble (P,N,E,Z,Code).
        /// </summary>
        public static void ExportarPuntosCSV(Project project)
        {
            EnsureOutputDir();
            string path = Path.Combine(OutputDir,
                string.Format("PSR_TBC_puntos_{0:yyyyMMdd-HHmm}.csv", DateTime.Now));
            var sb = new StringBuilder();
            sb.AppendLine("point,northing,easting,elevation,code");
            foreach (Point p in project.GetAll<Point>())
            {
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "{0},{1:F4},{2:F4},{3:F4},{4}\n",
                    p.Name, p.Northing, p.Easting, p.Elevation,
                    (p.FeatureCode ?? "").Replace(',', ' '));
            }
            File.WriteAllText(path, sb.ToString());
            Messages.Info("[PSR-TBC] Puntos exportados: " + path);
        }

        /// <summary>
        /// Exporta los alineamientos horizontales del proyecto TBC con su
        /// longitud y estaciones inicial/final.
        /// </summary>
        public static void ExportarAlineamientosCSV(Project project)
        {
            EnsureOutputDir();
            string path = Path.Combine(OutputDir,
                string.Format("PSR_TBC_alineamientos_{0:yyyyMMdd-HHmm}.csv", DateTime.Now));
            var sb = new StringBuilder();
            sb.AppendLine("alignment,length,start_station,end_station");
            foreach (HorizontalAlignment ha in project.GetAll<HorizontalAlignment>())
            {
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "{0},{1:F3},{2:F3},{3:F3}\n",
                    ha.Name, ha.Length, ha.StartStation, ha.EndStation);
            }
            File.WriteAllText(path, sb.ToString());
            Messages.Info("[PSR-TBC] Alineamientos exportados: " + path);
        }

        /// <summary>
        /// Exporta las estadísticas de cada superficie TBC del proyecto.
        /// </summary>
        public static void ExportarSuperficieEstadisticas(Project project)
        {
            EnsureOutputDir();
            string path = Path.Combine(OutputDir,
                string.Format("PSR_TBC_superficies_{0:yyyyMMdd-HHmm}.csv", DateTime.Now));
            var sb = new StringBuilder();
            sb.AppendLine("surface,points,triangles,z_min,z_max,area_2d,area_3d");
            foreach (Surface s in project.GetAll<Surface>())
            {
                SurfaceStatistics st = s.GetStatistics();
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3:F3},{4:F3},{5:F2},{6:F2}\n",
                    s.Name, st.PointCount, st.TriangleCount,
                    st.MinElevation, st.MaxElevation,
                    st.Area2D, st.Area3D);
            }
            File.WriteAllText(path, sb.ToString());
            Messages.Info("[PSR-TBC] Superficies exportadas: " + path);
        }

        /// <summary>
        /// Calcula el volumen entre las dos primeras superficies del
        /// proyecto TBC usando Surface.ComputeVolume y lo registra.
        /// </summary>
        public static void CalcularVolumenEntreSuperficies(Project project)
        {
            Surface baseS = null, compS = null;
            foreach (Surface s in project.GetAll<Surface>())
            {
                if (s.Name.Equals("PSR-TN", StringComparison.OrdinalIgnoreCase)) baseS = s;
                else if (s.Name.Equals("PSR-PROY", StringComparison.OrdinalIgnoreCase)) compS = s;
            }
            if (baseS == null || compS == null)
            {
                Messages.Warn("[PSR-TBC] No se encontraron PSR-TN y PSR-PROY.");
                return;
            }

            VolumeResult vr = Surface.ComputeVolume(baseS, compS);
            Messages.Info(string.Format(
                "[PSR-TBC] Corte: {0:N2} m³ | Relleno: {1:N2} m³ | Neto: {2:N2} m³",
                vr.Cut, vr.Fill, vr.Cut - vr.Fill));

            EnsureOutputDir();
            string csv = Path.Combine(OutputDir,
                string.Format("PSR_TBC_volumen_{0:yyyyMMdd-HHmm}.csv", DateTime.Now));
            File.WriteAllText(csv,
                string.Format(CultureInfo.InvariantCulture,
                    "concepto,m3\ncorte,{0:F2}\nrelleno,{1:F2}\nneto,{2:F2}\n",
                    vr.Cut, vr.Fill, vr.Cut - vr.Fill));
        }

        private const string OutputDir = @"C:\PSR\Reportes";
        private static void EnsureOutputDir()
        {
            if (!Directory.Exists(OutputDir)) Directory.CreateDirectory(OutputDir);
        }
    }
}
#endif
