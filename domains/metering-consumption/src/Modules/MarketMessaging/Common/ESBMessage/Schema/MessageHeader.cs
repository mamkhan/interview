using System;
using System.Collections.Generic;
using System.Text;

namespace MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema;

public class MessageHeader
{
    public DateTimeOffset? MarketTimestamp { get; set; }

    public string? MessageTypeCode { get; set; }

    public string? RecipientID { get; set; }

    public string? SenderID { get; set; }

    public string? TxRefNbr { get; set; }

    public string? VersionNumber { get; set; }
}
