using System;
using System.Windows.Forms;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;

namespace MovimientoTierrasControl
{
    public class SelectionForm : Form
    {
        private Button btnSelectAlignment;
        private Button btnSelectSurface;
        private Button btnSelectSampleLine;

        public SelectionForm()
        {
            this.Text = "Selección de objetos Civil 3D";
            this.Width = 300;
            this.Height = 200;

            btnSelectAlignment = new Button { Text = "Seleccionar Alineamiento", Top = 20, Width = 250, Left = 20 };
            btnSelectSurface = new Button { Text = "Seleccionar Superficie", Top = 60, Width = 250, Left = 20 };
            btnSelectSampleLine = new Button { Text = "Seleccionar Sample Line", Top = 100, Width = 250, Left = 20 };

            btnSelectAlignment.Click += BtnSelectAlignment_Click;
            btnSelectSurface.Click += BtnSelectSurface_Click;
            btnSelectSampleLine.Click += BtnSelectSampleLine_Click;

            Controls.Add(btnSelectAlignment);
            Controls.Add(btnSelectSurface);
            Controls.Add(btnSelectSampleLine);
        }

        private void BtnSelectAlignment_Click(object sender, EventArgs e)
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            var res = ed.GetEntity("Selecciona un alineamiento:");
            if (res.Status == PromptStatus.OK)
                ed.WriteMessage("\nAlineamiento seleccionado: " + res.ObjectId.ToString());
        }

        private void BtnSelectSurface_Click(object sender, EventArgs e)
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            var res = ed.GetEntity("Selecciona una superficie:");
            if (res.Status == PromptStatus.OK)
                ed.WriteMessage("\nSuperficie seleccionada: " + res.ObjectId.ToString());
        }

        private void BtnSelectSampleLine_Click(object sender, EventArgs e)
        {
            var ed = Application.DocumentManager.MdiActiveDocument.Editor;
            var res = ed.GetEntity("Selecciona una sample line:");
            if (res.Status == PromptStatus.OK)
                ed.WriteMessage("\nSample Line seleccionada: " + res.ObjectId.ToString());
        }
    }
}