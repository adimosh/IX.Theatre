using System.IO.Ports;

namespace DmxClient;

internal class SerialPortClient : IAsyncDisposable
{
    private readonly SerialPort _serialPort;

    private Dictionary<int, int>? _channels;
    private int _isDisposed;
    private Task? _runningTask;
    private int? _maxByPort;
    private int _previousValue;

    public SerialPortClient(string ttyPort)
    {
        if (string.IsNullOrWhiteSpace(ttyPort)) throw new ArgumentNullException(nameof(ttyPort));
        
        using var port = new SerialPort(ttyPort, 9600, Parity.None, 8, StopBits.One);
        port.RtsEnable = true;
        port.Handshake = Handshake.None;

        _serialPort = port;

        _isDisposed = 0;
    }
    
    public EventHandler<MaxChannelChangedEventArgs>? MaxChannelChanged;
    public EventHandler<ChannelValueChangedEventArgs>? ValueChanged;

    public async ValueTask<bool> Start(Dictionary<int, int> channels, Func<SerialPortOperationResult, string?, ValueTask> onError, CancellationToken cancellationToken)
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
            await onError(SerialPortOperationResult.CannotOpenPort, e.Message);
            return false;
        }

        WriteMessage("Start");
        if (ReadMessage() != "Go start")
        {
            await onError(SerialPortOperationResult.StartProtocolInvalid, null);
            return false;
        }

        foreach (var channel in channels.Keys)
        {
            WriteMessage(channel.ToString());
            if (ReadMessage() != "Channel OK")
            {
                await onError(SerialPortOperationResult.ChannelProtocolInvalid, null);
                return false;
            }
        }

        WriteMessage("Channel complete");
        if (ReadMessage() != "Channel complete OK")
        {
            await onError(SerialPortOperationResult.ChannelProtocolInvalid, null);
            return false;
        }

        foreach (var channel in channels.Keys)
        {
            channels[channel] = 0;
        }

        _channels = channels;

        _runningTask = Task.Run(() => InternalLoop(onError, cancellationToken), cancellationToken);

        return true;
    }

    private async Task InternalLoop(Func<SerialPortOperationResult, string?, ValueTask> onError, CancellationToken token)
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
                    await onError(SerialPortOperationResult.MessageProtocolInvalid, line);
                    continue;
                }

                if (!channels.ContainsKey(channel))
                {
                    await onError(SerialPortOperationResult.ChannelInvalid, line);
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
            await onError(SerialPortOperationResult.PortCommunicationError, e.Message);
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