using Microsoft.Extensions.Logging;
using System.IO.Ports;

namespace DmxClient;

internal class SerialPortClient : IAsyncDisposable
{
    private readonly ILogger<SerialPortClient> _logger;
    private readonly SerialPort _serialPort;

    private Dictionary<int, int>? _channels;
    private int _isDisposed;
    private Task? _runningTask;
    private int _maxByPort = -1;
    private int _previousValue = -1;

    public SerialPortClient(string ttyPort, ILogger<SerialPortClient> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        if (string.IsNullOrWhiteSpace(ttyPort)) throw new ArgumentNullException(nameof(ttyPort));
        
        using var port = new SerialPort(ttyPort, 9600, Parity.None, 8, StopBits.One);
        port.RtsEnable = true;
        port.Handshake = Handshake.None;

        _serialPort = port;

        _isDisposed = 0;
    }
    
    public EventHandler<MaxChannelChangedEventArgs>? MaxChannelChanged;
    public EventHandler<ChannelValueChangedEventArgs>? ValueChanged;

    public async ValueTask<bool> Start(Dictionary<int, int> channels, CancellationToken cancellationToken)
    {
        if (_isDisposed == 1) throw new ObjectDisposedException(nameof(SerialPortClient));
        if (_runningTask is not null) throw new InvalidOperationException("The serial port has already been opened.");
        var port = _serialPort;

        try
        {
            port.Open();
            port.DiscardOutBuffer();
            port.DiscardInBuffer();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not open port!");
            return false;
        }

        WriteMessage("Start");
        if (ReadMessage() != "Go start")
        {
            _logger.LogError("Start comm protocol invalid!");
            return false;
        }

        foreach (var channel in channels.Keys)
        {
            WriteMessage(channel.ToString());
            if (ReadMessage() != "Channel OK")
            {
                _logger.LogError("Channel comm protocol invalid!");
                return false;
            }
        }

        WriteMessage("Channel complete");
        if (ReadMessage() != "Channel complete OK")
        {
            _logger.LogError("Channel comm protocol invalid!");
            return false;
        }

        foreach (var channel in channels.Keys)
        {
            channels[channel] = 0;
        }

        _channels = channels;

        _runningTask = Task.Run(() => InternalLoop(cancellationToken), cancellationToken);

        return true;
    }

    private void InternalLoop(CancellationToken token)
    {
        if (_channels is not { } channels) return;
        try
        {
            while (!token.IsCancellationRequested)
            {
                var line = ReadMessage();
                var lineItems = line.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (lineItems.Length != 2 || !int.TryParse(lineItems[0], out var channel) || !int.TryParse(lineItems[1], out var value))
                {
                    _logger.LogError("Message comm protocol invalid! Line: {line}", line);
                    continue;
                }

                if (!channels.ContainsKey(channel))
                {
                    _logger.LogError("Message channel invalid! Channel ID: {channelId}", channel);
                    continue;
                }

                channels[channel] = value;

                var (channelId, channelValue) = channels.MaxBy(p => p.Value);

                if (channelValue != 0 && Interlocked.Exchange(ref _maxByPort, channelId) != channelId)
                {
                    // We need to make sure that we do not change the channel when we get "0" as the channel value, because we might find ourselves
                    // in the situation in which we get the first channel as maximum, when only doing operations on other channels - and that would
                    // simply be a waste of resources constantly switching between videos
                    MaxChannelChanged?.Invoke(this, _maxByPort);
                }

                if (Interlocked.Exchange(ref _previousValue, channelValue) != channelValue)
                {
                    ValueChanged?.Invoke(this, channelValue);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // There was cancellation - let's exit the loop
            return;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Channel comm protocol invalid!");
        }
    }

    private string ReadMessage()
    {
        return _serialPort.ReadTo(";").Trim();
    }

    private void WriteMessage(string message)
    {
        _serialPort.Write(message);
        _serialPort.Write(";");
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or
    /// resetting unmanaged resources asynchronously.</summary>
    /// <returns>A task that represents the asynchronous dispose operation.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 1) return;

        if (_runningTask is not null) await _runningTask;

        _serialPort.Dispose();
    }
}