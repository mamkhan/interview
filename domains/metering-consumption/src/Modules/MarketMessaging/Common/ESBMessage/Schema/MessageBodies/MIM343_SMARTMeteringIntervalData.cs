using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace MeteringConsumption.MarketMessaging.Common.ESBMessage.Schema.MessageBodies;

[SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
        Justification = "Mirrors the ESB XSD attribute name; ESBMessageParser binds by property name.")]
public class MIM343_SMARTMeteringIntervalData
{
    public MessageTrailer? MessageTrailer { get; set; }

    public IReadOnlyList<MPRNLevelInfo>? MPRNLevelInfos { get; set; }
}
