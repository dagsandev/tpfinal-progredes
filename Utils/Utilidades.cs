namespace TpFinalProgRedes.Utils
{
    internal class Utilidades
    {
        public static string ObtenerContentType(string extension)
        {
            switch (extension)
            {
                case ".html":
                case ".htm":
                    return "text/html; charset=utf-8";
                case ".css":
                    return "text/css; charset=utf-8";
                case ".js":
                    return "application/javascript; charset=utf-8";
                case ".png":
                    return "image/png";
                case ".json":
                    return "application/json; charset=utf-8";
                default:
                    return "application/octet-stream";
            }
        }
    }
}
