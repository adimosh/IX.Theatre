namespace DmxClient;

public enum SerialPortOperationResult
{
    Success,
    
    // Communication errors
    CannotOpenPort,
    PortCommunicationError,
    
    // Protocol errors
    StartProtocolInvalid,
    ChannelProtocolInvalid,
    MessageProtocolInvalid,

    // Channel errors
    ChannelInvalid
}