using System.Text;

namespace TpFinalProgRedes.Web
{
    internal class RespuestaHTTP
    {
        public int CodigoEstado { get; set; }
        public string MensajeEstado { get; set; }
        public Dictionary<string, string> Encabezados { get; set; }
        public byte[] Cuerpo { get; set; }

        public RespuestaHTTP()
        {
            Encabezados = new Dictionary<string, string>();
            CodigoEstado = 200;
            MensajeEstado = "OK";
        }

        public byte[] ConstruirRespuesta()
        {
            // Línea de estado: HTTP/1.1 200 OK
            StringBuilder respuesta = new StringBuilder();
            respuesta.AppendLine($"HTTP/1.1 {CodigoEstado} {MensajeEstado}");

            // Encabezados
            foreach (var encabezado in Encabezados)
            {
                respuesta.AppendLine($"{encabezado.Key}: {encabezado.Value}");
            }

            // Línea en blanco que separa encabezados del cuerpo
            respuesta.AppendLine();

            // Convertir encabezados a bytes
            byte[] encabezadosBytes = Encoding.UTF8.GetBytes(respuesta.ToString());

            // Si hay cuerpo, combinarlo con los encabezados
            if (Cuerpo != null && Cuerpo.Length > 0)
            {
                byte[] respuestaCompleta = new byte[encabezadosBytes.Length + Cuerpo.Length];
                Buffer.BlockCopy(encabezadosBytes, 0, respuestaCompleta, 0, encabezadosBytes.Length);
                Buffer.BlockCopy(Cuerpo, 0, respuestaCompleta, encabezadosBytes.Length, Cuerpo.Length);
                return respuestaCompleta;
            }

            return encabezadosBytes;
        }

    }
}