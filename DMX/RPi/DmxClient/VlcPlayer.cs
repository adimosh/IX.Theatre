using LibVLCSharp.Shared;

namespace DmxClient;

internal class VlcPlayer : IDisposable
{
    private const int ProtectionTimeoutMilliseconds = 100;
    
    private readonly LibVLC _libVlc;
    private readonly MediaPlayer _player;
    private readonly MediaList _media;
    private readonly List<int> _channels;

    private int _isDisposed;
    
    private DateTime _lastUpdate;
    private int _channelToPlay = -1;
    private int _playingChannel = -1;
    private int _hasQueue;

    public VlcPlayer(Dictionary<int, string> channelPaths)
    {
        Core.Initialize();
        List<string> inputParams = new(channelPaths.Count + 1);
        inputParams.AddRange(channelPaths.Values);
        inputParams.Add("--input-repeat=2");
        _libVlc = new(inputParams.ToArray());

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

    public void Play(int channel)
    {
        if (_isDisposed == 1) throw new ObjectDisposedException(nameof(VlcPlayer));

        if (!_channels.Contains(channel)) return;
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
        _player.Media = _media[_channels.IndexOf(_channelToPlay)];
        _lastUpdate = DateTime.Now;
        Interlocked.Exchange(ref _hasQueue, 0);
        if (!_player.IsPlaying)
        {
            _player.Play();
            _player.SetAdjustFloat(VideoAdjustOption.Enable, 1);
        }
    }

    public void SetBrightness(int brightness)
    {
        if (brightness < 0) brightness = 0;
        if (brightness > 255) brightness = 255;

        if (brightness == 0)
        {
            // This is going to be a dark movie - we'd better make it as dark as humanly possible
            _player.SetAdjustFloat(VideoAdjustOption.Contrast, 0f);
            _player.SetAdjustFloat(VideoAdjustOption.Brightness, 0f);
        }
        else
        {
            _player.SetAdjustFloat(VideoAdjustOption.Contrast, brightness / 255f);
            _player.SetAdjustFloat(VideoAdjustOption.Brightness, 1f);
        }
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