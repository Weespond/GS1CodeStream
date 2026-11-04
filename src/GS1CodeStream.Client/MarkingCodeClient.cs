using System.Net;
using System.Net.Sockets;
using System.Text;

namespace GS1CodeStream.Client;

/// <summary>
/// Получает коды от TCP-сервера и сохраняет полные сообщения в файл.
/// </summary>
/// <param name="address">Адрес сервера.</param>
/// <param name="port">Порт сервера.</param>
/// <param name="filePath">Путь к файлу с кодами.</param>
/// <param name="reconnectDelay">Задержка между попытками подключения.</param>
public sealed class MarkingCodeClient(IPAddress address, int port, string filePath, TimeSpan reconnectDelay)
{
    /// <summary>
    /// Получает и сохраняет коды до отмены или ошибки файла.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await using var file = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read,
            bufferSize: 4096, useAsync: true);
        await using var writer = new StreamWriter(file, new UTF8Encoding(false));

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using var client = new TcpClient(address.AddressFamily);
                try
                {
                    await client.ConnectAsync(address, port, cancellationToken);
                    Console.WriteLine("Подключено к серверу.");
                    await ReceiveAsync(client.GetStream(), writer, cancellationToken);
                    Console.WriteLine("Сервер отключился.");
                }
                catch (SocketException exception)
                {
                    Console.WriteLine($"Ошибка соединения: {exception.Message}");
                }

                await Task.Delay(reconnectDelay, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            Console.WriteLine("Клиент остановлен.");
        }
    }

    /// <summary>
    /// Читает поток до разрыва и сохраняет только строки с завершающим переводом строки.
    /// </summary>
    private static async Task ReceiveAsync(NetworkStream stream, StreamWriter writer, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true));
        var pending = new StringBuilder();
        char[] buffer = new char[1024];
        while (true)
        {
            int count;
            try
            {
                count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
            }
            catch (IOException exception)
            {
                Console.WriteLine($"Ошибка чтения из соединения: {exception.Message}");
                return;
            }

            if (count == 0)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                if (buffer[i] != '\n')
                {
                    pending.Append(buffer[i]);
                    continue;
                }

                await writer.WriteLineAsync(pending.ToString().AsMemory(), cancellationToken);
                await writer.FlushAsync(cancellationToken);
                pending.Clear();
            }
        }
    }
}
