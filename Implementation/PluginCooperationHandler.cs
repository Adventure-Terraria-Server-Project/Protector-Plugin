using System;
using System.Data;
using System.Diagnostics.Contracts;
using System.IO;
using DPoint = System.Drawing.Point;

using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using OTAPI.Tile;
using Terraria.ID;
using Terraria.Plugins.Common;

using TShockAPI;
using TShockAPI.DB;
using Wolfje.Plugins.SEconomy;
using Wolfje.Plugins.SEconomy.Journal;

namespace Terraria.Plugins.CoderCow.Protector {
  public class PluginCooperationHandler {
    [Flags]
    private enum InfiniteChestsChestFlags {
      PUBLIC = 1,
      REGION = 2,
      REFILL = 4,
      BANK = 8
    }

    private const string SeconomySomeTypeQualifiedName = "Wolfje.Plugins.SEconomy.SEconomy, Wolfje.Plugins.SEconomy";

    public PluginTrace PluginTrace { get; }
    public Configuration Config { get; private set; }
    public ChestManager ChestManager { get; set; }
    public bool IsSeconomyAvailable { get; private set; }


    public PluginCooperationHandler(PluginTrace pluginTrace, Configuration config, ChestManager chestManager) {
      Contract.Requires<ArgumentNullException>(pluginTrace != null);
      Contract.Requires<ArgumentNullException>(config != null);

      this.PluginTrace = pluginTrace;
      this.Config = config;
      this.ChestManager = chestManager;
      
      this.IsSeconomyAvailable = (Type.GetType(SeconomySomeTypeQualifiedName, false) != null);
    }

    #region Infinite Chests
    public void InfiniteChests_ChestDataImport(
      ChestManager chestManager, ProtectionManager protectionManager, out int importedChests, out int protectFailures
    ) {
      Contract.Assert(this.ChestManager != null);

      importedChests = 0;
      protectFailures = 0;

      IDbConnection dbConnection = null;
      try {
        switch (TShock.Config.StorageType.ToLower()) {
          case "mysql":
            string[] host = TShock.Config.MySqlHost.Split(':');
            dbConnection = new MySqlConnection(string.Format(
              "Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
              host[0],
              host.Length == 1 ? "3306" : host[1],
              TShock.Config.MySqlDbName,
              TShock.Config.MySqlUsername,
              TShock.Config.MySqlPassword
            ));

            break;
          case "sqlite":
            string sqliteDatabaseFilePath = Path.Combine(TShock.SavePath, "chests.sqlite");
            if (!File.Exists(sqliteDatabaseFilePath))
              throw new FileNotFoundException("Sqlite database file not found.", sqliteDatabaseFilePath);

            dbConnection = new SqliteConnection($"uri=file://{sqliteDatabaseFilePath},Version=3");

            break;
          default:
            throw new NotImplementedException("Unsupported database.");
        }

        using (QueryResult reader = dbConnection.QueryReader(
          "SELECT X, Y, Account, Flags, Items, RefillTime FROM Chests WHERE WorldID = @0", Main.worldID)
        ) {
          while (reader.Read()) {
            int rawX = reader.Get<int>("X");
            int rawY = reader.Get<int>("Y");
            string rawAccount = reader.Get<string>("Account");
            InfiniteChestsChestFlags rawFlags = (InfiniteChestsChestFlags)reader.Get<int>("Flags");
            string rawItems = reader.Get<string>("Items");
            int refillTime = reader.Get<int>("RefillTime");

            if (!TerrariaUtils.Tiles.IsValidCoord(rawX, rawY))
              continue;

            DPoint chestLocation = new DPoint(rawX, rawY);
            ITile chestTile = TerrariaUtils.Tiles[chestLocation];
            if (!chestTile.active() || (chestTile.type != TileID.Containers && chestTile.type != TileID.Containers2 && chestTile.type != TileID.Dressers)) {
              this.PluginTrace.WriteLineWarning($"Chest data at {chestLocation} could not be imported because no corresponding chest tiles exist in the world.");
              continue;
            }

            // TSPlayer.All = chest will not be protected
            TSPlayer owner = TSPlayer.All;
            if (!string.IsNullOrEmpty(rawAccount)) {
              User tUser = TShock.Users.GetUserByName(rawAccount);
              if (tUser != null) {
                owner = new TSPlayer(0);
                owner.User.ID = tUser.ID;
                owner.User.Name = tUser.Name;
                owner.Group = TShock.Groups.GetGroupByName(tUser.Group);
              } else {
                // The original owner of the chest does not exist anymore, so we just protect it for the server player.
                owner = TSPlayer.Server;
              }
            }

            IChest importedChest;
            try {
              importedChest = this.ChestManager.ChestFromLocation(chestLocation);
              if (importedChest == null)
                importedChest = this.ChestManager.CreateChestData(chestLocation);
            } catch (LimitEnforcementException) {
              this.PluginTrace.WriteLineWarning($"Chest limit of {Main.chest.Length + this.Config.MaxProtectorChests - 1} has been reached!");
              break;
            }
            
            string[] itemData = rawItems.Split(',');
            int[] itemArgs = new int[itemData.Length];
            for (int i = 0; i < itemData.Length; i++)
              itemArgs[i] = int.Parse(itemData[i]);

            for (int i = 0; i < 40; i++) {
              int type = itemArgs[i * 3];
              int stack = itemArgs[i * 3 + 1];
              int prefix = (byte)itemArgs[i * 3 + 2];

              importedChest.Items[i] = new ItemData(prefix, type, stack);
            }
            importedChests++;

            if (owner != TSPlayer.All) {
              try {
                ProtectionEntry protection = protectionManager.CreateProtection(owner, chestLocation, false, false, false);
                protection.IsSharedWithEveryone = (rawFlags & InfiniteChestsChestFlags.PUBLIC) != 0;

                if ((rawFlags & InfiniteChestsChestFlags.REFILL) != 0)
                  chestManager.SetUpRefillChest(owner, chestLocation, TimeSpan.FromSeconds(refillTime));
              } catch (Exception ex) {
                this.PluginTrace.WriteLineWarning($"Failed to create protection or define refill chest at {chestLocation}:\n{ex}");
                protectFailures++;
              }
            }
          }
        }
      } finally {
        dbConnection?.Close();
      }
    }

