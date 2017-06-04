using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OTAPI.Tile;
using Terraria.ID;
using Terraria.Plugins.Common;
using TShockAPI;
using TShockAPI.DB;
using DPoint = System.Drawing.Point;

namespace Terraria.Plugins.CoderCow.Protector {
  public class ChestManager {
    // The last chest of the Terraria world chests is used to fake all the other chests stored in Protector's world metadata.
    public static readonly int DummyChestIndex = Main.chest.Length - 1;
    public static readonly Chest DummyChest;

    private Configuration config;

    public PluginTrace PluginTrace { get; }
    public WorldMetadata WorldMetadata { get; }
    public ServerMetadataHandler ServerMetadataHandler { get; }
    public PluginCooperationHandler CooperationHandler { get; }
    public TimerManager RefillTimers { get; }
    public Func<TimerBase,bool> RefillTimerCallbackHandler { get; }

    public Configuration Config {
      get { return this.config; }
      set {
        Contract.Requires<ArgumentNullException>(value != null);
        this.config = value;
      }
    }

    static ChestManager() {
      ChestManager.DummyChest = Main.chest[ChestManager.DummyChestIndex] = new Chest();
      ChestManager.DummyChest.item = new Item[Chest.maxItems];
    }

    public ChestManager(
      PluginTrace pluginTrace, Configuration config, ServerMetadataHandler serverMetadataHandler, WorldMetadata worldMetadata,
      PluginCooperationHandler cooperationHandler
    ) {
      this.PluginTrace = pluginTrace;
      this.config = config;
      this.ServerMetadataHandler = serverMetadataHandler;
      this.WorldMetadata = worldMetadata;
      this.CooperationHandler = cooperationHandler;

      this.RefillTimers = new TimerManager(pluginTrace);
      this.RefillTimerCallbackHandler = this.RefillChestTimer_Callback;
    }

