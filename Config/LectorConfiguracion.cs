using System;
using System.IO;
using System.Text.Json;

namespace TpFinalProgRedes.Config
{
    internal class LectorConfiguracion
    {
        public static Configuracion Cargar(string rutaArchivo = "config.json")
        {
            try
            {
                if (!File.Exists(rutaArchivo))
                {
                    Console.WriteLine($"Advertencia: No se encontró el archivo {rutaArchivo}. Usando valores por defecto.");
                    return new Configuracion();
                }

                string json = File.ReadAllText(rutaArchivo);
                Configuracion config = JsonSerializer.Deserialize<Configuracion>(json);

                Console.WriteLine("=== CONFIGURACIÓN CARGADA ===");
                Console.WriteLine($"Puerto: {config.Puerto}");
                Console.WriteLine($"Carpeta de archivos: {config.CarpetaArchivos}");
                Console.WriteLine("=============================");

                return config;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al cargar configuración: {ex.Message}");
                Console.WriteLine("Usando valores por defecto.");
                return new Configuracion();
            }
        }
    }
}