    public void InfiniteSigns_SignDataImport(
      ProtectionManager protectionManager, 
      out int importedSigns, out int protectFailures
    ) {
      string sqliteDatabaseFilePath = Path.Combine(TShock.SavePath, "signs.sqlite");
      if (!File.Exists(sqliteDatabaseFilePath))
        throw new FileNotFoundException("Sqlite database file not found.", sqliteDatabaseFilePath);

      IDbConnection dbConnection = null;
      try {
        switch (TShock.Config.StorageType.ToLower()) {
          case "mysql":
            string[] host = TShock.Config.MySqlHost.Split(':');
            dbConnection = new MySqlConnection(string.Format(
              "Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
              host[0],
              host.Length == 1 ? "3306" : host[1],
              TShock.Config.MySqlDbName,
              TShock.Config.MySqlUsername,
              TShock.Config.MySqlPassword
            ));

            break;
          case "sqlite":
            dbConnection = new SqliteConnection(
              string.Format("uri=file://{0},Version=3", sqliteDatabaseFilePath)
            );

            break;
          default:
            throw new NotImplementedException("Unsupported database.");
        }

        importedSigns = 0;
        protectFailures = 0;
        using (QueryResult reader = dbConnection.QueryReader(
          "SELECT X, Y, Account, Text FROM Signs WHERE WorldID = @0", Main.worldID)
        ) {
          while (reader.Read()) {
            int rawX = reader.Get<int>("X");
            int rawY = reader.Get<int>("Y");
            string rawAccount = reader.Get<string>("Account");
            string rawText = reader.Get<string>("Text");

            if (!TerrariaUtils.Tiles.IsValidCoord(rawX, rawY))
              continue;

            // TSPlayer.All means that the sign must not be protected at all.
            TSPlayer owner = TSPlayer.All;
            if (!string.IsNullOrEmpty(rawAccount)) {
              User tUser = TShock.Users.GetUserByName(rawAccount);
              if (tUser != null) {
                owner = new TSPlayer(0);
                owner.User.ID = tUser.ID;
                owner.User.Name = tUser.Name;
                owner.Group = TShock.Groups.GetGroupByName(tUser.Group);
              } else {
                // The original owner of the sign does not exist anymore, so we just protect it for the server player.
                owner = TSPlayer.Server;
              }
            }

            DPoint signLocation = new DPoint(rawX, rawY);
            int signIndex = -1;
            for (int i = 0; i < Main.sign.Length; i++) {
              Sign sign = Main.sign[i];
              if (sign == null || sign.x != signLocation.X || sign.y != signLocation.Y)
                continue;

              signIndex = i;
              break;
            }

            if (signIndex == -1) {
              ITile signTile = TerrariaUtils.Tiles[signLocation];
              if (!signTile.active() || (signTile.type != TileID.Signs && signTile.type != TileID.Tombstones)) {
                this.PluginTrace.WriteLineWarning(
                  $"The sign data on the location {signLocation} could not be imported because no corresponding sign does exist in the world.");
                continue;
              }

              for (int i = 0; i < Main.sign.Length; i++) {
                Sign sign = Main.sign[i];
                if (sign == null)
                  continue;

                Main.sign[i] = new Sign() {
                  x = rawX, 
                  y = rawY,
                  text = rawText
                };

                signIndex = i;
                break;
              }
            } else {
              Sign.TextSign(signIndex, rawText);
              importedSigns++;
            }

            if (owner != TSPlayer.All) {
              try {
                protectionManager.CreateProtection(owner, signLocation, true, false, false);
              } catch (Exception ex) {
                this.PluginTrace.WriteLineWarning("Failed to create protection at {0}:\n{1}", signLocation, ex);
                protectFailures++;
              }
            }
          }
        }
      } finally {
        if (dbConnection != null)
          dbConnection.Close();
      }
    }
    #endregion

    #region Seconomy
    public Money Seconomy_GetBalance(string playerName) => this.Seconomy_Instance().GetPlayerBankAccount(playerName).Balance;

    public void Seconomy_TransferToWorld(string playerName, Money amount, string journalMsg, string transactionMsg) {
      SEconomy seconomy = this.Seconomy_Instance();

      IBankAccount account = seconomy.GetPlayerBankAccount(playerName);
      account.TransferTo(seconomy.WorldAccount, amount, BankAccountTransferOptions.AnnounceToSender, journalMsg, transactionMsg);
    }

    public string Seconomy_MoneyName() => this.Seconomy_Instance().Configuration.MoneyConfiguration.MoneyNamePlural;

    private SEconomy Seconomy_Instance() {
      SEconomy seconomy = SEconomyPlugin.Instance;
      if (seconomy == null) {
        var errMsg = "The SEconomy instance was null.";
        this.PluginTrace.WriteLineWarning(errMsg);
        throw new InvalidOperationException(errMsg);
      }

      return seconomy;
    }
    #endregion
  }
}