    /// <returns>
    ///   A <c>bool</c> which is <c>false</c> if a refill chest already existed at the given location and thus just its refill 
    ///   time was set or <c>true</c> if a new refill chest was actually defined.
    /// </returns>
    public bool SetUpRefillChest(
      TSPlayer player, DPoint tileLocation, TimeSpan? refillTime, bool? oneLootPerPlayer = null, int? lootLimit = null, 
      bool? autoLock = null, bool? autoEmpty = null, bool fairLoot = false, bool checkPermissions = false
    ) {
      Contract.Requires<ArgumentNullException>(player != null);
      Contract.Requires<ArgumentException>(TerrariaUtils.Tiles[tileLocation] != null, "tileLocation");
      Contract.Requires<ArgumentException>(TerrariaUtils.Tiles[tileLocation].active(), "tileLocation");
      Contract.Requires<ArgumentOutOfRangeException>(lootLimit == null || lootLimit >= -1);

      ITile tile = TerrariaUtils.Tiles[tileLocation];
      if (tile.type != TileID.Containers && tile.type != TileID.Containers2 && tile.type != TileID.Dressers)
        throw new InvalidBlockTypeException(tile.type);

      if (checkPermissions && !player.Group.HasPermission(ProtectorPlugin.SetRefillChests_Permission))
        throw new MissingPermissionException(ProtectorPlugin.SetRefillChests_Permission);

      DPoint chestLocation = TerrariaUtils.Tiles.MeasureObject(tileLocation).OriginTileLocation;
      ProtectionEntry protection;
      lock (this.WorldMetadata.Protections)
        if (!this.WorldMetadata.Protections.TryGetValue(chestLocation, out protection))
          throw new NoProtectionException(chestLocation);

      if (protection.BankChestKey != BankChestDataKey.Invalid)
        throw new ChestIncompatibilityException();

      RefillChestMetadata refillChestData;
      if (protection.RefillChestData != null) {
        refillChestData = protection.RefillChestData;

        if (refillTime != null)
          refillChestData.RefillTimer.TimeSpan = refillTime.Value;
        if (oneLootPerPlayer != null)
          refillChestData.OneLootPerPlayer = oneLootPerPlayer.Value;
        if (lootLimit != null)
          refillChestData.RemainingLoots = lootLimit.Value;
        if (autoLock != null)
          refillChestData.AutoLock = autoLock.Value;
        if (autoEmpty != null)
          refillChestData.AutoEmpty = autoEmpty.Value;

        if (refillChestData.OneLootPerPlayer || refillChestData.RemainingLoots > 0)
          if (refillChestData.Looters == null)
            refillChestData.Looters = new Collection<int>();
        else
          refillChestData.Looters = null;

        this.RefillTimers.RemoveTimer(refillChestData.RefillTimer);

        return false;
      }

      IChest chest = this.ChestFromLocation(chestLocation);
      if (chest == null)
        throw new NoChestDataException(chestLocation);

      TimeSpan actualRefillTime = TimeSpan.Zero;
      if (refillTime != null)
        actualRefillTime = refillTime.Value;

      refillChestData = new RefillChestMetadata(player.User.ID);
      refillChestData.RefillTimer = new Timer(actualRefillTime, refillChestData, this.RefillTimerCallbackHandler);
      if (oneLootPerPlayer != null)
        refillChestData.OneLootPerPlayer = oneLootPerPlayer.Value;
      if (lootLimit != null)
        refillChestData.RemainingLoots = lootLimit.Value;

      if (refillChestData.OneLootPerPlayer || refillChestData.RemainingLoots > 0)
        refillChestData.Looters = new Collection<int>();
      else
        refillChestData.Looters = null;

      if (autoLock != null)
        refillChestData.AutoLock = autoLock.Value;

      if (autoEmpty != null)
        refillChestData.AutoEmpty = autoEmpty.Value;

      bool fairLootPutItem = fairLoot;
      for (int i = 0; i < Chest.maxItems; i++) {
        ItemData item = chest.Items[i];
        if (item.StackSize == 0 && fairLootPutItem) {
          try {
            bool isLocked;
            int keyItemType = TerrariaUtils.Tiles.GetItemTypeFromChestStyle(TerrariaUtils.Tiles.GetChestStyle(tile, out isLocked));
            chest.Items[i] = new ItemData(keyItemType);
          } catch (ArgumentException) {}

          fairLootPutItem = false;
        }

        refillChestData.RefillItems[i] = item;
      }

      protection.RefillChestData = refillChestData;

      return true;
    }

