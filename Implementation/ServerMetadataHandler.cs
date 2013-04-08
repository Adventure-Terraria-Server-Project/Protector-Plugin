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
    #region [Property: WorkQueue]
    private readonly AsyncWorkQueue workQueue;

    protected AsyncWorkQueue WorkQueue {
      get { return this.workQueue; }
    }
    #endregion

    private readonly object workQueueLock = new object();


    #region [Methods: Constructor, EnsureDataStructure]
    public ServerMetadataHandler(string sqliteFilePath): base(sqliteFilePath) {
      this.workQueue = new AsyncWorkQueue();
    }

    public override void EnsureDataStructure() {      
      SqlTableCreator tableCreator = new SqlTableCreator(this.DbConnection, this.GetQueryBuilder());
      tableCreator.EnsureExists(new SqlTable(
        "Protector_BankChests",
        new SqlColumn("UserId", MySqlDbType.Int32),
        new SqlColumn("ChestIndex", MySqlDbType.Int32),
        new SqlColumn("Content", MySqlDbType.String)
      ));
    }
    #endregion

    #region [Methods: EnqueueGetBankChestCount, EnqueueGetBankChestMetadata]
    public Task<int> EnqueueGetBankChestCount() {
      Contract.Requires<ObjectDisposedException>(!base.IsDisposed);

      return Task<int>.Factory.StartNew(() => {
        Task<object> queueTask;
        lock (this.workQueueLock) {
          queueTask = this.WorkQueue.EnqueueTask((state) => {
            return this.GetBankChestCount();
          });
        }

        return (int)queueTask.Result;
      });
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

      return Task<BankChestMetadata>.Factory.StartNew(() => {
        Task<object> queueTask;
        lock (this.workQueueLock) {
          queueTask = this.WorkQueue.EnqueueTask((state) => {
            return this.GetBankChestMetadata((BankChestDataKey)state);
          }, key);
        }
        return (BankChestMetadata)queueTask.Result;
      });
    }

    private BankChestMetadata GetBankChestMetadata(BankChestDataKey key) {
      using (QueryResult reader = this.DbConnection.QueryReader(
        "SELECT Content FROM Protector_BankChests WHERE UserId = @0 AND ChestIndex = @1;",
        key.UserId, key.BankChestIndex
      )) {
        if (!reader.Read())
          return null;

        return new BankChestMetadata {
          Items = this.StringToItemMetadata(reader.Get<string>("Content"))
        };
      };
    }
    #endregion

    #region [Methods: EnqueueAddOrUpdateBankChest, EnqueueUpdateBankChestItem, EnqueueDeleteBankChestsOfUser]
    public Task EnqueueAddOrUpdateBankChest(BankChestDataKey key, BankChestMetadata bankChest) {
      Contract.Requires<ObjectDisposedException>(!this.IsDisposed);

      lock (this.workQueueLock) {
        return this.WorkQueue.EnqueueTask((state) => {
          this.AddOrUpdateBankChest(key, bankChest);
          return null;
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

    public Task EnqueueUpdateBankChestItem(BankChestDataKey key, int slotIndex, ItemMetadata newItem) {
      Contract.Requires<ObjectDisposedException>(!this.IsDisposed);

      lock (this.workQueueLock) {
        return this.WorkQueue.EnqueueTask((state) => {
          this.UpdateBankChestItem(key, slotIndex, newItem);
          return null;
        });
      }
    }

    private void UpdateBankChestItem(BankChestDataKey key, int slotIndex, ItemMetadata newItem) {
      BankChestMetadata bankChest = this.GetBankChestMetadata(key);
      if (bankChest == null)
        throw new ArgumentException("No bank chest with the given key found.", "key");

      bankChest.Items[slotIndex] = newItem;
      this.AddOrUpdateBankChest(key, bankChest);
    }

    public Task EnqueueDeleteBankChestsOfUser(int userId) {
      Contract.Requires<ObjectDisposedException>(!this.IsDisposed);

      lock (this.workQueueLock) {
        return this.WorkQueue.EnqueueTask((state) => {
          this.EnqueueDeleteBankChestsOfUser(userId);
          return null;
        });
      }
    }

    private void DeleteBankChestsOfUser(int userId) {
      this.DbConnection.Query("DELETE FROM Protector_BankChests WHERE UserId = @0", userId);
    }
    #endregion

    #region [Methods: StringToItemMetadata, ItemMetadataToString]
    protected ItemMetadata[] StringToItemMetadata(string raw) {
      string[] itemsRaw = raw.Split(';');
      ItemMetadata[] items = new ItemMetadata[itemsRaw.Length];
      for (int i = 0; i < itemsRaw.Length; i++) {
        string[] itemDataRaw = itemsRaw[i].Split(',');
        items[i] = new ItemMetadata(
          (ItemPrefix)int.Parse(itemDataRaw[0]),
          (ItemType)int.Parse(itemDataRaw[1]),
          int.Parse(itemDataRaw[2])
        );
      }

      return items;
    }

    protected string ItemMetadataToString(IEnumerable<ItemMetadata> items) {
      StringBuilder builder = new StringBuilder();
      foreach (ItemMetadata item in items) {
        if (builder.Length > 0)
          builder.Append(';');

        builder.Append((byte)item.Prefix);
        builder.Append(',');
        builder.Append((short)item.Type);
        builder.Append(',');
        builder.Append(item.StackSize);
      }

      return builder.ToString();
    }
    #endregion

    #region [IDisposable Implementation]
    protected override void Dispose(bool isDisposing) {
      if (base.IsDisposed)
        return;
    
      if (isDisposing) {
        if (this.workQueue != null) {
          lock (this.workQueueLock) {
            this.workQueue.Dispose();
          }
        }
      }

      base.Dispose(isDisposing);
    }
    #endregion
  }
}
