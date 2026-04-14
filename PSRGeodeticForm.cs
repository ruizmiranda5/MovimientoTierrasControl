using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace MovimientoTierrasControl
{
    /// <summary>
    /// Formulario interactivo para la calculadora geodésica PSR.
    /// Tres pestañas: UTM↔LatLon, Vincenty (distancia/azimut),
    /// DMS↔Decimal.
    /// </summary>
    public class PSRGeodeticForm : Form
    {
        private TabControl tabs;

        // UTM tab.
        private TextBox txtLat, txtLon, txtZone, txtEast, txtNorth, txtConv, txtK;
        private CheckBox chkSouth;

        // Vincenty tab.
        private TextBox txtLat1, txtLon1, txtLat2, txtLon2, txtDist, txtAz12, txtAz21;

        // DMS tab.
        private TextBox txtDec, txtG, txtM, txtS;

        public PSRGeodeticForm()
        {
            Text = "PSR · Calculadora Geodésica (WGS84)";
            Size = new Size(560, 460);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9);

            tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(BuildUtmTab());
            tabs.TabPages.Add(BuildVincentyTab());
            tabs.TabPages.Add(BuildDmsTab());
            Controls.Add(tabs);
        }

        // ------------- UTM ↔ LatLon -------------
        private TabPage BuildUtmTab()
        {
            var tp = new TabPage("UTM ↔ Lat/Lon");
            int y = 15;
            tp.Controls.Add(new Label { Text = "Latitud (°dec):", Left = 15, Top = y, Width = 120 });
            txtLat = new TextBox { Left = 140, Top = y, Width = 140, Text = "-12.046374" };
            tp.Controls.Add(txtLat);
            tp.Controls.Add(new Label { Text = "Longitud (°dec):", Left = 295, Top = y, Width = 120 });
            txtLon = new TextBox { Left = 420, Top = y, Width = 110, Text = "-77.042793" };
            tp.Controls.Add(txtLon);

            y += 30;
            tp.Controls.Add(new Label { Text = "Zona (0=auto):", Left = 15, Top = y, Width = 120 });
            txtZone = new TextBox { Left = 140, Top = y, Width = 60, Text = "0" };
            tp.Controls.Add(txtZone);
            chkSouth = new CheckBox { Left = 220, Top = y - 2, Width = 160,
                Text = "Hemisferio Sur", Checked = true };
            tp.Controls.Add(chkSouth);

            y += 35;
            var btnFwd = new Button { Text = "Lat/Lon → UTM", Left = 15, Top = y, Width = 250 };
            btnFwd.Click += (s, e) => LatLonToUtm();
            tp.Controls.Add(btnFwd);
            var btnInv = new Button { Text = "UTM → Lat/Lon", Left = 280, Top = y, Width = 250 };
            btnInv.Click += (s, e) => UtmToLatLon();
            tp.Controls.Add(btnInv);

            y += 40;
            tp.Controls.Add(new Label { Text = "Este (m):", Left = 15, Top = y, Width = 120 });
            txtEast = new TextBox { Left = 140, Top = y, Width = 140 };
            tp.Controls.Add(txtEast);
            tp.Controls.Add(new Label { Text = "Norte (m):", Left = 295, Top = y, Width = 120 });
            txtNorth = new TextBox { Left = 420, Top = y, Width = 110 };
            tp.Controls.Add(txtNorth);

            y += 30;
            tp.Controls.Add(new Label { Text = "Convergencia (°):", Left = 15, Top = y, Width = 120 });
            txtConv = new TextBox { Left = 140, Top = y, Width = 140, ReadOnly = true };
            tp.Controls.Add(txtConv);
            tp.Controls.Add(new Label { Text = "Factor escala k:", Left = 295, Top = y, Width = 120 });
            txtK = new TextBox { Left = 420, Top = y, Width = 110, ReadOnly = true };
            tp.Controls.Add(txtK);

            return tp;
        }

        private void LatLonToUtm()
        {
            try
            {
                double lat = ParseD(txtLat.Text);
                double lon = ParseD(txtLon.Text);
                int zf = int.Parse(txtZone.Text);
                double e, n; int z;
                PSRGeodetic.LatLonToUtm(lat, lon, out e, out n, out z,
                    chkSouth.Checked, zf);
                txtEast.Text = e.ToString("F4", CultureInfo.InvariantCulture);
                txtNorth.Text = n.ToString("F4", CultureInfo.InvariantCulture);
                txtZone.Text = z.ToString();
                txtConv.Text = PSRGeodetic.GridConvergenceDeg(lat, lon, z)
                    .ToString("F6", CultureInfo.InvariantCulture);
                txtK.Text = PSRGeodetic.PointScaleFactor(lat, lon, z)
                    .ToString("F8", CultureInfo.InvariantCulture);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void UtmToLatLon()
        {
            try
            {
                double e = ParseD(txtEast.Text);
                double n = ParseD(txtNorth.Text);
                int z = int.Parse(txtZone.Text);
                if (z < 1) { MessageBox.Show("Zona UTM inválida."); return; }
                double lat, lon;
                PSRGeodetic.UtmToLatLon(e, n, z, chkSouth.Checked, out lat, out lon);
                txtLat.Text = lat.ToString("F8", CultureInfo.InvariantCulture);
                txtLon.Text = lon.ToString("F8", CultureInfo.InvariantCulture);
                txtConv.Text = PSRGeodetic.GridConvergenceDeg(lat, lon, z)
                    .ToString("F6", CultureInfo.InvariantCulture);
                txtK.Text = PSRGeodetic.PointScaleFactor(lat, lon, z)
                    .ToString("F8", CultureInfo.InvariantCulture);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        // ------------- Vincenty -------------
        private TabPage BuildVincentyTab()
        {
            var tp = new TabPage("Vincenty");
            int y = 15;
            tp.Controls.Add(new Label { Text = "P1 Lat / Lon:", Left = 15, Top = y, Width = 100 });
            txtLat1 = new TextBox { Left = 120, Top = y, Width = 160, Text = "-12.046374" };
            txtLon1 = new TextBox { Left = 290, Top = y, Width = 160, Text = "-77.042793" };
            tp.Controls.Add(txtLat1); tp.Controls.Add(txtLon1);

            y += 30;
            tp.Controls.Add(new Label { Text = "P2 Lat / Lon:", Left = 15, Top = y, Width = 100 });
            txtLat2 = new TextBox { Left = 120, Top = y, Width = 160, Text = "-13.531950" };
            txtLon2 = new TextBox { Left = 290, Top = y, Width = 160, Text = "-71.967461" };
            tp.Controls.Add(txtLat2); tp.Controls.Add(txtLon2);

            y += 35;
            var btn = new Button { Text = "Calcular distancia y azimutes", Left = 15, Top = y, Width = 435 };
            btn.Click += (s, e) => CalcVincenty();
            tp.Controls.Add(btn);

            y += 40;
            tp.Controls.Add(new Label { Text = "Distancia (m):", Left = 15, Top = y, Width = 110 });
            txtDist = new TextBox { Left = 130, Top = y, Width = 320, ReadOnly = true };
            tp.Controls.Add(txtDist);

            y += 30;
            tp.Controls.Add(new Label { Text = "Azimut 1→2 (°):", Left = 15, Top = y, Width = 110 });
            txtAz12 = new TextBox { Left = 130, Top = y, Width = 320, ReadOnly = true };
            tp.Controls.Add(txtAz12);

            y += 30;
            tp.Controls.Add(new Label { Text = "Azimut 2→1 (°):", Left = 15, Top = y, Width = 110 });
            txtAz21 = new TextBox { Left = 130, Top = y, Width = 320, ReadOnly = true };
            tp.Controls.Add(txtAz21);

            return tp;
        }

        private void CalcVincenty()
        {
            try
            {
                double d, a12, a21;
                PSRGeodetic.VincentyInverse(
                    ParseD(txtLat1.Text), ParseD(txtLon1.Text),
                    ParseD(txtLat2.Text), ParseD(txtLon2.Text),
                    out d, out a12, out a21);
                txtDist.Text = d.ToString("F3", CultureInfo.InvariantCulture);
                txtAz12.Text = a12.ToString("F6", CultureInfo.InvariantCulture);
                txtAz21.Text = a21.ToString("F6", CultureInfo.InvariantCulture);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        // ------------- DMS -------------
        private TabPage BuildDmsTab()
        {
            var tp = new TabPage("DMS ↔ Decimal");
            int y = 15;
            tp.Controls.Add(new Label { Text = "Decimal (°):", Left = 15, Top = y, Width = 100 });
            txtDec = new TextBox { Left = 120, Top = y, Width = 160, Text = "-12.046374" };
            tp.Controls.Add(txtDec);

            y += 30;
            tp.Controls.Add(new Label { Text = "G ° M ' S \":", Left = 15, Top = y, Width = 100 });
            txtG = new TextBox { Left = 120, Top = y, Width = 60 };
            txtM = new TextBox { Left = 185, Top = y, Width = 60 };
            txtS = new TextBox { Left = 250, Top = y, Width = 90 };
            tp.Controls.Add(txtG); tp.Controls.Add(txtM); tp.Controls.Add(txtS);

            y += 35;
            var b1 = new Button { Text = "Decimal → DMS", Left = 15, Top = y, Width = 200 };
            b1.Click += (s, e) =>
            {
                int g, m; double sec;
                PSRGeodetic.Dec2Dms(ParseD(txtDec.Text), out g, out m, out sec);
                txtG.Text = g.ToString();
                txtM.Text = m.ToString();
                txtS.Text = sec.ToString("F4", CultureInfo.InvariantCulture);
            };
            tp.Controls.Add(b1);

            var b2 = new Button { Text = "DMS → Decimal", Left = 230, Top = y, Width = 200 };
            b2.Click += (s, e) =>
            {
                int g = int.Parse(txtG.Text);
                int m = int.Parse(txtM.Text);
                double sec = ParseD(txtS.Text);
                txtDec.Text = PSRGeodetic.Dms2Dec(g, m, sec)
                    .ToString("F10", CultureInfo.InvariantCulture);
            };
            tp.Controls.Add(b2);

            return tp;
        }

        private static double ParseD(string s)
        {
            return double.Parse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture);
        }
    }
}