    public void SetUpBankChest(TSPlayer player, DPoint tileLocation, int bankChestIndex, bool checkPermissions = false) {
      Contract.Requires<ArgumentNullException>(player != null);
      Contract.Requires<ArgumentException>(TerrariaUtils.Tiles[tileLocation] != null, "tileLocation");
      Contract.Requires<ArgumentException>(TerrariaUtils.Tiles[tileLocation].active(), "tileLocation");
      Contract.Requires<ArgumentOutOfRangeException>(bankChestIndex >= 1, "bankChestIndex");

      ITile tile = TerrariaUtils.Tiles[tileLocation];
      if (tile.type != TileID.Containers && tile.type != TileID.Containers2 && tile.type != TileID.Dressers)
        throw new InvalidBlockTypeException(tile.type);

      if (checkPermissions && !player.Group.HasPermission(ProtectorPlugin.SetBankChests_Permission))
        throw new MissingPermissionException(ProtectorPlugin.SetBankChests_Permission);

      if (
        checkPermissions && !player.Group.HasPermission(ProtectorPlugin.NoBankChestLimits_Permission)
      ) {
        if (bankChestIndex > this.Config.MaxBankChestsPerPlayer)
          throw new ArgumentOutOfRangeException("bankChestIndex", this.Config.MaxBankChestsPerPlayer, "Global bank chest limit reached.");

        int byGroupLimit;
        if (
          this.Config.MaxBankChests.TryGetValue(player.Group.Name, out byGroupLimit) &&
          bankChestIndex > byGroupLimit
        ) {
          throw new ArgumentOutOfRangeException("bankChestIndex", byGroupLimit, "Group bank chest limit reached.");
        }
      }

      DPoint chestLocation = TerrariaUtils.Tiles.MeasureObject(tileLocation).OriginTileLocation;
      ProtectionEntry protection;
      lock (this.WorldMetadata.Protections)
        if (!this.WorldMetadata.Protections.TryGetValue(chestLocation, out protection))
          throw new NoProtectionException(chestLocation);

      if (protection.RefillChestData != null || protection.TradeChestData != null)
        throw new ChestIncompatibilityException();
     
      IChest chest = this.ChestFromLocation(chestLocation);
      if (chest == null)
        throw new NoChestDataException(chestLocation);

      if (protection.BankChestKey != BankChestDataKey.Invalid)
        throw new ChestTypeAlreadyDefinedException();

      BankChestDataKey bankChestKey = new BankChestDataKey(player.User.ID, bankChestIndex);
      lock (this.WorldMetadata.Protections) {
        if (this.WorldMetadata.Protections.Values.Any(p => p.BankChestKey == bankChestKey))
          throw new BankChestAlreadyInstancedException();
      }

      if (checkPermissions && !player.Group.HasPermission(ProtectorPlugin.BankChestShare_Permission))
        protection.Unshare();

      BankChestMetadata bankChest = this.ServerMetadataHandler.EnqueueGetBankChestMetadata(bankChestKey).Result;
      if (bankChest == null) {
        bankChest = new BankChestMetadata();
        for (int i = 0; i < Chest.maxItems; i++)
          bankChest.Items[i] = chest.Items[i];

        this.ServerMetadataHandler.EnqueueAddOrUpdateBankChest(bankChestKey, bankChest);
      } else {
        for (int i = 0; i < Chest.maxItems; i++)
          if (chest.Items[i].StackSize > 0)
            throw new ChestNotEmptyException(chestLocation);
        
        for (int i = 0; i < Chest.maxItems; i++)
          chest.Items[i] = bankChest.Items[i];
      }

      protection.BankChestKey = bankChestKey;
    }

