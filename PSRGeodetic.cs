using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;

namespace MovimientoTierrasControl
{
    /// <summary>
    /// Calculadora geodésica para topografía PSR.
    /// Implementa conversiones WGS84 ↔ UTM, DMS ↔ decimal,
    /// problema inverso/directo sobre elipsoide (Vincenty),
    /// cálculo de convergencia de meridianos, factor de escala
    /// y área por coordenadas (Gauss).
    /// Perú: zonas UTM 17 (-81°), 18 (-75°), 19 (-69°).
    /// </summary>
    public static class PSRGeodetic
    {
        // Elipsoide WGS84.
        public const double A = 6378137.0;
        public const double F = 1.0 / 298.257223563;
        public const double B = A * (1.0 - F);
        public const double E2 = F * (2.0 - F);
        public const double EP2 = E2 / (1.0 - E2);
        public const double K0 = 0.9996;

        public static double Deg2Rad(double d) { return d * Math.PI / 180.0; }
        public static double Rad2Deg(double r) { return r * 180.0 / Math.PI; }

        /// <summary>
        /// Decimal grados → (grados, minutos, segundos). Devuelve signo con
        /// el grado cuando el valor es negativo.
        /// </summary>
        public static void Dec2Dms(double dec, out int g, out int m, out double s)
        {
            int sign = dec < 0 ? -1 : 1;
            double abs = Math.Abs(dec);
            g = (int)Math.Floor(abs);
            double minFull = (abs - g) * 60.0;
            m = (int)Math.Floor(minFull);
            s = (minFull - m) * 60.0;
            g *= sign;
        }

        public static double Dms2Dec(int g, int m, double s)
        {
            int sign = g < 0 ? -1 : 1;
            return sign * (Math.Abs(g) + m / 60.0 + s / 3600.0);
        }

        /// <summary>
        /// Zona UTM a partir de la longitud (grados decimales).
        /// </summary>
        public static int UtmZone(double lonDeg)
        {
            int z = (int)Math.Floor((lonDeg + 180.0) / 6.0) + 1;
            if (z < 1) z = 1;
            if (z > 60) z = 60;
            return z;
        }

        public static double CentralMeridian(int zone) { return -183.0 + 6.0 * zone; }

        /// <summary>
        /// Geográficas WGS84 (lat,lon en grados) → UTM (E,N en metros).
        /// Si <paramref name="forceZone"/> ≥ 1 usa esa zona, si no la
        /// calcula. Para hemisferio sur se añade el falso Norte de
        /// 10,000,000 m automáticamente si <paramref name="southernHemisphere"/>.
        /// </summary>
        public static void LatLonToUtm(double latDeg, double lonDeg,
            out double east, out double north, out int zone,
            bool southernHemisphere = true, int forceZone = 0)
        {
            zone = forceZone >= 1 ? forceZone : UtmZone(lonDeg);
            double lon0 = Deg2Rad(CentralMeridian(zone));
            double phi = Deg2Rad(latDeg);
            double lam = Deg2Rad(lonDeg);

            double sinPhi = Math.Sin(phi);
            double cosPhi = Math.Cos(phi);
            double tanPhi = Math.Tan(phi);

            double N = A / Math.Sqrt(1.0 - E2 * sinPhi * sinPhi);
            double T = tanPhi * tanPhi;
            double C = EP2 * cosPhi * cosPhi;
            double Aa = cosPhi * (lam - lon0);

            double M = A * (
                (1 - E2 / 4.0 - 3 * E2 * E2 / 64.0 - 5 * E2 * E2 * E2 / 256.0) * phi
                - (3 * E2 / 8.0 + 3 * E2 * E2 / 32.0 + 45 * E2 * E2 * E2 / 1024.0) * Math.Sin(2 * phi)
                + (15 * E2 * E2 / 256.0 + 45 * E2 * E2 * E2 / 1024.0) * Math.Sin(4 * phi)
                - (35 * E2 * E2 * E2 / 3072.0) * Math.Sin(6 * phi));

            east = K0 * N * (Aa + (1 - T + C) * Math.Pow(Aa, 3) / 6.0
                + (5 - 18 * T + T * T + 72 * C - 58 * EP2) * Math.Pow(Aa, 5) / 120.0)
                + 500000.0;

            north = K0 * (M + N * tanPhi * (
                Aa * Aa / 2.0
                + (5 - T + 9 * C + 4 * C * C) * Math.Pow(Aa, 4) / 24.0
                + (61 - 58 * T + T * T + 600 * C - 330 * EP2) * Math.Pow(Aa, 6) / 720.0));

            if (southernHemisphere) north += 10000000.0;
        }

