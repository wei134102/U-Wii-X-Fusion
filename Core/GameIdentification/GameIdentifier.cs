using System.Collections.Generic;
using U_Wii_X_Fusion.Core.Interfaces;
using U_Wii_X_Fusion.Core.Models;

namespace U_Wii_X_Fusion.Core.GameIdentification
{
    public class GameIdentifier
    {
        private readonly List<IGameIdentifier> _identifiers;

        public GameIdentifier()
        {
            _identifiers = new List<IGameIdentifier>
            {
                new WiiGameIdentifier(),
                new WiiUGameIdentifier(),
                new Xbox360GameIdentifier()
            };
        }

        public GameInfo IdentifyGame(string filePath)
        {
            foreach (var identifier in _identifiers)
            {
                if (identifier.IsSupportedFormat(filePath))
                {
                    return identifier.IdentifyGame(filePath);
                }
            }
            return null;
        }

        public IGameIdentifier GetIdentifierForPlatform(string platform)
        {
            return _identifiers.Find(id => id.GetPlatform() == platform);
        }

        public List<IGameIdentifier> GetAllIdentifiers()
        {
            return _identifiers;
        }
    }
}
