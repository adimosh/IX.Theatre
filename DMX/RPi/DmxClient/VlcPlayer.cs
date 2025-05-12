using LibVLCSharp.Shared;
using Microsoft.Extensions.Logging;

namespace DmxClient;

internal class VlcPlayer : IDisposable
{
    private const int ProtectionTimeoutMilliseconds = 100;

    private readonly ILogger<VlcPlayer> _logger;
    private readonly LibVLC _libVlc;
    private readonly MediaPlayer _player;
    private readonly MediaList _media;
    private readonly List<int> _channels;
    private readonly string _valueOptionText;
    private readonly VideoAdjustOption _valueOption;

    private int _isDisposed;
    
    private DateTime _lastUpdate;
    private int _channelToPlay = -1;
    private int _playingChannel = -1;
    private float _previousValue = -1f;
    private int _hasQueue;

    public VlcPlayer(Dictionary<int, string> channelPaths, bool setBrightness, ILogger<VlcPlayer> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(channelPaths);

        _logger = logger;

        try
        {
            Core.Initialize();
            List<string> inputParams = new(channelPaths.Count + 1);
            inputParams.AddRange(channelPaths.Values);
            inputParams.Add("--input-repeat=2");
            _libVlc = new(inputParams.ToArray());
            if (setBrightness)
            {
                _valueOptionText = "brightness";
                _valueOption = VideoAdjustOption.Brightness;
            }
            else
            {
                _valueOptionText = "contrast";
                _valueOption = VideoAdjustOption.Contrast;
            }

            _player = new(_libVlc)
            {
                Fullscreen = true
            };

            _media = new(_libVlc);

            _channels = new(channelPaths.Count);

            foreach (var p in channelPaths)
            {
                _media.AddMedia(new(_libVlc, p.Value));
                _channels.Add(p.Key);
            }
        }
        catch (Exception e)
        {
            logger.LogCritical(e, "Could not initialize VLC player because of an internal exception.");
            throw;
        }
    }

    public void Play(int channel)
    {
        ObjectDisposedException.ThrowIf(_isDisposed == 1, this);

        if (!_channels.Contains(channel))
        {
            _logger.LogWarning("Info for channel {channelId} was received while it is not in the list of configured channels.", channel);
            return;
        }
        if (Interlocked.Exchange(ref _channelToPlay, channel) == channel) return;
        if ((DateTime.Now - _lastUpdate).TotalMilliseconds < ProtectionTimeoutMilliseconds)
        {
            // Flood prevention
            if (Interlocked.Exchange(ref _hasQueue, 1) == 1) return;

            Task.Delay(ProtectionTimeoutMilliseconds).ContinueWith(_ => PlayInternal());
        }
        else
        {
            PlayInternal();
        }
    }

    private void PlayInternal()
    {
        if (Interlocked.Exchange(ref _playingChannel, _channelToPlay) == _channelToPlay) return;
        _logger.LogInformation("Switching playback to channel {channelId}.", _channelToPlay);
        _player.Media = _media[_channels.IndexOf(_channelToPlay)];
        _lastUpdate = DateTime.Now;
        Interlocked.Exchange(ref _hasQueue, 0);
        
        if (_player.IsPlaying) return;

        // If the player is not currently playing, for some reason, let's start it
        _logger.LogInformation("Starting playback.");
        _player.Play();
        _player.SetAdjustFloat(VideoAdjustOption.Enable, 1);
    }

    public void SetValue(int valueToSet)
    {
        // Standard DMX offers values between 0 and 255 - if we get anything outside of this range,
        // let's cap the values
        if (valueToSet < 0)
        {
            _logger.LogWarning("Channel value received {value}, which is below 0.", valueToSet);
            valueToSet = 0;
        }

        if (valueToSet > 255)
        {
            _logger.LogWarning("Channel value received {value}, which is above 255.", valueToSet);
            valueToSet = 255;
        }

        // While both Brightness and Contrast allow from 0f to 2f, let's set it 1-based, as values above 1
        // (especially for brightness) will just wash out the colors without adding too much to the image
        var value = valueToSet / 255f;

        // Let's not bother if there's nothing to adjust
        if (Math.Abs(value - _previousValue) < float.Epsilon) return;
        
        _previousValue = value;
        _logger.LogInformation("Setting channel value {value} as {option}.", valueToSet, _valueOptionText);

        _player.SetAdjustFloat(_valueOption, value);
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or
    /// resetting unmanaged resources asynchronously.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 1) return;
        
        _player.Stop();

        foreach (var media in _media) media.Dispose();
        _media.Dispose();

        _player.Dispose();
        _libVlc.Dispose();
    }
}