    public void SetUpTradeChest(TSPlayer player, DPoint tileLocation, int sellAmount, int sellItemId, int payAmount, object payItemIdOrGroup, int lootLimit = 0, bool checkPermissions = false) {
      Contract.Requires<ArgumentNullException>(player != null);
      Contract.Requires<ArgumentException>(TerrariaUtils.Tiles[tileLocation] != null, "tileLocation");
      Contract.Requires<ArgumentException>(TerrariaUtils.Tiles[tileLocation].active(), "tileLocation");
      Contract.Requires<ArgumentOutOfRangeException>(sellAmount > 0, "sellAmount");
      Contract.Requires<ArgumentOutOfRangeException>(payAmount > 0, "payAmount");

      Item itemInfo = new Item();
      itemInfo.netDefaults(sellItemId);
      if (sellAmount > itemInfo.maxStack)
        throw new ArgumentOutOfRangeException(nameof(sellAmount));

      bool isPayItemGroup = payItemIdOrGroup is string;
      if (!isPayItemGroup) {
        itemInfo.netDefaults((int)payItemIdOrGroup);
        if (payAmount > itemInfo.maxStack)
          throw new ArgumentOutOfRangeException(nameof(payAmount));
      }

      ITile tile = TerrariaUtils.Tiles[tileLocation];
      if (tile.type != TileID.Containers && tile.type != TileID.Containers2 && tile.type != TileID.Dressers)
        throw new InvalidBlockTypeException(tile.type);

      if (checkPermissions && !player.Group.HasPermission(ProtectorPlugin.SetTradeChests_Permission))
        throw new MissingPermissionException(ProtectorPlugin.SetTradeChests_Permission);

      DPoint chestLocation = TerrariaUtils.Tiles.MeasureObject(tileLocation).OriginTileLocation;
      ProtectionEntry protection;
      lock (this.WorldMetadata.Protections)
        if (!this.WorldMetadata.Protections.TryGetValue(chestLocation, out protection))
          throw new NoProtectionException(chestLocation);

      if (protection.BankChestKey != BankChestDataKey.Invalid)
        throw new ChestIncompatibilityException();
     
      IChest chest = this.ChestFromLocation(chestLocation);
      if (chest == null)
        throw new NoChestDataException(chestLocation);

      bool isNewTradeChest = (protection.TradeChestData == null);
      if (isNewTradeChest && checkPermissions && this.CooperationHandler.IsSeconomyAvailable && !player.Group.HasPermission(ProtectorPlugin.FreeTradeChests_Permission)) {
        if (this.CooperationHandler.Seconomy_GetBalance(player.Name) < this.Config.TradeChestPayment)
          throw new PaymentException(this.Config.TradeChestPayment);

        this.CooperationHandler.Seconomy_TransferToWorld(player.Name, this.Config.TradeChestPayment, "Trade Chest", "Setup Price");
      }

      protection.TradeChestData = protection.TradeChestData ?? new TradeChestMetadata();
      protection.TradeChestData.ItemToSellAmount = sellAmount;
      if (protection.TradeChestData.ItemToSellId != sellItemId)
        protection.TradeChestData.LootersTable.Clear();

      protection.TradeChestData.ItemToSellId = sellItemId;
      protection.TradeChestData.ItemToPayAmount = payAmount;
      if (isPayItemGroup)
        protection.TradeChestData.ItemToPayGroup = (payItemIdOrGroup as string).ToLowerInvariant();
      else
        protection.TradeChestData.ItemToPayId = (int)payItemIdOrGroup;
      protection.TradeChestData.LootLimitPerPlayer = lootLimit;
      
      // TODO: uncomment
      //this.PluginTrace.WriteLineVerbose($"{player.Name} just setup a trade chest selling {sellAmount}x {sellItemId} for {payAmount}x {payItemId} with a limit of {lootLimit} at {tileLocation}");
    }

    public bool TryRefillChest(DPoint chestLocation, RefillChestMetadata refillChestData) {
      IChest chest = this.ChestFromLocation(chestLocation);
      if (chest == null)
        throw new InvalidOperationException("No chest data found at lcoation.");
        
      return this.TryRefillChest(chest, refillChestData);
    }

    public bool TryRefillChest(IChest chest, RefillChestMetadata refillChestData) {
      for (int i = 0; i < Chest.maxItems; i++)
        chest.Items[i] = refillChestData.RefillItems[i];

      if (
        refillChestData.AutoLock && refillChestData.RefillTime != TimeSpan.Zero && 
        !TerrariaUtils.Tiles.IsChestLocked(TerrariaUtils.Tiles[chest.Location])
      ) {
        TerrariaUtils.Tiles.LockChest(chest.Location);
      }

      return true;
    }

    public IChest PlaceChest(ushort tileType, int style, DPoint placeLocation) {
      Contract.Requires<ArgumentException>(tileType == TileID.Containers || tileType == TileID.Containers2 || tileType == TileID.Dressers);

      IChest chest;
      bool isDresser = (tileType == TileID.Dressers);
      int chestIndex = WorldGen.PlaceChest(placeLocation.X, placeLocation.Y, tileType, false, style);
      bool isWorldFull = (chestIndex == -1 || chestIndex == ChestManager.DummyChestIndex);

      if (!isWorldFull) {
        chest = new ChestAdapter(chestIndex, Main.chest[chestIndex]);
      } else {
        lock (this.WorldMetadata.ProtectorChests) {
          isWorldFull = (this.WorldMetadata.ProtectorChests.Count >= this.Config.MaxProtectorChests);
          if (isWorldFull)
            throw new LimitEnforcementException();

          if (isDresser)
            WorldGen.PlaceDresserDirect(placeLocation.X, placeLocation.Y, tileType, style, chestIndex);
          else
            WorldGen.PlaceChestDirect(placeLocation.X, placeLocation.Y, tileType, style, chestIndex);

          DPoint chestLocation = TerrariaUtils.Tiles.MeasureObject(placeLocation).OriginTileLocation;
          chest = new ProtectorChestData(chestLocation);
          this.WorldMetadata.ProtectorChests.Add(chestLocation, (ProtectorChestData)chest);

          chestIndex = ChestManager.DummyChestIndex;
        }
      }

      int storageType = 0;
      if (isDresser)
        storageType = 2;
      if (tileType == TileID.Containers2)
        storageType = 4;

      TSPlayer.All.SendData(PacketTypes.TileKill, string.Empty, storageType, placeLocation.X, placeLocation.Y, style, chestIndex);
      // The client will always show open / close animations for the latest chest index. But when there are multiple chests with id 999
      // this will look awkard, so instead tell the client about another 999 chest on a location where the animation will never be noticed by the player.
      if (chestIndex == ChestManager.DummyChestIndex)
        TSPlayer.All.SendData(PacketTypes.TileKill, string.Empty, storageType, 0, 0, style, chestIndex);

      return chest;
    }

