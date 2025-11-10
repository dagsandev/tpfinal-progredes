using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using TpFinalProgRedes.Config;
using TpFinalProgRedes.Servidor;
using TpFinalProgRedes.Utils;
using TpFinalProgRedes.Web;

namespace TpFinalProgRedes
{
    internal class ServidorWeb
    {
        private static readonly object ConsolaLock = new object();
        private static string carpetaArchivos;

        static void Main(string[] args)
        {
            Configuracion config = LectorConfiguracion.Cargar();
            carpetaArchivos = config.CarpetaArchivos;

            Socket socketEscucha = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp
            );

            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, config.Puerto);

            socketEscucha.Bind(endPoint);

            socketEscucha.Listen(10);

            Console.WriteLine($"Servidor escuchando en el puerto {config.Puerto}...");
            Console.WriteLine("Presiona Ctrl+C para detener el servidor");
      
            while (true)
            {
                Socket socketCliente = socketEscucha.Accept();

                Log("ACCEPT", $"[Thread {Thread.CurrentThread.ManagedThreadId}] Cliente conectado", ConsoleColor.Yellow);

                Thread hiloCliente = new Thread(() => AtenderCliente(socketCliente));
                hiloCliente.Start();
            }

        }        

        static void AtenderCliente(Socket cliente)
        {
            Log("THREAD", $"[Thread {Thread.CurrentThread.ManagedThreadId}] Atendiendo cliente...", ConsoleColor.Gray);

            try
            {
                cliente.ReceiveTimeout = 5000;

                IPEndPoint remoteEndPoint = cliente.RemoteEndPoint as IPEndPoint;
                string ipCliente = remoteEndPoint?.Address.ToString() ?? "Desconocida";
 
                byte[] buffer = new byte[8192];  

                int bytesRecibidos = cliente.Receive(buffer);

                if (bytesRecibidos == 0)
                {
                    Log("WARN", $"[Thread {Thread.CurrentThread.ManagedThreadId}] Cliente {ipCliente} no envió datos, cerrando conexión...", ConsoleColor.Yellow);
                    return;
                }

                string solicitudHTTP = Encoding.UTF8.GetString(buffer, 0, bytesRecibidos);

                SolicitudHTTP solicitud = ParsearSolicitud(solicitudHTTP);
                solicitud.IPCliente = ipCliente;

                if (!string.IsNullOrEmpty(solicitud.QueryString))
                {
                    string salidaFormateadaQueryString = solicitud.QueryString.Replace("&", ", ");
                    Log("REQ", $"[Thread {Thread.CurrentThread.ManagedThreadId}] Método: {solicitud.Metodo} | Ruta: {solicitud.Ruta} | IP: {solicitud.IPCliente} | Versión HTTP: {solicitud.VersionHTTP} | ParametrosQuery: {salidaFormateadaQueryString}", ConsoleColor.Blue);
                }
                else
                {
                    Log("REQ", $"[Thread {Thread.CurrentThread.ManagedThreadId}] Método: {solicitud.Metodo} | Ruta: {solicitud.Ruta} | IP: {solicitud.IPCliente} | Versión HTTP: {solicitud.VersionHTTP}", ConsoleColor.Blue);
                }

                if (solicitud.Metodo == "POST")
                {
                    Log("Body", $"[Thread {Thread.CurrentThread.ManagedThreadId}] Datos POST: {solicitud.Body}", ConsoleColor.Green);
                    Logger.LoguearPost(solicitud.IPCliente, solicitud.Ruta, solicitud.Body);
                }

                RespuestaHTTP respuesta = ProcesarSolicitud(solicitud);

                Logger.LoguearSolicitud(
                    solicitud.IPCliente,
                    solicitud.Metodo,
                    solicitud.Ruta,
                    solicitud.QueryString,
                    respuesta.CodigoEstado
                );

                byte[] respuestaBytes = respuesta.ConstruirRespuesta();
                cliente.Send(respuestaBytes);

                Log("RESP", $"[Thread {Thread.CurrentThread.ManagedThreadId}] Respuesta enviada: {respuesta.CodigoEstado} {respuesta.MensajeEstado}", ConsoleColor.Green);
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode == SocketError.TimedOut)
                    Log("WARN", $"[Thread {Thread.CurrentThread.ManagedThreadId}] Timeout al esperar datos del cliente.", ConsoleColor.Yellow);
                else
                    Log("ERROR", $"[Thread {Thread.CurrentThread.ManagedThreadId}] Error de socket: {ex.SocketErrorCode}", ConsoleColor.Red);
            }
            catch (Exception ex)
            {
                Log("ERROR", $"[Thread {Thread.CurrentThread.ManagedThreadId}] Error: {ex.Message}", ConsoleColor.Red);
            }
            finally
            {
                cliente.Close();
                Log("END", $"[Thread {Thread.CurrentThread.ManagedThreadId}] Cliente desconectado \n", ConsoleColor.DarkGray);
            }
        }

        static RespuestaHTTP ProcesarSolicitud(SolicitudHTTP solicitud)
        {
            RespuestaHTTP respuesta = new RespuestaHTTP();

            if (solicitud.Metodo != "GET" && solicitud.Metodo != "POST")
            {
                respuesta.CodigoEstado = 405;
                respuesta.MensajeEstado = "Method Not Allowed";
                respuesta.Cuerpo = Encoding.UTF8.GetBytes("<h1>405 - Método no permitido</h1>");
                respuesta.Encabezados["Content-Type"] = "text/html; charset=utf-8";
                return respuesta;
            }

            if (solicitud.Metodo == "POST")
            {
                respuesta.Cuerpo = Encoding.UTF8.GetBytes("OK");
                respuesta.Encabezados["Content-Type"] = "text/plain";
                respuesta.Encabezados["Content-Length"] = respuesta.Cuerpo.Length.ToString();
                respuesta.CodigoEstado = 200;
                respuesta.MensajeEstado = "OK";
                return respuesta;
            }

            string ruta = solicitud.Ruta;

            if (ruta == "/" || ruta == "")
            {
                ruta = "/index.html";
            }

            string rutaArchivo = Path.Combine(carpetaArchivos, ruta.TrimStart('/'));

            if (File.Exists(rutaArchivo))
            {
                byte[] contenido = File.ReadAllBytes(rutaArchivo);
                int tamañoOriginal = contenido.Length;

                bool usarCompresion = Compresor.ClienteSoportaGzip(solicitud);

                if (usarCompresion)
                {
                    byte[] contenidoComprimido = Compresor.ComprimirGzip(contenido);
                    respuesta.Cuerpo = contenidoComprimido;
                    respuesta.Encabezados["Content-Encoding"] = "gzip";

                    Log("RESP", $"[Thread {Thread.CurrentThread.ManagedThreadId}] Archivo comprimido: {tamañoOriginal} → {contenidoComprimido.Length} bytes ({(1 - (double)contenidoComprimido.Length / tamañoOriginal) * 100:F1}% reducción)", ConsoleColor.Green);
                }
                else
                {

                    respuesta.Cuerpo = contenido;
                    Log("ERROR", $"[Thread {Thread.CurrentThread.ManagedThreadId}] Cliente no soporta compresión, enviando sin comprimir", ConsoleColor.Red);

                }

                string extension = Path.GetExtension(rutaArchivo).ToLower();
                respuesta.Encabezados["Content-Type"] = Utilidades.ObtenerContentType(extension);
                respuesta.Encabezados["Content-Length"] = respuesta.Cuerpo.Length.ToString();

                respuesta.CodigoEstado = 200;
                respuesta.MensajeEstado = "OK";
            }
            else
            {
                respuesta.CodigoEstado = 404;
                respuesta.MensajeEstado = "Not Found";

                string ruta404 = Path.Combine(carpetaArchivos, "404.html");

                if (File.Exists(ruta404))
                {
                    byte[] contenido404 = File.ReadAllBytes(ruta404);

                    if (Compresor.ClienteSoportaGzip(solicitud))
                    {
                        respuesta.Cuerpo = Compresor.ComprimirGzip(contenido404);
                        respuesta.Encabezados["Content-Encoding"] = "gzip";
                    }
                    else
                    {
                        respuesta.Cuerpo = contenido404;
                    }
                }
                else
                {
                    respuesta.Cuerpo = Encoding.UTF8.GetBytes("<h1>404 - Archivo no encontrado</h1>");
                }

                respuesta.Encabezados["Content-Type"] = "text/html; charset=utf-8";
                respuesta.Encabezados["Content-Length"] = respuesta.Cuerpo.Length.ToString();
            }

            return respuesta;
        }        

        static SolicitudHTTP ParsearSolicitud(string solicitudRaw)
        {
            var solicitud = new SolicitudHTTP();

            string[] lineas = solicitudRaw.Split(new[] { "\r\n" }, StringSplitOptions.None);

            if (lineas.Length > 0)
            {
                string[] primeraLinea = lineas[0].Split(' ');

                if (primeraLinea.Length >= 3)
                {
                    solicitud.Metodo = primeraLinea[0]; 
                    solicitud.VersionHTTP = primeraLinea[2]; 

                    string rutaCompleta = primeraLinea[1];
                    int indiceQuery = rutaCompleta.IndexOf('?');

                    if (indiceQuery >= 0)
                    {
                        solicitud.Ruta = rutaCompleta.Substring(0, indiceQuery);
                        solicitud.QueryString = rutaCompleta.Substring(indiceQuery + 1);
                    }
                    else
                    {
                        solicitud.Ruta = rutaCompleta;
                        solicitud.QueryString = "";
                    }
                }
                

                int i = 1;
                for (; i < lineas.Length; i++)
                {
                    if (string.IsNullOrEmpty(lineas[i]))
                    {
                        i++;
                        break;
                    }

                    int separador = lineas[i].IndexOf(':');
                    if (separador > 0)
                    {
                        string nombreHeader = lineas[i].Substring(0, separador).Trim();
                        string valorHeader = lineas[i].Substring(separador + 1).Trim();
                        solicitud.Headers[nombreHeader] = valorHeader;
                    }
                }


                if (i < lineas.Length)
                {
                    solicitud.Body = string.Join("\r\n", lineas, i, lineas.Length - i);
                }
            }

            return solicitud;
        }
        private static void Log(string tipo, string mensaje, ConsoleColor color = ConsoleColor.Gray)
        {
            lock (ConsolaLock)
            {
                Console.ForegroundColor = color;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [{tipo}] {mensaje}");
                Console.ResetColor();
            }
        }
    }

    }
