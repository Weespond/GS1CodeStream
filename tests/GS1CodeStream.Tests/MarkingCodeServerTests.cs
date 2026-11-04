using System.Net;
using System.Net.Sockets;
using System.Text;
using GS1CodeStream.Server;

namespace GS1CodeStream.Tests;

/// <summary>
/// Проверяет передачу кодов и жизненный цикл TCP-сервера.
/// </summary>
public class MarkingCodeServerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Проверяет отключение клиента, отправившего данные, и приём следующего клиента.
    /// </summary>
    [Fact]
    public async Task IncomingDataClosesConnectionAndServerAcceptsNextClient()
    {
        using var shutdown = new CancellationTokenSource();
        var server = new MarkingCodeServer(IPAddress.Loopback, 0);
        Task serverTask = server.RunAsync(shutdown.Token);
        try
        {
            using (var client = new TcpClient())
            {
                await client.ConnectAsync(server.LocalEndpoint).WaitAsync(Timeout);
                NetworkStream stream = client.GetStream();
                await stream.WriteAsync(new byte[] { 42 }).AsTask().WaitAsync(Timeout);
                Assert.Equal(0, await stream.ReadAsync(new byte[1]).AsTask().WaitAsync(Timeout));
            }

            using var next = new TcpClient();
            await next.ConnectAsync(server.LocalEndpoint).WaitAsync(Timeout);
            using var reader = new StreamReader(next.GetStream(), Encoding.UTF8);
            Assert.Matches(@"\A\(01\)[0-9]{14}\(21\)[A-Z0-9]{12}\z",
                Assert.IsType<string>(await reader.ReadLineAsync().WaitAsync(Timeout)));
        }
        finally
        {
            shutdown.Cancel();
            await serverTask.WaitAsync(Timeout);
        }
    }

    /// <summary>
    /// Проверяет передачу кодов и подключение следующего клиента.
    /// </summary>
    [Fact]
    public async Task SendsCodesAndAcceptsAReplacementClient()
    {
        using var shutdown = new CancellationTokenSource();
        var server = new MarkingCodeServer(IPAddress.Loopback, 0);
        Task serverTask = server.RunAsync(shutdown.Token);

        try
        {
            using (var client = new TcpClient())
            {
                await client.ConnectAsync(server.LocalEndpoint).WaitAsync(Timeout);
                using var reader = new StreamReader(client.GetStream(), Encoding.UTF8);
                for (int i = 0; i < 2; i++)
                {
                    string? line = await reader.ReadLineAsync().WaitAsync(Timeout);
                    Assert.NotNull(line);
                    Assert.Matches(@"\A\(01\)[0-9]{14}\(21\)[A-Z0-9]{12}\z", line);
                }
            }

            using var replacement = new TcpClient();
            await replacement.ConnectAsync(server.LocalEndpoint).WaitAsync(Timeout);
            using var replacementReader = new StreamReader(replacement.GetStream(), Encoding.UTF8);
            string? nextLine = await replacementReader.ReadLineAsync().WaitAsync(Timeout);
            Assert.NotNull(nextLine);
            Assert.Matches(@"\A\(01\)[0-9]{14}\(21\)[A-Z0-9]{12}\z", nextLine);
        }
        finally
        {
            shutdown.Cancel();
            await serverTask.WaitAsync(Timeout);
        }
    }

    /// <summary>
    /// Проверяет остановку во время ожидания подключения.
    /// </summary>
    [Fact]
    public async Task CancellationStopsServerWhileWaitingForClient()
    {
        using var shutdown = new CancellationTokenSource();
        var server = new MarkingCodeServer(IPAddress.Loopback, 0);
        Task serverTask = server.RunAsync(shutdown.Token);

        shutdown.Cancel();

        await serverTask.WaitAsync(Timeout);
    }

    /// <summary>
    /// Проверяет остановку при подключённом клиенте.
    /// </summary>
    [Fact]
    public async Task CancellationStopsServerWithConnectedClient()
    {
        using var shutdown = new CancellationTokenSource();
        var server = new MarkingCodeServer(IPAddress.Loopback, 0);
        Task serverTask = server.RunAsync(shutdown.Token);

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(server.LocalEndpoint).WaitAsync(Timeout);
            using var reader = new StreamReader(client.GetStream(), Encoding.UTF8);
            Assert.NotNull(await reader.ReadLineAsync().WaitAsync(Timeout));

            shutdown.Cancel();
            await serverTask.WaitAsync(Timeout);
            Assert.Null(await reader.ReadLineAsync().WaitAsync(Timeout));
        }
        finally
        {
            shutdown.Cancel();
            await serverTask.WaitAsync(Timeout);
        }
    }
}
