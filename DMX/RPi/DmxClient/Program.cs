using DmxClient;
using System.IO.Ports;

// Command line argument validation and data 
List<string> paths = new();
Dictionary<int, int> channels = new();
string? ttyPort = null;
string? darkPath = null;

#region Argument validation

foreach (var arg in args)
{
    if (arg.Length <= 2)
    {
        await Console.Error.WriteAsync($"An argument is not well defined! Argument: {arg}");
        return;
    }

    switch (arg[..2])
    {
        case "s:":
            {
                ttyPort = arg[2..];
                if (string.IsNullOrWhiteSpace(ttyPort))
                {
                    await Console.Error.WriteAsync("The serial port is not correct!");
                    return;
                }

                break;
            }

        case "c:":
            {
                var channelText = arg[2..];
                if (string.IsNullOrWhiteSpace(channelText) || !int.TryParse(channelText, out int channel) || channel <= 0)
                {
                    await Console.Error.WriteAsync($"A channel definition is not correct! Channel definition: {channelText}");
                    return;
                }

                channels.Add(channel, 0);
                break;
            }

        case "p:":
        case "f:":
            {
                var pathText = arg[2..];
                if (string.IsNullOrWhiteSpace(pathText) || pathText.IndexOfAny(Path.GetInvalidPathChars()) != -1)
                {
                    await Console.Error.WriteAsync($"A path definition is not correct! Path definition: {pathText}");
                    return;
                }

                paths.Add(pathText);
                break;
            }

        case "h:":
            {
                var pathText = arg[2..];
                if (string.IsNullOrWhiteSpace(pathText) || pathText.IndexOfAny(Path.GetInvalidPathChars()) != -1)
                {
                    await Console.Error.WriteAsync($"The dark definition is not correct! Path definition: {pathText}");
                    return;
                }

                darkPath = pathText;
                break;
            }
    }
}

if (string.IsNullOrWhiteSpace(ttyPort))
{
    await Console.Error.WriteAsync("The serial port is not defined!");

    Console.WriteLine("The following ports are open:");
    foreach (var portName in SerialPort.GetPortNames())
    {
        Console.WriteLine($" - {portName}");
    }
    return;
}

if (channels.Count == 0)
{
    await Console.Error.WriteAsync("No channels defined!");
    return;
}

if (paths.Count is < 1 or > 10)
{
    await Console.Error.WriteAsync("At least one and at most 10 files are supported to play at a time!");
    return;
}

if (string.IsNullOrWhiteSpace(darkPath))
{
    await Console.Error.WriteAsync("No path defined to play when dark!");
    return;
}

Dictionary<int, string> playPaths = new() { { 0, darkPath } };
for (int i = 0; i < paths.Count; i++)
{
    var arg = paths[i];
    if (!File.Exists(arg))
    {
        await Console.Error.WriteAsync($"File '{arg}' does not exist!");
        return;
    }
    playPaths.Add(channels.ElementAt(i).Key, arg);
}

#endregion

#region Initialize VLC

using var mediaPlayer = new VlcPlayer(playPaths);
mediaPlayer.Play(0);
mediaPlayer.SetBrightness(0);

#endregion

#region Serial port communication initialization

CancellationTokenSource cts = new();

await using SerialPortClient client = new(ttyPort);

client.MaxChannelChanged += MaxChannelChanged;
client.ValueChanged += ValueChanged;

#endregion

#region Main operation

var result = await client.Start(channels, async (operationResult, s) => await ReportErrorToUser(operationResult, s), cts.Token);

if (!result) return;

Console.ReadLine();

await cts.CancelAsync();

return;

#endregion

#region Local functions

async Task<bool> ReportErrorToUser(SerialPortOperationResult serialPortOperationResult, string? errMsg)
{
    switch (serialPortOperationResult)
    {
        case SerialPortOperationResult.CannotOpenPort:
            await Console.Error.WriteAsync($"Could not open port! Message: {errMsg}");
            return true;
        case SerialPortOperationResult.PortCommunicationError:
            await Console.Error.WriteAsync($"Serial port communication! Message: {errMsg}");
            return true;
        case SerialPortOperationResult.StartProtocolInvalid:
            await Console.Error.WriteAsync("Start comm protocol invalid!");
            return true;
        case SerialPortOperationResult.ChannelProtocolInvalid:
            await Console.Error.WriteAsync("Channel comm protocol invalid!");
            return true;
        case SerialPortOperationResult.MessageProtocolInvalid:
            await Console.Error.WriteAsync($"Message comm protocol invalid! Message: {errMsg}");
            return true;
        case SerialPortOperationResult.ChannelInvalid:
            await Console.Error.WriteAsync($"Message channel invalid! Message: {errMsg}");
            return true;
    }

    return false;
}

void MaxChannelChanged(object? sender, MaxChannelChangedEventArgs e)
{
    try
    {
        // ReSharper disable once AccessToDisposedClosure
        mediaPlayer.Play(e.ChannelId);
    }
    catch (ObjectDisposedException)
    {
        Console.Error.WriteLine("Attempted to play while player is already disposed!");
    }
}

void ValueChanged(object? sender, ChannelValueChangedEventArgs e)
{
    try
    {
        // ReSharper disable once AccessToDisposedClosure
        mediaPlayer.SetBrightness(e.ChannelValue);
    }
    catch (ObjectDisposedException)
    {
        Console.Error.WriteLine("Attempted to play while player is already disposed!");
    }
}

#endregion