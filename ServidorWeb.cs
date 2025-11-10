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

            /*
             Bind = "Reservar/Asociar" Le dice al sistema operativo:
            "Este socket va a usar el puerto 9000 en todas las interfaces. Reservámelo y envíame todo el tráfico que llegue ahí."
             */
            socketEscucha.Bind(endPoint);

            /*
             Listen = "Ponerlo en modo escucha"
            El `10` es el **backlog** (cola de espera):
            - Es el **número máximo de conexiones pendientes** esperando ser aceptadas
            - Si llegan 15 clientes simultáneamente y tu servidor está ocupado con `Accept()`:
            - Los primeros 10 esperan en la cola
            - Los últimos 5 podrían ser rechazados
            ¿Por qué 10? Es un valor estándar conservador:
            - 10 es suficiente para la mayoría de aplicaciones pequeñas
            - Podrías poner 100 o 1000 si esperas muchísimo tráfico simultáneo
            - En la práctica, tu servidor acepta conexiones tan rápido (con threads) que raramente se llena
             */
            socketEscucha.Listen(10);

            Console.WriteLine($"Servidor escuchando en el puerto {config.Puerto}...");
            Console.WriteLine("Presiona Ctrl+C para detener el servidor");

            // Bucle infinito para seguir aceptando conexiones            
            while (true)
            {
                // Aceptar conexión (bloquea hasta que llegue un cliente)
                Socket socketCliente = socketEscucha.Accept();

                Log("ACCEPT", $"[Thread {Thread.CurrentThread.ManagedThreadId}] Cliente conectado", ConsoleColor.Yellow);

                // Crear un thread para atender este cliente
                Thread hiloCliente = new Thread(() => AtenderCliente(socketCliente));
                hiloCliente.Start();
            }

        }        

        static void AtenderCliente(Socket cliente)
        {
            Log("THREAD", $"[Thread {Thread.CurrentThread.ManagedThreadId}] Atendiendo cliente...", ConsoleColor.Gray);

            try
            {
                // Configurar timeout de recepción (5 segundos)
                cliente.ReceiveTimeout = 5000;

                // Obtener IP del cliente
                IPEndPoint remoteEndPoint = cliente.RemoteEndPoint as IPEndPoint;
                string ipCliente = remoteEndPoint?.Address.ToString() ?? "Desconocida";
                Console.WriteLine(remoteEndPoint);
                Console.WriteLine(ipCliente);

                // Buffer para recibir datos
                //byte[] buffer = new byte[4096]; anteriormente solo para GET
                byte[] buffer = new byte[8192];  // Aumentamos el buffer para POST

                // Recibir datos del cliente
                int bytesRecibidos = cliente.Receive(buffer);

                if (bytesRecibidos == 0)
                {
                    Log("WARN", $"[Thread {Thread.CurrentThread.ManagedThreadId}] Cliente {ipCliente} no envió datos, cerrando conexión...", ConsoleColor.Yellow);
                    return;
                }

                // Convertir los bytes a string
                string solicitudHTTP = Encoding.UTF8.GetString(buffer, 0, bytesRecibidos);

                SolicitudHTTP solicitud = ParsearSolicitud(solicitudHTTP);
                solicitud.IPCliente = ipCliente;

                // Loguear query string si existe (Requisito 7)
                if (!string.IsNullOrEmpty(solicitud.QueryString))
                {
                    string salidaFormateadaQueryString = solicitud.QueryString.Replace("&", ", ");
                    Log("REQ", $"[Thread {Thread.CurrentThread.ManagedThreadId}] Método: {solicitud.Metodo} | Ruta: {solicitud.Ruta} | IP: {solicitud.IPCliente} | Versión HTTP: {solicitud.VersionHTTP} | ParametrosQuery: {salidaFormateadaQueryString}", ConsoleColor.Blue);
                }
                else
                {
                    Log("REQ", $"[Thread {Thread.CurrentThread.ManagedThreadId}] Método: {solicitud.Metodo} | Ruta: {solicitud.Ruta} | IP: {solicitud.IPCliente} | Versión HTTP: {solicitud.VersionHTTP}", ConsoleColor.Blue);
                }

                // Si es POST, loguear el body
                if (solicitud.Metodo == "POST")
                {
                    //Console.WriteLine($"Datos POST: {solicitud.Body}");
                    Log("Body", $"[Thread {Thread.CurrentThread.ManagedThreadId}] Datos POST: {solicitud.Body}", ConsoleColor.Green);
                    Logger.LoguearPost(solicitud.IPCliente, solicitud.Ruta, solicitud.Body);
                }

                // Procesar la solicitud y generar respuesta
                RespuestaHTTP respuesta = ProcesarSolicitud(solicitud);

                Logger.LoguearSolicitud(
                    solicitud.IPCliente,
                    solicitud.Metodo,
                    solicitud.Ruta,
                    solicitud.QueryString,
                    respuesta.CodigoEstado
                );

                // Enviar respuesta al cliente
                byte[] respuestaBytes = respuesta.ConstruirRespuesta();
                cliente.Send(respuestaBytes);

                //Console.WriteLine($"Respuesta enviada: {respuesta.CodigoEstado} {respuesta.MensajeEstado}");
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

            // Aceptamos GET y POST
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

            // Procesamiento de GET
            string ruta = solicitud.Ruta;

            if (ruta == "/" || ruta == "")
            {
                ruta = "/index.html";
            }

            // Construir ruta completa al archivo
            string rutaArchivo = Path.Combine(carpetaArchivos, ruta.TrimStart('/'));

            // Verificar si el archivo existe
            if (File.Exists(rutaArchivo))
            {
                // Leer el archivo
                byte[] contenido = File.ReadAllBytes(rutaArchivo);
                int tamañoOriginal = contenido.Length;

                // Verificar si el cliente soporta compresión
                bool usarCompresion = Compresor.ClienteSoportaGzip(solicitud);

                if (usarCompresion)
                {
                    // Comprimir el contenido
                    byte[] contenidoComprimido = Compresor.ComprimirGzip(contenido);
                    respuesta.Cuerpo = contenidoComprimido;
                    respuesta.Encabezados["Content-Encoding"] = "gzip";

                    Log("RESP", $"[Thread {Thread.CurrentThread.ManagedThreadId}] Archivo comprimido: {tamañoOriginal} → {contenidoComprimido.Length} bytes ({(1 - (double)contenidoComprimido.Length / tamañoOriginal) * 100:F1}% reducción)", ConsoleColor.Green);
                    //Console.WriteLine($"Archivo comprimido: {tamañoOriginal} bytes -> {contenidoComprimido.Length} bytes ({(1 - (double)contenidoComprimido.Length / tamañoOriginal) * 100:F1}% reducción)");
                }
                else
                {
                    // Sin compresión
                    respuesta.Cuerpo = contenido;
                    Log("ERROR", $"[Thread {Thread.CurrentThread.ManagedThreadId}] Cliente no soporta compresión, enviando sin comprimir", ConsoleColor.Red);

                    //Console.WriteLine("Cliente no soporta compresión, enviando sin comprimir");
                }

                // Determinar Content-Type según la extensión
                string extension = Path.GetExtension(rutaArchivo).ToLower();
                respuesta.Encabezados["Content-Type"] = Utilidades.ObtenerContentType(extension);
                respuesta.Encabezados["Content-Length"] = respuesta.Cuerpo.Length.ToString();

                respuesta.CodigoEstado = 200;
                respuesta.MensajeEstado = "OK";
            }
            else
            {
                // Requisito 5: Archivo no encontrado - Error 404
                respuesta.CodigoEstado = 404;
                respuesta.MensajeEstado = "Not Found";

                // Leer el archivo 404.html personalizado
                string ruta404 = Path.Combine(carpetaArchivos, "404.html");

                if (File.Exists(ruta404))
                {
                    byte[] contenido404 = File.ReadAllBytes(ruta404);

                    // También comprimir la página 404 si el cliente lo soporta
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
                    // Si por alguna razón no existe 404.html, usar un mensaje simple
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

            // Dividir la solicitud en líneas
            string[] lineas = solicitudRaw.Split(new[] { "\r\n" }, StringSplitOptions.None);

            if (lineas.Length > 0)
            {
                // Primera línea: GET /ruta?params HTTP/1.1
                string[] primeraLinea = lineas[0].Split(' ');

                if (primeraLinea.Length >= 3)
                {
                    solicitud.Metodo = primeraLinea[0]; // GET, POST, etc.
                    solicitud.VersionHTTP = primeraLinea[2]; // HTTP/1.1

                    // Separar ruta y query string
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

                // Parsear headers (desde línea 1 hasta la línea vacía)
                int i = 1;
                for (; i < lineas.Length; i++)
                {
                    if (string.IsNullOrEmpty(lineas[i]))
                    {
                        // Línea vacía indica fin de headers
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

                // El resto es el body (si existe)
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