        /// <summary>
        /// UTM (E,N,zone) → Geográficas WGS84 (lat,lon en grados).
        /// </summary>
        public static void UtmToLatLon(double east, double north, int zone,
            bool southernHemisphere, out double latDeg, out double lonDeg)
        {
            double x = east - 500000.0;
            double y = southernHemisphere ? north - 10000000.0 : north;
            double lon0 = Deg2Rad(CentralMeridian(zone));

            double M = y / K0;
            double mu = M / (A * (1 - E2 / 4.0 - 3 * E2 * E2 / 64.0 - 5 * E2 * E2 * E2 / 256.0));
            double e1 = (1 - Math.Sqrt(1 - E2)) / (1 + Math.Sqrt(1 - E2));

            double phi1 = mu
                + (3 * e1 / 2.0 - 27 * e1 * e1 * e1 / 32.0) * Math.Sin(2 * mu)
                + (21 * e1 * e1 / 16.0 - 55 * e1 * e1 * e1 * e1 / 32.0) * Math.Sin(4 * mu)
                + (151 * e1 * e1 * e1 / 96.0) * Math.Sin(6 * mu);

            double sinPhi1 = Math.Sin(phi1);
            double cosPhi1 = Math.Cos(phi1);
            double tanPhi1 = Math.Tan(phi1);
            double C1 = EP2 * cosPhi1 * cosPhi1;
            double T1 = tanPhi1 * tanPhi1;
            double N1 = A / Math.Sqrt(1 - E2 * sinPhi1 * sinPhi1);
            double R1 = A * (1 - E2) / Math.Pow(1 - E2 * sinPhi1 * sinPhi1, 1.5);
            double D = x / (N1 * K0);

            double phi = phi1 - (N1 * tanPhi1 / R1) * (
                D * D / 2.0
                - (5 + 3 * T1 + 10 * C1 - 4 * C1 * C1 - 9 * EP2) * Math.Pow(D, 4) / 24.0
                + (61 + 90 * T1 + 298 * C1 + 45 * T1 * T1 - 252 * EP2 - 3 * C1 * C1) * Math.Pow(D, 6) / 720.0);

            double lam = lon0 + (
                D
                - (1 + 2 * T1 + C1) * Math.Pow(D, 3) / 6.0
                + (5 - 2 * C1 + 28 * T1 - 3 * C1 * C1 + 8 * EP2 + 24 * T1 * T1) * Math.Pow(D, 5) / 120.0)
                / cosPhi1;

            latDeg = Rad2Deg(phi);
            lonDeg = Rad2Deg(lam);
        }

        /// <summary>
        /// Convergencia de meridianos (grados) en una posición geográfica.
        /// γ ≈ (λ − λ0) · sen(φ), suficientemente precisa para topografía.
        /// </summary>
        public static double GridConvergenceDeg(double latDeg, double lonDeg, int zone)
        {
            double phi = Deg2Rad(latDeg);
            double dLam = Deg2Rad(lonDeg - CentralMeridian(zone));
            return Rad2Deg(dLam * Math.Sin(phi));
        }

        /// <summary>
        /// Factor de escala puntual UTM en una posición geográfica.
        /// k ≈ k0 · (1 + (cos²φ · (λ−λ0)²)/2).
        /// </summary>
        public static double PointScaleFactor(double latDeg, double lonDeg, int zone)
        {
            double phi = Deg2Rad(latDeg);
            double dLam = Deg2Rad(lonDeg - CentralMeridian(zone));
            return K0 * (1.0 + (Math.Cos(phi) * Math.Cos(phi) * dLam * dLam) / 2.0);
        }

