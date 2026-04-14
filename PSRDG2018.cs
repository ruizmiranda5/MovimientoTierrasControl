using System;
using System.Collections.Generic;

namespace MovimientoTierrasControl
{
    /// <summary>
    /// Tablas y fórmulas del "Manual de Carreteras: Diseño Geométrico
    /// DG-2018" (MTC, Perú). Sólo se incluyen los valores normativos
    /// usados por las comprobaciones automáticas PSR:
    ///   • Tabla 302.02 – Radio mínimo en curva horizontal.
    ///   • Tabla 303.01 – Pendientes máximas longitudinales.
    ///   • Fórmula Dp (distancia de parada).
    ///   • Tabla 303.02 – Pendiente mínima.
    ///   • Tabla 302.11/302.12 – Peralte máximo.
    ///   • Fórmula radio mínimo sin transición y longitud mínima de
    ///     clotoide (A_min).
    /// Este archivo NO sustituye al manual, sólo automatiza los checks
    /// rutinarios. Para casos especiales consulte el DG-2018 oficial.
    /// </summary>
    public static class PSRDG2018
    {
        public enum Terreno { Plano, Ondulado, Accidentado, Escarpado }

        public enum ClaseCarretera
        {
            AutopistaPrimeraClase,   // IMD > 6000 veh/día
            AutopistaSegundaClase,   // 6000 ≥ IMD > 4000
            CarreteraPrimeraClase,   // 4000 ≥ IMD > 2000
            CarreteraSegundaClase,   // 2000 ≥ IMD > 400
            CarreteraTerceraClase    // IMD ≤ 400
        }

        /// <summary>
        /// Tabla 302.02 DG-2018. Radio mínimo (m) para emax = 8 %
        /// y coeficientes f normativos, en función de la velocidad
        /// de diseño (km/h).
        /// </summary>
        public static readonly SortedDictionary<int, double> RadioMinimoEmax8 =
            new SortedDictionary<int, double>
            {
                { 30,  25 },
                { 40,  50 },
                { 50,  85 },
                { 60, 125 },
                { 70, 190 },
                { 80, 270 },
                { 90, 345 },
                { 100, 435 },
                { 110, 560 },
                { 120, 700 },
                { 130, 950 }
            };

        /// <summary>
        /// Tabla 302.12 DG-2018. Peralte máximo (%).
        /// </summary>
        public static readonly SortedDictionary<int, double> PeralteMaximoPct =
            new SortedDictionary<int, double>
            {
                { 30, 12 }, { 40, 12 }, { 50, 12 }, { 60, 10 },
                { 70, 10 }, { 80, 10 }, { 90,  8 }, { 100,  8 },
                { 110, 8 }, { 120, 8 }, { 130, 8 }
            };

        /// <summary>
        /// Tabla 303.01 DG-2018. Pendiente longitudinal máxima (%).
        /// </summary>
        public static double PendienteMaximaPct(ClaseCarretera c, Terreno t)
        {
            switch (c)
            {
                case ClaseCarretera.AutopistaPrimeraClase:
                case ClaseCarretera.AutopistaSegundaClase:
                    switch (t)
                    {
                        case Terreno.Plano: return 5;
                        case Terreno.Ondulado: return 5;
                        case Terreno.Accidentado: return 7;
                        case Terreno.Escarpado: return 8;
                    } break;
                case ClaseCarretera.CarreteraPrimeraClase:
                    switch (t)
                    {
                        case Terreno.Plano: return 6;
                        case Terreno.Ondulado: return 7;
                        case Terreno.Accidentado: return 9;
                        case Terreno.Escarpado: return 10;
                    } break;
                case ClaseCarretera.CarreteraSegundaClase:
                    switch (t)
                    {
                        case Terreno.Plano: return 6;
                        case Terreno.Ondulado: return 7;
                        case Terreno.Accidentado: return 9;
                        case Terreno.Escarpado: return 10;
                    } break;
                case ClaseCarretera.CarreteraTerceraClase:
                    switch (t)
                    {
                        case Terreno.Plano: return 8;
                        case Terreno.Ondulado: return 9;
                        case Terreno.Accidentado: return 10;
                        case Terreno.Escarpado: return 12;
                    } break;
            }
            return 10;
        }

        /// <summary>
        /// DG-2018 §303.3.2: pendiente mínima por drenaje = 0.5 %
        /// (0.3 % en zonas con bombeo eficaz).
        /// </summary>
        public const double PendienteMinimaPct = 0.5;

