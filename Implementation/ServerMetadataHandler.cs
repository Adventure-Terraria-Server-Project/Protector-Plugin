using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Text;
using System.Threading.Tasks;

using MySql.Data.MySqlClient;

using Terraria.Plugins.Common;

using TShockAPI.DB;

namespace Terraria.Plugins.CoderCow.Protector {
  public class ServerMetadataHandler: DatabaseHandlerBase {
    private readonly object workQueueLock = new object();

    protected AsyncWorkQueue WorkQueue { get; }


    public ServerMetadataHandler(string sqliteFilePath): base(sqliteFilePath) {
      this.WorkQueue = new AsyncWorkQueue();
    }

    public override void EnsureDataStructure() {      
      SqlTableCreator tableCreator = new SqlTableCreator(this.DbConnection, this.GetQueryBuilder());
      tableCreator.EnsureTableStructure(new SqlTable(
        "Protector_BankChests",
        new SqlColumn("UserId", MySqlDbType.Int32),
        new SqlColumn("ChestIndex", MySqlDbType.Int32),
        new SqlColumn("Content", MySqlDbType.Text)
      ));
    }

    public Task<int> EnqueueGetBankChestCount() {
      Contract.Requires<ObjectDisposedException>(!base.IsDisposed);

      lock (this.workQueueLock) {
        return this.WorkQueue.EnqueueTask(() => {
          return this.GetBankChestCount();
        });
      }
    }

    private int GetBankChestCount() {
      using (QueryResult reader = this.DbConnection.QueryReader(
        "SELECT COUNT(UserId) AS Count FROM Protector_BankChests;"
      )) {
        if (!reader.Read())
          throw new InvalidOperationException("Unexpected data were returned.");

        return reader.Get<int>("Count");
      };
    }

    public Task<BankChestMetadata> EnqueueGetBankChestMetadata(BankChestDataKey key) {
      Contract.Requires<ObjectDisposedException>(!this.IsDisposed);
      Contract.Requires<ArgumentException>(key != BankChestDataKey.Invalid);

      lock (this.workQueueLock) {
        return this.WorkQueue.EnqueueTask((keyLocal) => {
          return this.GetBankChestMetadata(keyLocal);
        }, key);
      }
    }

    private BankChestMetadata GetBankChestMetadata(BankChestDataKey key) {
      using (QueryResult reader = this.DbConnection.QueryReader(
        "SELECT Content FROM Protector_BankChests WHERE UserId = @0 AND ChestIndex = @1;",
        key.UserId, key.BankChestIndex
      )) {
        if (!reader.Read())
          return null;

        ItemData[] itemDataFromDB = this.StringToItemMetadata(reader.Get<string>("Content"));
        ItemData[] itemData = itemDataFromDB;
        // Backward compatibility in case chests can now hold more items than before.
        if (itemDataFromDB.Length < Chest.maxItems) {
          itemData = new ItemData[Chest.maxItems];
          for (int i = 0; i < itemDataFromDB.Length; i++)
            itemData[i] = itemDataFromDB[i];
        }

        return new BankChestMetadata { Items = itemData };
      };
    }

    public Task EnqueueAddOrUpdateBankChest(BankChestDataKey key, BankChestMetadata bankChest) {
      Contract.Requires<ObjectDisposedException>(!this.IsDisposed);

      lock (this.workQueueLock) {
        return this.WorkQueue.EnqueueTask(() => {
          this.AddOrUpdateBankChest(key, bankChest);
        });
      }
    }

    private void AddOrUpdateBankChest(BankChestDataKey key, BankChestMetadata bankChest) {
      bool insertRequired;
      using (QueryResult reader = this.DbConnection.QueryReader(
        "SELECT COUNT(UserId) AS Count FROM Protector_BankChests WHERE UserId = @0 AND ChestIndex = @1;", 
        key.UserId, key.BankChestIndex
      )) {
        if (!reader.Read())
          throw new InvalidOperationException("Unexpected data were returned.");

        insertRequired = (reader.Get<int>("Count") == 0);
      }

      if (insertRequired) {
        this.DbConnection.Query(
          "INSERT INTO Protector_BankChests (UserId, ChestIndex, Content) VALUES (@0, @1, @2);",
          key.UserId, key.BankChestIndex, this.ItemMetadataToString(bankChest.Items)
        );
      } else {
        this.DbConnection.Query(
          "UPDATE Protector_BankChests SET Content = @2 WHERE UserId = @0 AND ChestIndex = @1;",
          key.UserId, key.BankChestIndex, this.ItemMetadataToString(bankChest.Items)
        );
      }
    }

    public Task EnqueueUpdateBankChestItem(BankChestDataKey key, int slotIndex, ItemData newItem) {
      Contract.Requires<ObjectDisposedException>(!this.IsDisposed);

      lock (this.workQueueLock) {
        return this.WorkQueue.EnqueueTask(() => {
          this.UpdateBankChestItem(key, slotIndex, newItem);
        });
      }
    }

    private void UpdateBankChestItem(BankChestDataKey key, int slotIndex, ItemData newItem) {
      BankChestMetadata bankChest = this.GetBankChestMetadata(key);
      if (bankChest == null)
        throw new ArgumentException("No bank chest with the given key found.", "key");

      bankChest.Items[slotIndex] = newItem;
      this.AddOrUpdateBankChest(key, bankChest);
    }

    public Task EnqueueDeleteBankChestsOfUser(int userId) {
      Contract.Requires<ObjectDisposedException>(!this.IsDisposed);

      lock (this.workQueueLock) {
        return this.WorkQueue.EnqueueTask((userIdLocal) => {
          this.EnqueueDeleteBankChestsOfUser(userIdLocal);
        }, userId);
      }
    }

    private void DeleteBankChestsOfUser(int userId) {
      this.DbConnection.Query("DELETE FROM Protector_BankChests WHERE UserId = @0", userId);
    }

    protected ItemData[] StringToItemMetadata(string raw) {
      string[] itemsRaw = raw.Split(';');
      ItemData[] items = new ItemData[itemsRaw.Length];
      for (int i = 0; i < itemsRaw.Length; i++) {
        string[] itemDataRaw = itemsRaw[i].Split(',');
        items[i] = new ItemData(
          int.Parse(itemDataRaw[0]),
          int.Parse(itemDataRaw[1]),
          int.Parse(itemDataRaw[2])
        );
      }

      return items;
    }

    protected string ItemMetadataToString(IEnumerable<ItemData> items) {
      StringBuilder builder = new StringBuilder();
      foreach (ItemData item in items) {
        if (builder.Length > 0)
          builder.Append(';');

        builder.Append(item.Prefix);
        builder.Append(',');
        builder.Append(item.Type);
        builder.Append(',');
        builder.Append(item.StackSize);
      }

      return builder.ToString();
    }

    #region [IDisposable Implementation]
    protected override void Dispose(bool isDisposing) {
      if (base.IsDisposed)
        return;
    
      if (isDisposing) {
        if (this.WorkQueue != null) {
          lock (this.workQueueLock) {
            this.WorkQueue.Dispose();
          }
        }
      }

      base.Dispose(isDisposing);
    }
    #endregion
  }
}
