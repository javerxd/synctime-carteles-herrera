using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace LetrerosHerreraSYNCTIMEapi.Tests;

// Servidor SMTP minimo, solo en 127.0.0.1, para probar el envio real con MailKit sin salir del equipo.
// Puede aceptar, colgarse sin responder, rechazar al usuario (repitiendo su nombre, como hacen algunos
// servidores) o rechazar al destinatario. Guarda el texto de cada mensaje que acepta.
public sealed class ServidorSmtpDePrueba : IAsyncDisposable
{
    public enum Modo { Acepta, Cuelga, NoDisponible, RechazaUsuario, RechazaDestinatario }

    private readonly TcpListener escucha = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource cierre = new();
    private readonly Task atencion;
    private int conexiones;

    public ServidorSmtpDePrueba(Modo modo = Modo.Acepta)
    {
        ModoActual = modo;
        escucha.Start();
        atencion = AtenderAsync();
    }

    public Modo ModoActual { get; set; }
    public int Puerto => ((IPEndPoint)escucha.LocalEndpoint).Port;
    public int Conexiones => Volatile.Read(ref conexiones);
    public ConcurrentQueue<string> Recibidos { get; } = new();
    public ConcurrentQueue<string> UsuariosRecibidos { get; } = new();

    private async Task AtenderAsync()
    {
        while (!cierre.IsCancellationRequested)
        {
            TcpClient cliente;
            try
            {
                cliente = await escucha.AcceptTcpClientAsync(cierre.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            Interlocked.Increment(ref conexiones);
            _ = Task.Run(async () =>
            {
                using (cliente)
                {
                    try
                    {
                        await ConversarAsync(cliente.GetStream(), ModoActual);
                    }
                    catch (Exception e) when (e is IOException or OperationCanceledException or ObjectDisposedException or SocketException)
                    {
                        // El cliente corto la conexion (por ejemplo, por su tiempo de espera).
                    }
                }
            });
        }
    }

    private async Task ConversarAsync(NetworkStream red, Modo modo)
    {
        if (modo == Modo.Cuelga)
        {
            // Acepta la conexion y no saluda nunca: el cliente debe rendirse por su tiempo de espera.
            await Task.Delay(Timeout.Infinite, cierre.Token);
            return;
        }

        using var lector = new StreamReader(red, Encoding.Latin1, leaveOpen: true);
        await using var escritor = new StreamWriter(red, Encoding.Latin1, leaveOpen: true) { NewLine = "\r\n", AutoFlush = true };

        if (modo == Modo.NoDisponible)
        {
            await escritor.WriteLineAsync("421 4.3.2 Servicio no disponible, intente mas tarde");
            return;
        }

        await escritor.WriteLineAsync("220 smtp.prueba.local ESMTP listo");
        var datos = new StringBuilder();
        var enDatos = false;

        while (await lector.ReadLineAsync(cierre.Token) is { } linea)
        {
            if (enDatos)
            {
                if (linea == ".")
                {
                    enDatos = false;
                    Recibidos.Enqueue(datos.ToString());
                    datos.Clear();
                    await escritor.WriteLineAsync("250 2.0.0 Mensaje aceptado");
                }
                else
                {
                    datos.AppendLine(linea.StartsWith("..", StringComparison.Ordinal) ? linea[1..] : linea);
                }
                continue;
            }

            var comando = linea.Split(' ', 2)[0].ToUpperInvariant();
            switch (comando)
            {
                case "EHLO":
                    await escritor.WriteLineAsync("250-smtp.prueba.local");
                    await escritor.WriteLineAsync("250-8BITMIME");
                    await escritor.WriteLineAsync("250 AUTH PLAIN");
                    break;
                case "HELO":
                    await escritor.WriteLineAsync("250 smtp.prueba.local");
                    break;
                case "AUTH":
                    var partes = linea.Split(' ');
                    var credencial = partes.Length > 2 ? partes[2] : null;
                    if (credencial is null)
                    {
                        await escritor.WriteLineAsync("334 ");
                        credencial = await lector.ReadLineAsync(cierre.Token);
                    }
                    var usuario = Encoding.UTF8.GetString(Convert.FromBase64String(credencial ?? "")).Split('\0').ElementAtOrDefault(1) ?? "";
                    UsuariosRecibidos.Enqueue(usuario);
                    // Algunos servidores repiten el usuario en su respuesta: la API no debe guardarla ni registrarla.
                    await escritor.WriteLineAsync(modo == Modo.RechazaUsuario
                        ? $"535 5.7.8 Usuario {usuario} y clave no aceptados"
                        : "235 2.7.0 Autenticado");
                    break;
                case "MAIL":
                    await escritor.WriteLineAsync("250 2.1.0 Remitente aceptado");
                    break;
                case "RCPT":
                    await escritor.WriteLineAsync(modo == Modo.RechazaDestinatario
                        ? "550 5.1.1 El buzon no existe"
                        : "250 2.1.5 Destinatario aceptado");
                    break;
                case "DATA":
                    enDatos = true;
                    await escritor.WriteLineAsync("354 Termine con un punto en una linea");
                    break;
                case "RSET":
                case "NOOP":
                    await escritor.WriteLineAsync("250 2.0.0 Listo");
                    break;
                case "QUIT":
                    await escritor.WriteLineAsync("221 2.0.0 Adios");
                    return;
                default:
                    await escritor.WriteLineAsync("502 5.5.2 Comando no reconocido");
                    break;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await cierre.CancelAsync();
        escucha.Stop();
        try
        {
            await atencion;
        }
        catch (OperationCanceledException)
        {
        }
        cierre.Dispose();
    }
}
