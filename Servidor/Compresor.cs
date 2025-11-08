using System.IO.Compression;
using TpFinalProgRedes.Web;

namespace TpFinalProgRedes.Servidor
{
    internal class Compresor
    {
        public static byte[] ComprimirGzip(byte[] datos)
        {
            try
            {
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    using (GZipStream gzipStream = new GZipStream(memoryStream, CompressionMode.Compress))
                    {
                        gzipStream.Write(datos, 0, datos.Length);
                    }
                    return memoryStream.ToArray();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al comprimir: {ex.Message}");
                return datos; // Devolver datos sin comprimir en caso de error
            }
        }

        public static bool ClienteSoportaGzip(SolicitudHTTP solicitud)
        {
            if (solicitud.Headers.ContainsKey("Accept-Encoding"))
            {
                string acceptEncoding = solicitud.Headers["Accept-Encoding"];
                return acceptEncoding.Contains("gzip", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }
    }
}