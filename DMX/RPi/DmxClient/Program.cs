using DmxClient;
using Microsoft.Extensions.Logging;
using System.IO.Ports;

#region Logging

var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Warning;
}).SetMinimumLevel(LogLevel.Information));

var logger = loggerFactory.CreateLogger("Program");

#endregion

List<string> paths = [];
Dictionary<int, int> channels = new();
string? ttyPort = null;
bool setBrightness = false;

#region Argument validation

foreach (var arg in args)
{
    if (string.Equals(arg, "b", StringComparison.CurrentCultureIgnoreCase))
    {
        // Brightness instead of contrast - only one letter, let's treat it specially
        setBrightness = true;
        continue;
    }

    if (arg.Length <= 2)
    {
        logger.LogCritical("An argument is not well defined! Argument: {arg}", arg);
        return;
    }

    switch (arg[..2])
    {
        case "s:":
            {
                ttyPort = arg[2..];
                if (string.IsNullOrWhiteSpace(ttyPort))
                {
                    logger.LogCritical("The serial port is not correct!");
                    return;
                }

                break;
            }

        case "c:":
            {
                var channelText = arg[2..];
                if (string.IsNullOrWhiteSpace(channelText) || !int.TryParse(channelText, out int channel) || channel <= 0)
                {
                    logger.LogCritical("A channel definition is not correct! Channel definition: {channel}", channelText);
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
                    logger.LogCritical("A path definition is not correct! Path definition: {path}", pathText);
                    return;
                }

                paths.Add(pathText);
                break;
            }

        default:
        {
            logger.LogWarning("Invalid argument: {arg}", arg);
            break;
        }
    }
}

if (string.IsNullOrWhiteSpace(ttyPort))
{
    logger.LogCritical("The serial port is not defined!");

    logger.LogInformation("The following ports are open:");
    foreach (var portName in SerialPort.GetPortNames())
    {
        logger.LogInformation(" - {portName}", portName);
    }
    return;
}

if (channels.Count == 0)
{
    logger.LogCritical("No channels defined!");
    return;
}

if (paths.Count is < 1 or > 10)
{
    logger.LogCritical("At least one and at most 10 files are supported to play at a time!");
    return;
}

Dictionary<int, string> playPaths = new(paths.Count);
for (int i = 0; i < paths.Count; i++)
{
    var arg = paths[i];
    if (!File.Exists(arg))
    {
        logger.LogCritical("File '{file}' does not exist!", arg);
        return;
    }
    playPaths.Add(channels.ElementAt(i).Key, arg);
}

logger.LogInformation("Parsed arguments correctly.");

#endregion

#region Initialize VLC

using var mediaPlayer = new VlcPlayer(playPaths, setBrightness, loggerFactory.CreateLogger<VlcPlayer>());
mediaPlayer.Play(channels.Keys.First());
mediaPlayer.SetValue(0);

#endregion

#region Serial port communication initialization

CancellationTokenSource cts = new();

await using SerialPortClient client = new(ttyPort, loggerFactory.CreateLogger<SerialPortClient>());

client.MaxChannelChanged += MaxChannelChanged;
client.ValueChanged += ValueChanged;

#endregion

#region Main operation

var result = client.Start(channels, cts.Token);

if (!result) return;

Console.ReadLine();

await cts.CancelAsync();

return;

#endregion

#region Local functions

void MaxChannelChanged(object? sender, MaxChannelChangedEventArgs e)
{
    try
    {
        // ReSharper disable once AccessToDisposedClosure
        mediaPlayer.Play(e.ChannelId);
    }
    catch (ObjectDisposedException)
    {
        logger.LogError("Attempted to play while player is already disposed!");
    }
}

void ValueChanged(object? sender, ChannelValueChangedEventArgs e)
{
    try
    {
        // ReSharper disable once AccessToDisposedClosure
        mediaPlayer.SetValue(e.ChannelValue);
    }
    catch (ObjectDisposedException)
    {
        logger.LogError("Attempted to change value while player is already disposed!");
    }
}

#endregion