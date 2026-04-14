namespace MovimientoTierrasControl
{
    /// <summary>
    /// Configuración específica del Proyecto Santa Rosa (PSR).
    /// Todas las herramientas PSR_* leen defaults desde aquí para
    /// mantener consistencia entre comandos.
    /// </summary>
    public static class PSRConfig
    {
        public const string ProjectCode = "PSR";
        public const string ProjectName = "Proyecto Santa Rosa";

        // Nombres esperados de superficies del proyecto.
        public const string SurfaceTerrenoNatural = "PSR-TN";
        public const string SurfaceProyecto = "PSR-PROY";
        public const string SurfaceVolumenPrefix = "PSR-VOL";

        // Alineamiento principal por defecto.
        public const string AlineamientoPrincipal = "PSR-EJE";

        // Intervalo estándar de secciones transversales (m).
        public const double IntervaloSeccionesMetros = 20.0;
        public const double AnchoSeccionIzquierda = 30.0;
        public const double AnchoSeccionDerecha = 30.0;

        // Intervalo de muestreo para datos de perfil (m).
        public const double IntervaloPerfilMetros = 10.0;

        // Factores de expansión / compactación para volúmenes ajustados.
        public const double FactorExpansionRelleno = 1.25;
        public const double FactorCompactacionCorte = 0.90;

        // Salidas por defecto.
        public const string CarpetaSalida = @"C:\PSR\Reportes";
    }
}
