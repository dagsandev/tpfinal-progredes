using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TpFinalProgRedes.Web
{
    internal class SolicitudHTTP
    {
        public string Metodo { get; set; }
        public string Ruta { get; set; }
        public string QueryString { get; set; }
        public string VersionHTTP { get; set; }
        public string IPCliente { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        public string Body { get; set; }

        public SolicitudHTTP()
        {
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public override string ToString()
        {
            var headersString = string.Join(", ", Headers.Select(kvp => $"{kvp.Key}: {kvp.Value}"));

            return $"Metodo: {Metodo}, Ruta: {Ruta}, QueryString: {QueryString}, VersionHttp: {VersionHTTP}, IPCliente: {IPCliente}, \n" +
                $" Headers={{ {headersString} }}, Body: {Body} ";
        }
    }
}
