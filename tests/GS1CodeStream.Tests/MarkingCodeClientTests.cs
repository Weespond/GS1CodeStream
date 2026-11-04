using System.Net;
using System.Net.Sockets;
using System.Text;
using GS1CodeStream.Client;

namespace GS1CodeStream.Tests;

/// <summary>
/// Проверяет получение сообщений, переподключение и завершение клиента.
/// </summary>
public class MarkingCodeClientTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Проверяет разделение потока, добавление в файл и отбрасывание неполной строки.
    /// </summary>
    [Fact]
    public async Task WritesCompleteMessagesAndDiscardsIncompleteMessageAfterReconnect()
    {
        string path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, "existing\n");
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var shutdown = new CancellationTokenSource();
        var client = new MarkingCodeClient(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port,
            path, TimeSpan.FromMilliseconds(50));
        Task clientTask = client.RunAsync(shutdown.Token);
        try
        {
            using (TcpClient first = await listener.AcceptTcpClientAsync().WaitAsync(Timeout))
            {
                NetworkStream stream = first.GetStream();
                await stream.WriteAsync("one\ntwo\npar"u8.ToArray());
                await WaitForContentAsync(path, "existing\none\ntwo\n");
                await stream.WriteAsync("tial"u8.ToArray());
            }

            using (TcpClient second = await listener.AcceptTcpClientAsync().WaitAsync(Timeout))
            {
                NetworkStream stream = second.GetStream();
                await stream.WriteAsync("th"u8.ToArray());
                await Task.Delay(100);
                await stream.WriteAsync("ree\n"u8.ToArray());
                await WaitForContentAsync(path, "existing\none\ntwo\nthree\n");
                shutdown.Cancel();
                await clientTask.WaitAsync(Timeout);
            }
        }
        finally
        {
            shutdown.Cancel();
            listener.Stop();
            await clientTask.WaitAsync(Timeout);
            File.Delete(path);
        }
    }

    /// <summary>
    /// Проверяет повторное подключение после первоначальной недоступности сервера.
    /// </summary>
    [Fact]
    public async Task ConnectsWhenServerBecomesAvailable()
    {
        string path = Path.GetTempFileName();
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        listener = new TcpListener(IPAddress.Loopback, port);
        using var shutdown = new CancellationTokenSource();
        var client = new MarkingCodeClient(IPAddress.Loopback, port, path, TimeSpan.FromMilliseconds(50));
        Task clientTask = client.RunAsync(shutdown.Token);
        try
        {
            await Task.Delay(250);
            listener.Start();
            using TcpClient connection = await listener.AcceptTcpClientAsync().WaitAsync(Timeout);
            await connection.GetStream().WriteAsync("connected\n"u8.ToArray());
            await WaitForContentAsync(path, "connected\n");
        }
        finally
        {
            shutdown.Cancel();
            listener.Stop();
            await clientTask.WaitAsync(Timeout);
            File.Delete(path);
        }
    }

    /// <summary>
    /// Проверяет, что отмена прерывает ожидание перед повторным подключением.
    /// </summary>
    [Fact]
    public async Task CancellationInterruptsReconnectDelay()
    {
        string path = Path.GetTempFileName();
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        using var shutdown = new CancellationTokenSource();
        var client = new MarkingCodeClient(IPAddress.Loopback, port, path, TimeSpan.FromMinutes(1));
        Task clientTask = client.RunAsync(shutdown.Token);
        try
        {
            await Task.Delay(200);
            shutdown.Cancel();
            await clientTask.WaitAsync(Timeout);
        }
        finally
        {
            shutdown.Cancel();
            await clientTask.WaitAsync(Timeout);
            File.Delete(path);
        }
    }

    /// <summary>
    /// Проверяет, что ошибка открытия файла передаётся вызывающему коду.
    /// </summary>
    [Fact]
    public async Task FileOpenFailureStopsClient()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "codes.txt");
        var client = new MarkingCodeClient(IPAddress.Loopback, 5000, path, TimeSpan.FromSeconds(1));
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => client.RunAsync(CancellationToken.None));
    }

    /// <summary>
    /// Ожидает появления ожидаемого содержимого файла в пределах времени теста.
    /// </summary>
    private static async Task WaitForContentAsync(string path, string expected)
    {
        using var timeout = new CancellationTokenSource(Timeout);
        while (true)
        {
            await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(file, Encoding.UTF8);
            string content = (await reader.ReadToEndAsync(timeout.Token)).Replace("\r\n", "\n");
            if (content == expected)
            {
                return;
            }

            await Task.Delay(20, timeout.Token);
        }
    }
}
