namespace MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema;

public class Message<TBody> where TBody : class
{
    public MessageHeader? MessageHeader { get; set; }

    public TBody? MessageBody { get; set; }
}