        /// <summary>
        /// DG-2018 §205 – rangos de velocidad de diseño normativos por
        /// clase y terreno (valor mínimo recomendado, km/h).
        /// </summary>
        public static int VelocidadDisenoMinima(ClaseCarretera c, Terreno t)
        {
            switch (c)
            {
                case ClaseCarretera.AutopistaPrimeraClase:
                    return t == Terreno.Plano ? 120 :
                           t == Terreno.Ondulado ? 100 :
                           t == Terreno.Accidentado ? 80 : 70;
                case ClaseCarretera.AutopistaSegundaClase:
                    return t == Terreno.Plano ? 100 :
                           t == Terreno.Ondulado ? 90 :
                           t == Terreno.Accidentado ? 80 : 70;
                case ClaseCarretera.CarreteraPrimeraClase:
                    return t == Terreno.Plano ? 90 :
                           t == Terreno.Ondulado ? 80 :
                           t == Terreno.Accidentado ? 60 : 50;
                case ClaseCarretera.CarreteraSegundaClase:
                    return t == Terreno.Plano ? 80 :
                           t == Terreno.Ondulado ? 70 :
                           t == Terreno.Accidentado ? 50 : 40;
                case ClaseCarretera.CarreteraTerceraClase:
                    return t == Terreno.Plano ? 60 :
                           t == Terreno.Ondulado ? 50 :
                           t == Terreno.Accidentado ? 40 : 30;
            }
            return 30;
        }

        /// <summary>
        /// Radio mínimo normativo de la tabla 302.02 a la velocidad v
        /// (km/h). Interpolación conservadora hacia el siguiente valor
        /// superior de la tabla.
        /// </summary>
        public static double RadioMinimoNormativo(int vKmh)
        {
            foreach (var kv in RadioMinimoEmax8)
                if (kv.Key >= vKmh) return kv.Value;
            return 950;
        }

        /// <summary>
        /// Distancia de parada Dp (m) DG-2018 fórmula §205:
        ///     Dp = V·tp/3.6 + V² / (254·(f ± i))
        /// tp = tiempo de percepción-reacción (2.5 s por defecto).
        /// f  = coeficiente de fricción longitudinal según V.
        /// i  = pendiente en tanto por uno (positivo subida, negativo bajada).
        /// </summary>
        public static double DistanciaParada(int vKmh, double iPct, double tp = 2.5)
        {
            double f = FriccionLongitudinal(vKmh);
            double i = iPct / 100.0;
            return vKmh * tp / 3.6 + (vKmh * vKmh) / (254.0 * (f - i));
        }

        /// <summary>
        /// Coeficiente de fricción longitudinal DG-2018 tabla 205.02
        /// (valores aproximados por interpolación lineal).
        /// </summary>
        public static double FriccionLongitudinal(int vKmh)
        {
            if (vKmh <= 30) return 0.40;
            if (vKmh <= 40) return 0.38;
            if (vKmh <= 50) return 0.35;
            if (vKmh <= 60) return 0.33;
            if (vKmh <= 70) return 0.31;
            if (vKmh <= 80) return 0.30;
            if (vKmh <= 90) return 0.30;
            if (vKmh <= 100) return 0.29;
            if (vKmh <= 110) return 0.28;
            if (vKmh <= 120) return 0.28;
            return 0.28;
        }

        /// <summary>
        /// Parámetro A mínimo de clotoide DG-2018 §302.6:
        ///     A_min = sqrt(R · L_min)  con  L_min = V³ / (46.656·R·J)
        /// J = variación máxima de aceleración centrífuga = 0.5 m/s³ (valor
        /// típico DG-2018 para c=0.4).
        /// Devuelve A (m) y Lmin (m).
        /// </summary>
        public static void ClotoideMinima(int vKmh, double radio,
            out double aMin, out double lMin, double J = 0.5)
        {
            lMin = (vKmh * vKmh * vKmh) / (46.656 * radio * J);
            aMin = Math.Sqrt(radio * lMin);
        }

        /// <summary>
        /// Longitud mínima y máxima de curva vertical DG-2018 §303.4.
        /// Sólo controlamos la relación K = L / |A| ≥ Kmin según visibilidad.
        /// Valores de K mínimo para curvas convexas, basados en distancia
        /// de parada (tabla 303.03 simplificada).
        /// </summary>
        public static double KMinConvexa(int vKmh)
        {
            if (vKmh <= 30) return 2;
            if (vKmh <= 40) return 4;
            if (vKmh <= 50) return 7;
            if (vKmh <= 60) return 12;
            if (vKmh <= 70) return 19;
            if (vKmh <= 80) return 30;
            if (vKmh <= 90) return 43;
            if (vKmh <= 100) return 60;
            if (vKmh <= 110) return 80;
            if (vKmh <= 120) return 105;
            return 135;
        }

        /// <summary>
        /// K mínimo para curvas verticales cóncavas (tabla 303.04
        /// simplificada por distancia de parada nocturna).
        /// </summary>
        public static double KMinConcava(int vKmh)
        {
            if (vKmh <= 30) return 4;
            if (vKmh <= 40) return 6;
            if (vKmh <= 50) return 9;
            if (vKmh <= 60) return 12;
            if (vKmh <= 70) return 17;
            if (vKmh <= 80) return 22;
            if (vKmh <= 90) return 28;
            if (vKmh <= 100) return 36;
            if (vKmh <= 110) return 43;
            if (vKmh <= 120) return 51;
            return 59;
        }
    }
}
