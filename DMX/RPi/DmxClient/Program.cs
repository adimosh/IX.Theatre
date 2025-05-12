using DmxClient.ArgumentValidation;
using DmxClient.SerialPort;
using DmxClient.Vlc;
using Microsoft.Extensions.Logging;

#region Logging

var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Warning;
}).SetMinimumLevel(LogLevel.Information));

var logger = loggerFactory.CreateLogger("Program");

#endregion

string? ttyPort;
bool setBrightness;
Dictionary<int, string>? playPaths;
Dictionary<int, int>? channels;

#region Argument validation

if (!ArgumentParser.TryValidateArguments(args, loggerFactory.CreateLogger("ArgumentParser"), out setBrightness, out ttyPort, out playPaths,
                                         out channels))
{
    return;
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