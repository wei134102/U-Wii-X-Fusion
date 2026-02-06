using U_Wii_X_Fusion.Database.Interfaces;
using U_Wii_X_Fusion.Database.Local;

namespace U_Wii_X_Fusion.Database
{
    public class DatabaseManager
    {
        private readonly IGameDatabase _localDatabase;
        private readonly WiiGameDatabase _wiiDatabase;

        public DatabaseManager()
        {
            _localDatabase = new LocalGameDatabase();
            _localDatabase.Initialize();

            _wiiDatabase = new WiiGameDatabase();
            _wiiDatabase.Initialize();
        }

        public IGameDatabase GetLocalDatabase()
        {
            return _localDatabase;
        }

        public WiiGameDatabase GetWiiDatabase()
        {
            return _wiiDatabase;
        }

        // 这里可以添加获取在线数据库的方法
        // public IGameDatabase GetOnlineDatabase()
        // {
        //     return _onlineDatabase;
        // }
    }
}