    public IChest CreateChestData(DPoint chestLocation) {
      int chestIndex = Chest.CreateChest(chestLocation.X, chestLocation.Y);
      bool isWorldFull = (chestIndex == -1 || chestIndex == ChestManager.DummyChestIndex);

      if (!isWorldFull) {
        return new ChestAdapter(chestIndex, Main.chest[chestIndex]);
      } else {
        lock (this.WorldMetadata.ProtectorChests) {
          isWorldFull = (this.WorldMetadata.ProtectorChests.Count >= this.Config.MaxProtectorChests);
          if (isWorldFull)
            throw new LimitEnforcementException();

          IChest chest = new ProtectorChestData(chestLocation);
          this.WorldMetadata.ProtectorChests.Add(chestLocation, (ProtectorChestData)chest);
          return chest;
        }
      }
    }

    public IChest ChestFromLocation(DPoint chestLocation, TSPlayer reportToPlayer = null) {
      ITile tile = TerrariaUtils.Tiles[chestLocation];
      if (!tile.active() || (tile.type != TileID.Containers && tile.type != TileID.Containers2 && tile.type != TileID.Dressers)) {
        reportToPlayer?.SendErrorMessage("There is no chest at this position.");
        return null;
      }

      IChest chest = null;
      int chestIndex = Chest.FindChest(chestLocation.X, chestLocation.Y);
      bool isWorldDataChest = (chestIndex != -1 && chestIndex != ChestManager.DummyChestIndex);

      if (isWorldDataChest) {
        Chest tChest = Main.chest[chestIndex];
        if (tChest != null)
          chest = new ChestAdapter(chestIndex, Main.chest[chestIndex]);
        else
          reportToPlayer?.SendErrorMessage($"World data for this chest (id {chestIndex}) were expected, but was not present.");
      } else {
        lock (this.WorldMetadata.ProtectorChests) {
          ProtectorChestData protectorChest;
          if (this.WorldMetadata.ProtectorChests.TryGetValue(chestLocation, out protectorChest))
            chest = protectorChest;
          else
            reportToPlayer?.SendErrorMessage("The data record of this chest is missing in both world data and Protector's data.");
        }
      }

      return chest;
    }

    public void DestroyChest(DPoint anyTileLocation) {
      DPoint chestLocation = TerrariaUtils.Tiles.MeasureObject(anyTileLocation).OriginTileLocation;
      this.DestroyChest(this.ChestFromLocation(chestLocation));
    }

    public IEnumerable<IChest> EnumerateAllChests() {
      for (int i = 0; i < Main.chest.Length; i++) {
        if (Main.chest[i] != null && i != ChestManager.DummyChestIndex)
          yield return new ChestAdapter(i, Main.chest[i]);
      }
      
      lock (this.WorldMetadata.ProtectorChests) {
        foreach (ProtectorChestData protectorChest in this.WorldMetadata.ProtectorChests.Values)
          yield return protectorChest;
      }
    }

