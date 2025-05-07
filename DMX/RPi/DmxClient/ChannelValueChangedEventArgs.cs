namespace DmxClient;

public class ChannelValueChangedEventArgs(int channelValue) : EventArgs
{
    public int ChannelValue => channelValue;

    public static implicit operator ChannelValueChangedEventArgs(int channelValue) => new(channelValue);

    public static implicit operator int(ChannelValueChangedEventArgs args) => args.ChannelValue;
}