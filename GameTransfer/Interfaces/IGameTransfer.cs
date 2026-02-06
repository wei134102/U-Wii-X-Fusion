using System;
using U_Wii_X_Fusion.Core.Models;
using U_Wii_X_Fusion.GameTransfer.Models;

namespace U_Wii_X_Fusion.GameTransfer.Interfaces
{
    public interface IGameTransfer
    {
        event EventHandler<TransferProgress> TransferProgressChanged;
        void TransferGame(GameInfo game, string destination);
        void CancelTransfer();
        bool IsTransferring { get; }
        string GetPlatform();
    }
}
