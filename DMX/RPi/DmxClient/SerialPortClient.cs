using System.IO.Ports;

namespace DmxClient;

internal class SerialPortClient : IAsyncDisposable
{
    private readonly SerialPort _serialPort;

    private Dictionary<int, int>? _channels;
    private int _isDisposed;
    private Task? _runningTask;

    private int _maxValue;
    private int? _maxByPort;

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

        var stream = port.BaseStream;

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
            var stream = _serialPort.BaseStream;

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

                var maxChannel = channels.MaxBy(p => p.Value);

                if (maxChannel.Value == 0)
                {
                    _maxByPort = null;
                    _maxValue = 0;

                    MaxChannelChanged?.Invoke(this, 0);
                    ValueChanged?.Invoke(this, 0);
                }
                else
                {
                    if (_maxByPort != maxChannel.Key)
                    {
                        _maxByPort = maxChannel.Key;
                        MaxChannelChanged?.Invoke(this, _maxByPort);
                    }
                    _maxValue = maxChannel.Value;
                    ValueChanged?.Invoke(this, _maxValue);
                }
            }
        }
        catch (OperationCanceledException)
        {
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