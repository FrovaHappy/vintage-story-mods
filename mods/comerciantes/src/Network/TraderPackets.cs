using ProtoBuf;

namespace Comerciantes;

[ProtoContract]
public sealed class WantedItemEntry
{
    [ProtoMember(1)] public string ItemCode { get; set; } = string.Empty;
    [ProtoMember(2)] public string DisplayName { get; set; } = string.Empty;
    [ProtoMember(3)] public int MinLotSize { get; set; }
    [ProtoMember(4)] public int LotGearPayout { get; set; }
    [ProtoMember(5)] public double DeltaPct { get; set; }
    [ProtoMember(6)] public double SaturationPct { get; set; }
    [ProtoMember(7)] public byte[]? StackBytes { get; set; }
}

[ProtoContract]
public sealed class StockOfferEntry
{
    [ProtoMember(1)] public int SellingSlotIndex { get; set; }
    [ProtoMember(2)] public string ItemCode { get; set; } = string.Empty;
    [ProtoMember(3)] public string DisplayName { get; set; } = string.Empty;
    [ProtoMember(4)] public int StackSize { get; set; }
    [ProtoMember(5)] public int Stock { get; set; }
    [ProtoMember(6)] public int PriceGears { get; set; }
    [ProtoMember(7)] public byte[]? StackBytes { get; set; }
}

[ProtoContract]
public sealed class SellSlotValuationEntry
{
    [ProtoMember(1)] public int SellSlotIndex { get; set; }
    [ProtoMember(2)] public string ItemCode { get; set; } = string.Empty;
    [ProtoMember(3)] public string DisplayName { get; set; } = string.Empty;
    [ProtoMember(4)] public int QuantityInSlot { get; set; }
    [ProtoMember(5)] public int MinLotSize { get; set; }
    [ProtoMember(6)] public int LotGearPayout { get; set; }
    [ProtoMember(7)] public int ConsumedLots { get; set; }
    [ProtoMember(8)] public int TotalPayoutGears { get; set; }
    [ProtoMember(9)] public double DeltaPct { get; set; }
    [ProtoMember(10)] public bool IsTradeable { get; set; }
    [ProtoMember(11)] public double BaseValue { get; set; }
    [ProtoMember(12)] public double AffinityMod { get; set; }
    [ProtoMember(13)] public double DemandMod { get; set; }
    [ProtoMember(14)] public double SaturationMod { get; set; }
    [ProtoMember(15)] public int ConsumedUnits { get; set; }
    [ProtoMember(16)] public int CurrentLotSize { get; set; }
    [ProtoMember(17)] public bool IsOverstocked { get; set; }
}

[ProtoContract]
public sealed class TraderStateSyncPacket
{
    [ProtoMember(1)] public long EntityId { get; set; }
    [ProtoMember(2)] public string TraderName { get; set; } = string.Empty;
    [ProtoMember(3)] public string Archetype { get; set; } = string.Empty;
    [ProtoMember(4)] public int TraderMoney { get; set; }
    [ProtoMember(5)] public double CurrentTotalDays { get; set; }
    [ProtoMember(6)] public double NextRefreshTotalDays { get; set; }
    [ProtoMember(7)] public List<WantedItemEntry> WantedItems { get; set; } = [];
    [ProtoMember(8)] public List<StockOfferEntry> SellingOffers { get; set; } = [];
    [ProtoMember(9)] public List<SellSlotValuationEntry> SellSlotQuotes { get; set; } = [];
    [ProtoMember(10)] public double TraderRewardBonusPct { get; set; }
    [ProtoMember(12)] public string EconomyModeCode { get; set; } = string.Empty;
}

[ProtoContract]
public sealed class BuyCartLine
{
    [ProtoMember(1)] public int SellingSlotIndex { get; set; }
    [ProtoMember(2)] public int PackCount { get; set; }
}

[ProtoContract]
public sealed class SellSlotQuoteRequestPacket
{
    [ProtoMember(1)] public long EntityId { get; set; }
}

[ProtoContract]
public sealed class SellSlotQuoteResponsePacket
{
    [ProtoMember(1)] public long EntityId { get; set; }
    [ProtoMember(2)] public List<SellSlotValuationEntry> SellSlotQuotes { get; set; } = [];
}

[ProtoContract]
public sealed class OpenTraderRequestPacket
{
    [ProtoMember(1)] public long EntityId { get; set; }
}

[ProtoContract]
public sealed class InspectTraderRequestPacket
{
    [ProtoMember(1)] public long EntityId { get; set; }
}

[ProtoContract]
public sealed class CloseTradeSessionPacket
{
    [ProtoMember(1)] public long EntityId { get; set; }
}

[ProtoContract]
public sealed class TradeRequestPacket
{
    [ProtoMember(1)] public long EntityId { get; set; }
    [ProtoMember(2)] public List<BuyCartLine> BuyCart { get; set; } = [];
}

[ProtoContract]
public sealed class TradeResponsePacket
{
    [ProtoMember(1)] public bool Success { get; set; }
    [ProtoMember(3)] public TraderStateSyncPacket? UpdatedState { get; set; }
    [ProtoMember(4)] public string MessageKey { get; set; } = string.Empty;
    [ProtoMember(5)] public List<string> MessageArgs { get; set; } = [];
}

[ProtoContract]
public sealed class DirectoryOpenRequestPacket
{
}

[ProtoContract]
public sealed class KnownTraderDto
{
    [ProtoMember(1)] public long EntityId { get; set; }
    [ProtoMember(2)] public string TraderName { get; set; } = string.Empty;
    [ProtoMember(3)] public string Archetype { get; set; } = string.Empty;
    [ProtoMember(4)] public int PosX { get; set; }
    [ProtoMember(5)] public int PosY { get; set; }
    [ProtoMember(6)] public int PosZ { get; set; }
    [ProtoMember(7)] public double LastSeenTotalDays { get; set; }
    [ProtoMember(8)] public double NextRefreshTotalDays { get; set; }
    [ProtoMember(9)] public int TraderMoney { get; set; }
    [ProtoMember(10)] public List<WantedItemEntry> WantedItems { get; set; } = [];
    [ProtoMember(11)] public List<StockOfferEntry> SellingOffers { get; set; } = [];
    [ProtoMember(12)] public double TraderRewardBonusPct { get; set; }
}

[ProtoContract]
public sealed class KnownTradersSyncPacket
{
    [ProtoMember(1)] public bool IsLiveSyncMode { get; set; }
    [ProtoMember(2)] public List<KnownTraderDto> Traders { get; set; } = [];
    [ProtoMember(3)] public double CurrentTotalDays { get; set; }
    [ProtoMember(4)] public bool IsInstantTradeEnabled { get; set; }
}

[ProtoContract]
public sealed class ConfigReloadedNotifyPacket
{
}
