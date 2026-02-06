using System.Collections.Generic;
using U_Wii_X_Fusion.GameTransfer.Interfaces;
using U_Wii_X_Fusion.GameTransfer.Protocols;

namespace U_Wii_X_Fusion.GameTransfer.Managers
{
    public class GameTransferManager
    {
        private readonly Dictionary<string, IGameTransfer> _transferProtocols;

        public GameTransferManager()
        {
            _transferProtocols = new Dictionary<string, IGameTransfer>
            {
                { "Wii", new WiiTransfer() },
                { "Wii U", new WiiUTransfer() },
                { "Xbox 360", new Xbox360Transfer() }
            };
        }

        public IGameTransfer GetTransferProtocol(string platform)
        {
            if (_transferProtocols.ContainsKey(platform))
            {
                return _transferProtocols[platform];
            }
            return null;
        }

        public List<string> GetSupportedPlatforms()
        {
            return new List<string>(_transferProtocols.Keys);
        }
    }
}