        /// <summary>
        /// Problema inverso de Vincenty (elipsoide WGS84).
        /// Devuelve distancia geodésica (m), azimut directo y azimut
        /// inverso (grados). Iterativo, precisión ~mm.
        /// </summary>
        public static void VincentyInverse(
            double lat1Deg, double lon1Deg, double lat2Deg, double lon2Deg,
            out double distance, out double azimuth12, out double azimuth21)
        {
            double L = Deg2Rad(lon2Deg - lon1Deg);
            double U1 = Math.Atan((1 - F) * Math.Tan(Deg2Rad(lat1Deg)));
            double U2 = Math.Atan((1 - F) * Math.Tan(Deg2Rad(lat2Deg)));
            double sinU1 = Math.Sin(U1), cosU1 = Math.Cos(U1);
            double sinU2 = Math.Sin(U2), cosU2 = Math.Cos(U2);

            double lam = L, lamP, sinLam, cosLam;
            double sinSigma, cosSigma, sigma, sinAlpha, cos2Alpha, cos2SigmaM, C;
            int it = 0;
            do
            {
                sinLam = Math.Sin(lam);
                cosLam = Math.Cos(lam);
                sinSigma = Math.Sqrt(
                    (cosU2 * sinLam) * (cosU2 * sinLam) +
                    (cosU1 * sinU2 - sinU1 * cosU2 * cosLam) *
                    (cosU1 * sinU2 - sinU1 * cosU2 * cosLam));
                if (sinSigma == 0) { distance = 0; azimuth12 = 0; azimuth21 = 0; return; }
                cosSigma = sinU1 * sinU2 + cosU1 * cosU2 * cosLam;
                sigma = Math.Atan2(sinSigma, cosSigma);
                sinAlpha = cosU1 * cosU2 * sinLam / sinSigma;
                cos2Alpha = 1 - sinAlpha * sinAlpha;
                cos2SigmaM = cos2Alpha == 0 ? 0 : cosSigma - 2 * sinU1 * sinU2 / cos2Alpha;
                C = F / 16 * cos2Alpha * (4 + F * (4 - 3 * cos2Alpha));
                lamP = lam;
                lam = L + (1 - C) * F * sinAlpha *
                    (sigma + C * sinSigma *
                     (cos2SigmaM + C * cosSigma * (-1 + 2 * cos2SigmaM * cos2SigmaM)));
            } while (Math.Abs(lam - lamP) > 1e-12 && ++it < 200);

            double u2 = cos2Alpha * (A * A - B * B) / (B * B);
            double Acoef = 1 + u2 / 16384 * (4096 + u2 * (-768 + u2 * (320 - 175 * u2)));
            double Bcoef = u2 / 1024 * (256 + u2 * (-128 + u2 * (74 - 47 * u2)));
            double deltaSigma = Bcoef * sinSigma * (cos2SigmaM + Bcoef / 4 *
                (cosSigma * (-1 + 2 * cos2SigmaM * cos2SigmaM)
                - Bcoef / 6 * cos2SigmaM * (-3 + 4 * sinSigma * sinSigma)
                  * (-3 + 4 * cos2SigmaM * cos2SigmaM)));

            distance = B * Acoef * (sigma - deltaSigma);
            azimuth12 = Rad2Deg(Math.Atan2(cosU2 * sinLam,
                cosU1 * sinU2 - sinU1 * cosU2 * cosLam));
            azimuth21 = Rad2Deg(Math.Atan2(cosU1 * sinLam,
                -sinU1 * cosU2 + cosU1 * sinU2 * cosLam));
            if (azimuth12 < 0) azimuth12 += 360;
            if (azimuth21 < 0) azimuth21 += 360;
        }

        /// <summary>
        /// Área por coordenadas planas (Gauss / shoelace). Devuelve m².
        /// </summary>
        public static double PolygonArea(double[] east, double[] north)
        {
            int n = Math.Min(east.Length, north.Length);
            if (n < 3) return 0;
            double sum = 0;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                sum += east[i] * north[j] - east[j] * north[i];
            }
            return Math.Abs(sum) * 0.5;
        }

        [CommandMethod("PSR_CALC_GEODESICO")]
        public static void ShowCalculator()
        {
            Application.ShowModelessDialog(new PSRGeodeticForm());
        }
    }
}
