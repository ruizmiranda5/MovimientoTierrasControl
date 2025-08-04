using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace MovimientoTierrasControl
{
    public class ChartOptionsForm : Form
    {
        public string Titulo { get; private set; }
        public string ColorHex { get; private set; }
        public string TextoAdicional { get; private set; }

        private TextBox txtTitulo;
        private TextBox txtColor;
        private TextBox txtTexto;
        private Button btnAceptar;

        public ChartOptionsForm()
        {
            this.Text = "Opciones de Gráfico";
            this.Size = new Size(400, 250);

            Label lblTitulo = new Label { Text = "Título del gráfico:", Top = 20, Left = 20, Width = 120 };
            txtTitulo = new TextBox { Top = 20, Left = 150, Width = 200, Text = "Mi gráfico" };

            Label lblColor = new Label { Text = "Color (hex):", Top = 60, Left = 20, Width = 120 };
            txtColor = new TextBox { Top = 60, Left = 150, Width = 200, Text = "#3498db" };

            Label lblTexto = new Label { Text = "Texto adicional:", Top = 100, Left = 20, Width = 120 };
            txtTexto = new TextBox { Top = 100, Left = 150, Width = 200 };

            btnAceptar = new Button { Text = "Aceptar", Top = 150, Left = 150 };
            btnAceptar.Click += BtnAceptar_Click;

            this.Controls.Add(lblTitulo);
            this.Controls.Add(txtTitulo);
            this.Controls.Add(lblColor);
            this.Controls.Add(txtColor);
            this.Controls.Add(lblTexto);
            this.Controls.Add(txtTexto);
            this.Controls.Add(btnAceptar);
        }

        private void BtnAceptar_Click(object sender, EventArgs e)
        {
            Titulo = txtTitulo.Text;
            ColorHex = txtColor.Text;
            TextoAdicional = txtTexto.Text;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}