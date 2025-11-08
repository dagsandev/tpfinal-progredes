namespace TpFinalProgRedes.Config
{
    internal class Configuracion
    {
        public int Puerto { get; set; }
        public string CarpetaArchivos { get; set; }

        public Configuracion()
        {
            // Valores por defecto
            Puerto = 8080;
            CarpetaArchivos = "www";
        }
    }
}