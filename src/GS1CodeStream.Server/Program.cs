using System.Net;
using System.Net.Sockets;
using GS1CodeStream.Server;

using var shutdown = new CancellationTokenSource();
ConsoleCancelEventHandler onCancel = (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};
Console.CancelKeyPress += onCancel;

try
{
    var server = new MarkingCodeServer(IPAddress.Loopback, 5000);
    await server.RunAsync(shutdown.Token);
    return 0;
}
catch (SocketException exception)
{
    Console.Error.WriteLine($"Ошибка сервера: {exception.Message}");
    return 1;
}
finally
{
    Console.CancelKeyPress -= onCancel;
}
