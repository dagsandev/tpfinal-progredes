using System;
using System.IO;
using System.Text;

namespace TpFinalProgRedes.Servidor
{
    internal class Logger
    {
        private static readonly object lockObject = new object();
        private const string carpetaLogs = "logs";

        static Logger()
        {
            // Crear carpeta de logs si no existe
            if (!Directory.Exists(carpetaLogs))
            {
                Directory.CreateDirectory(carpetaLogs);
            }
        }

        public static void LoguearSolicitud(string ip, string metodo, string ruta, string queryString, int codigoRespuesta)
        {
            try
            {
                // Nombre del archivo basado en la fecha actual
                string fecha = DateTime.Now.ToString("yyyy-MM-dd");
                string nombreArchivo = $"log_{fecha}.txt";
                string rutaArchivo = Path.Combine(carpetaLogs, nombreArchivo);

                // Construir línea de log
                StringBuilder lineaLog = new StringBuilder();
                lineaLog.Append($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ");
                lineaLog.Append($"IP: {ip} | ");
                lineaLog.Append($"Método: {metodo} | ");
                lineaLog.Append($"Ruta: {ruta}");

                if (!string.IsNullOrEmpty(queryString))
                {
                    lineaLog.Append($"?{queryString}");
                }

                lineaLog.Append($" | Respuesta: {codigoRespuesta}");

                // Usar lock para evitar problemas de concurrencia al escribir
                lock (lockObject)
                {
                    File.AppendAllText(rutaArchivo, lineaLog.ToString() + Environment.NewLine);
                }

                Console.WriteLine($"[LOG] {lineaLog}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al escribir log: {ex.Message}");
            }
        }

        public static void LoguearPost(string ip, string ruta, string datosPost)
        {
            try
            {
                string fecha = DateTime.Now.ToString("yyyy-MM-dd");
                string nombreArchivo = $"log_{fecha}.txt";
                string rutaArchivo = Path.Combine(carpetaLogs, nombreArchivo);

                StringBuilder lineaLog = new StringBuilder();
                lineaLog.Append($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ");
                lineaLog.Append($"IP: {ip} | ");
                lineaLog.Append($"POST {ruta} | ");
                lineaLog.Append($"Datos: {datosPost}");

                lock (lockObject)
                {
                    File.AppendAllText(rutaArchivo, lineaLog.ToString() + Environment.NewLine);
                }

                Console.WriteLine($"[LOG POST] {lineaLog}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al escribir log POST: {ex.Message}");
            }
        }
    }
}