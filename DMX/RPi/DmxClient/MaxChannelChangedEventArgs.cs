namespace DmxClient;

public class MaxChannelChangedEventArgs(int channelId) : EventArgs
{
    public int ChannelId => channelId;

    public static implicit operator MaxChannelChangedEventArgs(int channelId) => new(channelId);

    public static implicit operator int(MaxChannelChangedEventArgs args) => args.ChannelId;
}