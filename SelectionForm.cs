using System;
using System.Drawing;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;

namespace MovimientoTierrasControl
{
    /// <summary>
    /// Menú principal PSR con botones para todas las herramientas
    /// disponibles. Cada botón llama al comando Civil 3D asociado
    /// usando Document.SendStringToExecute.
    /// </summary>
    public class SelectionForm : Form
    {
        public SelectionForm()
        {
            this.Text = "PSR - Movimiento de Tierras (" + PSRConfig.ProjectName + ")";
            this.Size = new Size(520, 640);
            this.StartPosition = FormStartPosition.CenterScreen;

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(10)
            };

            AddSection(flow, "Superficies y Volúmenes");
            AddCommand(flow, "Volumen entre superficies", "PSR_VOLUMEN");
            AddCommand(flow, "Listar superficies", "PSR_LISTAR_SUPERFICIES");
            AddCommand(flow, "Estadísticas de superficie", "PSR_ESTADISTICAS_SUPERFICIE");
            AddCommand(flow, "Actualizar / Rebuild superficie", "PSR_ACTUALIZAR_SUPERFICIE");

            AddSection(flow, "Alineamientos y Perfiles");
            AddCommand(flow, "Listar alineamientos", "PSR_LISTAR_ALINEAMIENTOS");
            AddCommand(flow, "Exportar perfil (TN/Proy) a CSV", "PSR_EXPORTAR_PERFIL");

            AddSection(flow, "Secciones y Sample Lines");
            AddCommand(flow, "Listar sample lines", "PSR_LISTAR_SAMPLELINES");
            AddCommand(flow, "Crear sample lines a intervalo", "PSR_CREAR_SAMPLELINES");
            AddCommand(flow, "Cubicación por secciones", "PSR_CUBICACION_SECCIONES");

            AddSection(flow, "Puntos COGO / Topografía");
            AddCommand(flow, "Importar puntos desde CSV", "PSR_IMPORTAR_PUNTOS");
            AddCommand(flow, "Exportar puntos a CSV", "PSR_EXPORTAR_PUNTOS");
            AddCommand(flow, "Crear grupo de puntos por prefijo", "PSR_GRUPO_PUNTOS");

            AddSection(flow, "Parcelas, Corredores, Redes");
            AddCommand(flow, "Listar parcelas", "PSR_LISTAR_PARCELAS");
            AddCommand(flow, "Listar corredores", "PSR_LISTAR_CORREDORES");
            AddCommand(flow, "Rebuild corredores", "PSR_REBUILD_CORREDORES");
            AddCommand(flow, "Listar redes de tuberías", "PSR_LISTAR_REDES");

            AddSection(flow, "Control de Calidad (QA/QC)");
            AddCommand(flow, "Comparar puntos vs superficie", "PSR_QAQC_PUNTOS");

            AddSection(flow, "Intercambio / Trimble");
            AddCommand(flow, "Exportar LandXML", "PSR_EXPORTAR_LANDXML");
            AddCommand(flow, "Importar LandXML", "PSR_IMPORTAR_LANDXML");
            AddCommand(flow, "Exportar puntos a Trimble", "PSR_EXPORTAR_TRIMBLE");
            AddCommand(flow, "Exportar replanteo de eje", "PSR_REPLANTEO_EJE");

            AddSection(flow, "Reportes");
            AddCommand(flow, "Diagrama de masas (Bruckner)", "PSR_DIAGRAMA_MASAS");
            AddCommand(flow, "Reporte ejecutivo HTML", "PSR_REPORTE_EJECUTIVO");

            AddSection(flow, "Geodesia y Norma DG-2018");
            AddCommand(flow, "Calculadora geodésica (WGS84/UTM)", "PSR_CALC_GEODESICO");
            AddCommand(flow, "Verificar alineamiento DG-2018", "PSR_CHECK_DG2018");

            this.Controls.Add(flow);
        }

        private static void AddSection(FlowLayoutPanel parent, string title)
        {
            var lbl = new Label
            {
                Text = title,
                AutoSize = false,
                Width = 470,
                Height = 26,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(52, 152, 219),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                Margin = new Padding(0, 10, 0, 4)
            };
            parent.Controls.Add(lbl);
        }

        private static void AddCommand(FlowLayoutPanel parent, string label, string command)
        {
            var btn = new Button
            {
                Text = label + "   [" + command + "]",
                Width = 470,
                Height = 30,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 2, 0, 2)
            };
            btn.Click += (s, e) =>
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                    doc.SendStringToExecute(command + " ", true, false, true);
            };
            parent.Controls.Add(btn);
        }
    }
}
