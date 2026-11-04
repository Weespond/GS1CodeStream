using System.Net;
using GS1CodeStream.Client;

using var shutdown = new CancellationTokenSource();
ConsoleCancelEventHandler onCancel = (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};
Console.CancelKeyPress += onCancel;

try
{
    var client = new MarkingCodeClient(IPAddress.Loopback, 5000, "codes.txt", TimeSpan.FromSeconds(2));
    await client.RunAsync(shutdown.Token);
    return 0;
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine($"Ошибка работы с файлом: {exception.Message}");
    return 1;
}
finally
{
    Console.CancelKeyPress -= onCancel;
}
