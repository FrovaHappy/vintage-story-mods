namespace Comerciantes;

/// <summary>
/// Contrato mínimo que las ventanas de cliente requieren del ModSystem para solicitar acciones de red y directorio.
/// </summary>
public interface ITraderClientGateway
{
    void OpenDirectoryFromClient();
    void RequestInspectTrader(long entityId);
    void RequestSellQuotes(long entityId);
    void SendTradeRequest(TradeRequestPacket packet);
    void SendCloseTradeSession(long entityId);
}