    public IEnumerable<IChest> EnumerateProtectorChests() {
      lock (this.WorldMetadata.ProtectorChests) {
        foreach (ProtectorChestData protectorChest in this.WorldMetadata.ProtectorChests.Values)
          yield return protectorChest;
      }
    }

    public void DestroyChest(IChest chest) {
      if (chest != null) {
        int chestIndex;
        if (chest.IsWorldChest) {
          Main.chest[chest.Index] = null;
          chestIndex = chest.Index;
        } else {
          lock (this.WorldMetadata.ProtectorChests)
            this.WorldMetadata.ProtectorChests.Remove(chest.Location);

          chestIndex = ChestManager.DummyChestIndex;
        }

        WorldGen.KillTile(chest.Location.X, chest.Location.Y);
        TSPlayer.All.SendData(PacketTypes.TileKill, string.Empty, 1, chest.Location.X, chest.Location.Y, 0, chestIndex);
      }
    }

    public bool EnsureRefillChest(ProtectionEntry protection) {
      if (protection.RefillChestData == null)
        return false;

      if (this.ChestFromLocation(protection.TileLocation) == null) {
        protection.RefillChestData = null;
        return false;
      }

      protection.RefillChestData.RefillTimer.Data = protection.RefillChestData;
      protection.RefillChestData.RefillTimer.Callback = this.RefillTimerCallbackHandler;
      this.RefillTimers.ContinueTimer(protection.RefillChestData.RefillTimer);
      return true;
    }

    public bool EnsureBankChest(ProtectionEntry protection, bool resetContent) {
      if (protection.BankChestKey == BankChestDataKey.Invalid)
        return false;

      BankChestMetadata bankChest = this.ServerMetadataHandler.EnqueueGetBankChestMetadata(protection.BankChestKey).Result;
      if (bankChest == null) {
        protection.BankChestKey = BankChestDataKey.Invalid;
        return false;
      }

      IChest chest = this.ChestFromLocation(protection.TileLocation);
      if (chest == null) {
        protection.BankChestKey = BankChestDataKey.Invalid;
        return false;
      }

      User owner = TShock.Users.GetUserByID(protection.Owner);
      if (owner == null) {
        this.DestroyChest(chest);

        protection.BankChestKey = BankChestDataKey.Invalid;
        return false;
      }

      Group ownerGroup = TShock.Groups.GetGroupByName(owner.Group);
      if (ownerGroup != null) {
        if (protection.SharedUsers != null && !ownerGroup.HasPermission(ProtectorPlugin.BankChestShare_Permission))
          protection.SharedUsers.Clear();
        if (protection.SharedGroups != null && !ownerGroup.HasPermission(ProtectorPlugin.BankChestShare_Permission))
          protection.SharedGroups.Clear();
        if (protection.IsSharedWithEveryone && !ownerGroup.HasPermission(ProtectorPlugin.BankChestShare_Permission))
          protection.IsSharedWithEveryone = false;
      }

      if (resetContent) {
        for (int i = 0; i < Chest.maxItems; i++)
          chest.Items[i] = bankChest.Items[i];
      }

      return true;
    }

    public void HandleGameSecondUpdate() {
      this.RefillTimers.HandleGameUpdate();
    }

    private bool RefillChestTimer_Callback(TimerBase timer) {
      RefillChestMetadata refillChest = (RefillChestMetadata)timer.Data;
      lock (this.WorldMetadata.Protections) {
        ProtectionEntry protection = this.WorldMetadata.Protections.Values.SingleOrDefault(p => p.RefillChestData == refillChest);
        if (protection == null)
          return false;

        DPoint chestLocation = protection.TileLocation;
        try {
          this.TryRefillChest(chestLocation, refillChest);
        } catch (InvalidOperationException) {
          this.PluginTrace.WriteLineWarning($"Chest at position {chestLocation} doesn't seem to exist anymore. Can't refill it.");
          return false;
        }
        
        // Returning true would mean the Timer would repeat.
        return false;
      }
    }
  }
}
