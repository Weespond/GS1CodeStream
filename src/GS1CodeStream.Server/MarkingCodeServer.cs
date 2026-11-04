using System.Net;
using System.Net.Sockets;
using System.Text;

namespace GS1CodeStream.Server;

/// <summary>
/// Принимает TCP-клиентов и передаёт им синтетические коды.
/// </summary>
/// <param name="address">Локальный адрес прослушивания.</param>
/// <param name="port">Порт прослушивания; ноль выбирает свободный порт.</param>
public sealed class MarkingCodeServer(IPAddress address, int port)
{
    private readonly TcpListener listener = new(address, port);
    private readonly MarkingCodeGenerator generator = new();

    /// <summary>
    /// Возвращает локальный адрес и порт сервера после запуска.
    /// </summary>
    public IPEndPoint LocalEndpoint => (IPEndPoint)listener.LocalEndpoint;

    /// <summary>
    /// Запускает сервер и обслуживает клиентов до отмены.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        listener.Start();
        try
        {
            Console.WriteLine($"Сервер запущен: {LocalEndpoint}");
            while (!cancellationToken.IsCancellationRequested)
            {
                using TcpClient client = await listener.AcceptTcpClientAsync(cancellationToken);
                Console.WriteLine($"Клиент подключён: {client.Client.RemoteEndPoint}");
                try
                {
                    await HandleClientAsync(client, cancellationToken);
                }
                catch (InvalidDataException exception)
                {
                    Console.WriteLine($"Нарушение протокола: {exception.Message}");
                }
                catch (IOException exception)
                {
                    Console.WriteLine($"Соединение прервано: {exception.Message}");
                }
                catch (SocketException exception)
                {
                    Console.WriteLine($"Соединение прервано: {exception.Message}");
                }
                finally
                {
                    Console.WriteLine("Клиент отключён.");
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Отмена означает обычное завершение работы сервера.
        }
        finally
        {
            listener.Stop();
            Console.WriteLine("Сервер остановлен.");
        }
    }

    /// <summary>
    /// Передаёт коды и отслеживает отключение одного клиента.
    /// </summary>
    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        NetworkStream stream = client.GetStream();
        Task sending = SendCodesAsync(stream, connectionCancellation.Token);
        Task monitoring = MonitorConnectionAsync(stream, connectionCancellation.Token);

        await Task.WhenAny(sending, monitoring);
        connectionCancellation.Cancel();
        try
        {
            await Task.WhenAll(sending, monitoring);
        }
        catch (OperationCanceledException) when (connectionCancellation.IsCancellationRequested)
        {
            // Другая задача завершилась или сервер останавливается.
        }
    }

    /// <summary>
    /// Отправляет коды с интервалом 500 мс до отмены.
    /// </summary>
    private async Task SendCodesAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            byte[] message = Encoding.UTF8.GetBytes(generator.Generate() + "\n");
            await stream.WriteAsync(message, cancellationToken);
        }
    }

    /// <summary>
    /// Ожидает закрытия соединения и отклоняет входящие данные клиента.
    /// </summary>
    private static async Task MonitorConnectionAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[1];
        if (await stream.ReadAsync(buffer, cancellationToken) != 0)
        {
            throw new InvalidDataException("Клиент отправил данные, не предусмотренные протоколом.");
        }
    }
}
