using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Collections.ObjectModel;
using System.Linq;
using DPoint = System.Drawing.Point;

using Terraria.Plugins.Common;
using Terraria.Plugins.Common.Collections;

using TShockAPI;
using TShockAPI.DB;

namespace Terraria.Plugins.CoderCow.Protector {
  public class ProtectionManager {
    private Configuration config;

    public PluginTrace PluginTrace { get; private set; }

    public Configuration Config {
      get { return this.config; }
      set {
        Contract.Requires<ArgumentNullException>(value != null);
        this.config = value;
      }
    }

    public ServerMetadataHandler ServerMetadataHandler { get; private set; }
    public WorldMetadata WorldMetadata { get; private set; }
    public TimerManager RefillTimers { get; private set; }
    public Func<TimerBase,bool> RefillTimerCallbackHandler { get; private set; }


    public static bool IsShareableBlockType(BlockType blockType) {
      return (
        blockType == BlockType.Chest ||
        blockType == BlockType.Dresser ||
        blockType == BlockType.Sign ||
        blockType == BlockType.Tombstone ||
        blockType == BlockType.Bed ||
        blockType == BlockType.DoorOpened ||
        blockType == BlockType.DoorClosed ||
        TerrariaUtils.Tiles.IsSwitchableBlockType(blockType)
      );
    }

    public ProtectionManager(
      PluginTrace pluginTrace, Configuration config, ServerMetadataHandler serverMetadataHandler, WorldMetadata worldMetadata
    ) {
      this.PluginTrace = pluginTrace;
      this.config = config;
      this.ServerMetadataHandler = serverMetadataHandler;
      this.WorldMetadata = worldMetadata;

      this.RefillTimers = new TimerManager(pluginTrace);
      this.RefillTimerCallbackHandler = this.RefillChestTimer_Callback;
    }

    public IEnumerable<ProtectionEntry> EnumerateProtectionEntries(DPoint tileLocation) {
      Tile tile = TerrariaUtils.Tiles[tileLocation];
      if (!tile.active())
        yield break;

      lock (this.WorldMetadata.Protections) {
        ProtectionEntry protection;
        if (TerrariaUtils.Tiles.IsSolidBlockType((BlockType)tile.type, true) || tile.type == (int)BlockType.WoodenBeam) {
          if (this.WorldMetadata.Protections.TryGetValue(tileLocation, out protection))
            yield return protection;

          // Check for adjacent sprites.
          DPoint topTileLocation = new DPoint(tileLocation.X, tileLocation.Y - 1);
          Tile topTile = TerrariaUtils.Tiles[topTileLocation];
          if (
            topTile.active() && !TerrariaUtils.Tiles.IsSolidBlockType((BlockType)topTile.type, true, true)
          ) {
            if (
              topTile.type == (int)BlockType.CrystalShard || 
              topTile.type == (int)BlockType.Torch ||
              topTile.type == (int)BlockType.Sign ||
              topTile.type == (int)BlockType.Switch
            ) {
              if (
                TerrariaUtils.Tiles.GetObjectOrientation(topTile) == Direction.Up && 
                this.WorldMetadata.Protections.TryGetValue(TerrariaUtils.Tiles.MeasureObject(topTileLocation).OriginTileLocation, out protection)
              ) {
                yield return protection;
              }
            } else if (
              topTile.type != (int)BlockType.Vine &&
              topTile.type != (int)BlockType.JungleVine &&
              topTile.type != (int)BlockType.HallowedVine &&
              !(topTile.type >= (int)BlockType.CopperChandelier && topTile.type == (int)BlockType.GoldChandelier) &&
              topTile.type != (int)BlockType.Banner &&
              topTile.type != (int)BlockType.ChainLantern &&
              topTile.type != (int)BlockType.ChineseLantern &&
              topTile.type != (int)BlockType.DiscoBall &&
              topTile.type != (int)BlockType.XMasLight
            ) {
              ObjectMeasureData topObjectMeasureData = TerrariaUtils.Tiles.MeasureObject(topTileLocation);
              if (this.WorldMetadata.Protections.TryGetValue(topObjectMeasureData.OriginTileLocation, out protection))
                yield return protection;

              foreach (ProtectionEntry protectionOnTop in this.EnumProtectionEntriesOnTopOfObject(topObjectMeasureData))
                yield return protectionOnTop;
            }
          }
        
          DPoint leftTileLocation = new DPoint(tileLocation.X - 1, tileLocation.Y);
          Tile leftTile = TerrariaUtils.Tiles[leftTileLocation];
          if (
            leftTile.active() && (
              leftTile.type == (int)BlockType.CrystalShard || 
              leftTile.type == (int)BlockType.Torch ||
              leftTile.type == (int)BlockType.Sign ||
              leftTile.type == (int)BlockType.Switch
            ) &&
            TerrariaUtils.Tiles.GetObjectOrientation(leftTile) == Direction.Left
          ) {
            if (this.WorldMetadata.Protections.TryGetValue(TerrariaUtils.Tiles.MeasureObject(leftTileLocation).OriginTileLocation, out protection))
              yield return protection;
          }

          DPoint rightTileLocation = new DPoint(tileLocation.X + 1, tileLocation.Y);
          Tile rightTile = TerrariaUtils.Tiles[rightTileLocation];
          if (
            rightTile.active() && (
              rightTile.type == (int)BlockType.CrystalShard || 
              rightTile.type == (int)BlockType.Torch ||
              rightTile.type == (int)BlockType.Sign ||
              rightTile.type == (int)BlockType.Switch
            ) &&
            TerrariaUtils.Tiles.GetObjectOrientation(rightTile) == Direction.Right
          ) {
            if (this.WorldMetadata.Protections.TryGetValue(TerrariaUtils.Tiles.MeasureObject(rightTileLocation).OriginTileLocation, out protection))
              yield return protection;
          }

          DPoint bottomTileLocation = new DPoint(tileLocation.X, tileLocation.Y + 1);
          Tile bottomTile = TerrariaUtils.Tiles[bottomTileLocation];
          if (
            bottomTile.active() && (
              bottomTile.type == (int)BlockType.Vine ||
              bottomTile.type == (int)BlockType.JungleVine ||
              bottomTile.type == (int)BlockType.HallowedVine ||
              (bottomTile.type >= (int)BlockType.CopperChandelier && bottomTile.type == (int)BlockType.GoldChandelier) ||
              bottomTile.type == (int)BlockType.Banner ||
              bottomTile.type == (int)BlockType.ChainLantern ||
              bottomTile.type == (int)BlockType.ChineseLantern ||
              bottomTile.type == (int)BlockType.DiscoBall ||
              bottomTile.type == (int)BlockType.XMasLight || (
                (bottomTile.type == (int)BlockType.CrystalShard || bottomTile.type == (int)BlockType.Sign) &&
                TerrariaUtils.Tiles.GetObjectOrientation(bottomTile) == Direction.Down
              )
            )
          ) {
            if (this.WorldMetadata.Protections.TryGetValue(TerrariaUtils.Tiles.MeasureObject(bottomTileLocation).OriginTileLocation, out protection))
              yield return protection;
          }
        } else {
          // This tile represents a sprite, no solid block.
          ObjectMeasureData measureData = TerrariaUtils.Tiles.MeasureObject(tileLocation);
        
          tileLocation = measureData.OriginTileLocation;
          if (this.WorldMetadata.Protections.TryGetValue(tileLocation, out protection))
            yield return protection;

          if (tile.type >= (int)BlockType.HerbGrowing && tile.type <= (int)BlockType.HerbBlooming) {
            // Clay Pots and their plants have a special handling - the plant should not be removable if the pot is protected.
            Tile tileBeneath = TerrariaUtils.Tiles[tileLocation.X, tileLocation.Y + 1];
            if (
              tileBeneath.type == (int)BlockType.ClayPot && 
              this.WorldMetadata.Protections.TryGetValue(new DPoint(tileLocation.X, tileLocation.Y + 1), out protection)
            ) {
              yield return protection;
            }
          } else {
            // Check all tiles above the sprite, in case a protected sprite is placed upon it (like on a table).
            foreach (ProtectionEntry protectionOnTop in this.EnumProtectionEntriesOnTopOfObject(measureData))
              yield return protectionOnTop;
          }
        }
      }
    }

    private IEnumerable<ProtectionEntry> EnumProtectionEntriesOnTopOfObject(ObjectMeasureData measureData) {
      for (int rx = 0; rx < measureData.Size.X; rx++) {
        DPoint absoluteLocation = measureData.OriginTileLocation.OffsetEx(rx, -1);

        Tile topTile = TerrariaUtils.Tiles[absoluteLocation];
        if (
          topTile.type == (int)BlockType.Bottle ||
          topTile.type == (int)BlockType.Chest ||
          topTile.type == (int)BlockType.Candle ||
          topTile.type == (int)BlockType.WaterCandle ||
          topTile.type == (int)BlockType.Book ||
          topTile.type == (int)BlockType.ClayPot ||
          topTile.type == (int)BlockType.Bed ||
          topTile.type == (int)BlockType.SkullLantern ||
          topTile.type == (int)BlockType.TrashCan_UNUSED ||
          topTile.type == (int)BlockType.Candelabra ||
          topTile.type == (int)BlockType.Bowl ||
          topTile.type == (int)BlockType.CrystalBall
        ) {
          lock (this.WorldMetadata.Protections) {
            ProtectionEntry protection;
            if (this.WorldMetadata.Protections.TryGetValue(absoluteLocation, out protection))
              yield return protection;
          }
        }
      }
    }

    public bool CheckProtectionAccess(ProtectionEntry protection, TSPlayer player, bool fullAccessRequired = false) {
      bool hasAccess = (player.IsLoggedIn && protection.Owner == player.User.ID);
      if (!hasAccess && !fullAccessRequired) {
        hasAccess = player.Group.HasPermission(ProtectorPlugin.UseEverything_Permision);
        if (!hasAccess)
          hasAccess = protection.IsSharedWithPlayer(player);
      }

      return hasAccess;
    }

    public bool CheckBlockAccess(TSPlayer player, DPoint tileLocation, bool fullAccessRequired, out ProtectionEntry relatedProtection) {
      foreach (ProtectionEntry protection in this.EnumerateProtectionEntries(tileLocation)) {
        relatedProtection = protection;

        if (!this.CheckProtectionAccess(protection, player, fullAccessRequired)) {
          relatedProtection = protection;
          return false;
        }

        // If full access is not required, then there's no use in checking the adjacent blocks.
        if (!fullAccessRequired)
          return true;
      }

      relatedProtection = null;
      return true;
    }

    public bool CheckBlockAccess(TSPlayer player, DPoint tileLocation, bool fullAccessRequired = false) {
      ProtectionEntry dummy;
      return this.CheckBlockAccess(player, tileLocation, fullAccessRequired, out dummy);
    }

    public ProtectionEntry CreateProtection(
      TSPlayer player, DPoint tileLocation, bool checkIfBlockTypeProtectableByConfig = true, 
      bool checkTShockBuildAndRegionAccess = true, bool checkLimits = true
    ) {
      Contract.Requires<ArgumentNullException>(player != null);
      Contract.Requires<ArgumentException>(TerrariaUtils.Tiles[tileLocation] != null, "tileLocation");
      Contract.Requires<ArgumentException>(TerrariaUtils.Tiles[tileLocation].active(), "tileLocation");

      Tile tile = TerrariaUtils.Tiles[tileLocation];
      BlockType blockType = (BlockType)tile.type;
      tileLocation = TerrariaUtils.Tiles.MeasureObject(tileLocation).OriginTileLocation;

      if (checkIfBlockTypeProtectableByConfig && !this.Config.ManuallyProtectableTiles[tile.type])
        throw new InvalidBlockTypeException(blockType);

      if (checkTShockBuildAndRegionAccess && TShock.CheckTilePermission(player, tileLocation.X, tileLocation.Y))
        throw new TileProtectedException(tileLocation);

      if (
        checkLimits &&
        !player.Group.HasPermission(ProtectorPlugin.NoProtectionLimits_Permission) &&
        this.WorldMetadata.CountUserProtections(player.User.ID) >= this.Config.MaxProtectionsPerPlayerPerWorld
      ) {
        throw new LimitEnforcementException();
      }

      ProtectionEntry protection;
      lock (this.WorldMetadata.Protections) {
        if (this.WorldMetadata.Protections.TryGetValue(tileLocation, out protection)) {
          if (protection.Owner == player.User.ID)
            throw new AlreadyProtectedException();

          throw new TileProtectedException(tileLocation);
        }
      }

      protection = new ProtectionEntry(player.User.ID, tileLocation, (BlockType)tile.type);
      
      lock (this.WorldMetadata.Protections)
        this.WorldMetadata.Protections.Add(tileLocation, protection);

      return protection;
    }

    public void RemoveProtection(TSPlayer player, DPoint tileLocation, bool checkIfBlockTypeDeprotectableByConfig = true) {
      Contract.Requires<ArgumentNullException>(player != null);

      bool canDeprotectEverything = player.Group.HasPermission(ProtectorPlugin.ProtectionMaster_Permission);
      if (TerrariaUtils.Tiles.IsValidCoord(tileLocation)) {
        Tile tile = TerrariaUtils.Tiles[tileLocation];
        if (tile.active()) {
          if (!canDeprotectEverything && checkIfBlockTypeDeprotectableByConfig && this.Config.NotDeprotectableTiles[tile.type])
            throw new InvalidBlockTypeException((BlockType)tile.type);
        
          tileLocation = TerrariaUtils.Tiles.MeasureObject(tileLocation).OriginTileLocation;
        }
      }

      ProtectionEntry protection;
      lock (this.WorldMetadata.Protections)
        if (!this.WorldMetadata.Protections.TryGetValue(tileLocation, out protection))
          throw new NoProtectionException(tileLocation);

      if (!canDeprotectEverything && protection.Owner != player.User.ID)
        throw new TileProtectedException(tileLocation);

      if (protection.BankChestKey != BankChestDataKey.Invalid) {
        int tChestIndex = Chest.FindChest(tileLocation.X, tileLocation.Y);
        if (tChestIndex != -1) {
          Chest tChest = Main.chest[tChestIndex];
          for (int i = 0; i < Chest.maxItems; i++)
            tChest.item[i] = ItemData.None.ToItem();
        }
      }

      lock (this.WorldMetadata.Protections)
        this.WorldMetadata.Protections.Remove(tileLocation);
    }

    public void ProtectionShareAll(TSPlayer player, DPoint tileLocation, bool shareOrUnshare, bool checkPermissions = false) {
      Contract.Requires<ArgumentNullException>(player != null);
      Contract.Requires<ArgumentException>(TerrariaUtils.Tiles[tileLocation] != null, "tileLocation");
      Contract.Requires<ArgumentException>(TerrariaUtils.Tiles[tileLocation].active(), "tileLocation");

      ProtectionEntry protection;
      try {
        this.ProtectionSharePreValidation(player, tileLocation, shareOrUnshare, checkPermissions, out protection);
      } catch (Exception ex) {
        // Excludes the internal method from the callstack.
        throw ex;
      }

      if (protection.IsSharedWithEveryone == shareOrUnshare) {
        if (shareOrUnshare)
          throw new ProtectionAlreadySharedException();
        else
          throw new ProtectionNotSharedException();
      }

      protection.IsSharedWithEveryone = shareOrUnshare;
    }

    public void ProtectionShareUser(
      TSPlayer player, DPoint tileLocation, int targetUserId, bool shareOrUnshare = true, bool checkPermissions = false
    ) {
      Contract.Requires<ArgumentNullException>(player != null);
      Contract.Requires<ArgumentException>(TerrariaUtils.Tiles[tileLocation] != null, "tileLocation");
      Contract.Requires<ArgumentException>(TerrariaUtils.Tiles[tileLocation].active(), "tileLocation");

      ProtectionEntry protection;
      try {
        this.ProtectionSharePreValidation(player, tileLocation, shareOrUnshare, checkPermissions, out protection);
      } catch (Exception ex) {
        // Excludes the internal method from the callstack.
        throw ex;
      }

      User user = TShock.Users.GetUserByID(targetUserId);
      if (user == null)
        throw new ArgumentException(null, "targetUserId");

      if (shareOrUnshare) {
        if (protection.SharedUsers == null)
          protection.SharedUsers = new Collection<int>();
        else if (protection.SharedUsers.Contains(targetUserId))
          throw new ProtectionAlreadySharedException();

        protection.SharedUsers.Add(targetUserId);
      } else {
        if (protection.SharedUsers == null || !protection.SharedUsers.Contains(targetUserId))
          throw new ProtectionNotSharedException();

        protection.SharedUsers.Remove(targetUserId);

        if (protection.SharedUsers.Count == 0)
          protection.SharedUsers = null;
      }
    }

    public void ProtectionShareGroup(
      TSPlayer player, DPoint tileLocation, string targetGroupName, bool shareOrUnshare = true, bool checkPermissions = false
    ) {
      Contract.Requires<ArgumentNullException>(player != null);
      Contract.Requires<ArgumentException>(TerrariaUtils.Tiles[tileLocation] != null, "tileLocation");
      Contract.Requires<ArgumentException>(TerrariaUtils.Tiles[tileLocation].active(), "tileLocation");
      Contract.Requires<ArgumentNullException>(targetGroupName != null);

      ProtectionEntry protection;
      try {
        this.ProtectionSharePreValidation(player, tileLocation, shareOrUnshare, checkPermissions, out protection);
      } catch (Exception ex) {
        // Excludes the internal method from the callstack.
        throw ex;
      }

      if (!TShock.Groups.GroupExists(targetGroupName))
        throw new ArgumentException(null, "targetGroupName");

      if (shareOrUnshare) {
        if (protection.SharedGroups == null)
          protection.SharedGroups = new StringCollection();
        else if (protection.SharedGroups.Contains(targetGroupName))
          throw new ProtectionAlreadySharedException();

        protection.SharedGroups.Add(targetGroupName);
      } else {
        if (protection.SharedGroups == null || !protection.SharedGroups.Contains(targetGroupName))
          throw new ProtectionNotSharedException();

        protection.SharedGroups.Remove(targetGroupName);

        if (protection.SharedGroups.Count == 0)
          protection.SharedGroups = null;
      }
    }

    private void ProtectionSharePreValidation(
      TSPlayer player, DPoint tileLocation, bool shareOrUnshare, bool checkPermissions, out ProtectionEntry protection
    ) {
      Tile tile = TerrariaUtils.Tiles[tileLocation];
      BlockType blockType = (BlockType)tile.type;
      if (!ProtectionManager.IsShareableBlockType(blockType))
        throw new InvalidBlockTypeException(blockType);

      if (checkPermissions) {
        if (
          (tile.type == (int)BlockType.Chest || tile.type == (int)BlockType.Dresser) &&
          !player.Group.HasPermission(ProtectorPlugin.ChestSharing_Permission)
        ) {
          throw new MissingPermissionException(ProtectorPlugin.ChestSharing_Permission);
        }

        if (
          TerrariaUtils.Tiles.IsSwitchableBlockType((BlockType)tile.type) && 
          !player.Group.HasPermission(ProtectorPlugin.SwitchSharing_Permission)
        ) {
          throw new MissingPermissionException(ProtectorPlugin.SwitchSharing_Permission);
        }

        if (
          (
            tile.type == (int)BlockType.Sign || 
            tile.type == (int)BlockType.Tombstone ||
            tile.type == (int)BlockType.Bed ||
            tile.type == (int)BlockType.DoorOpened ||
            tile.type == (int)BlockType.DoorClosed
          ) &&
          !player.Group.HasPermission(ProtectorPlugin.OtherSharing_Permission)
        ) {
          throw new MissingPermissionException(ProtectorPlugin.OtherSharing_Permission);
        }
      }

      tileLocation = TerrariaUtils.Tiles.MeasureObject(tileLocation).OriginTileLocation;
      lock (this.WorldMetadata.Protections)
        if (!this.WorldMetadata.Protections.TryGetValue(tileLocation, out protection))
          throw new NoProtectionException(tileLocation);

      if (checkPermissions) {
        if (
          protection.BankChestKey != BankChestDataKey.Invalid &&
          !player.Group.HasPermission(ProtectorPlugin.BankChestShare_Permission)
        ) {
          throw new MissingPermissionException(ProtectorPlugin.BankChestShare_Permission);
        }
      }

      if (protection.Owner != player.User.ID && !player.Group.HasPermission(ProtectorPlugin.ProtectionMaster_Permission)) {
        if (!protection.IsSharedWithPlayer(player))
          throw new TileProtectedException(tileLocation);

        if (shareOrUnshare) {
          if (!this.Config.AllowChainedSharing)
            throw new TileProtectedException(tileLocation);
        } else if (!this.Config.AllowChainedShareAltering) {
          throw new TileProtectedException(tileLocation);
        }
      }
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

      Tile tile = TerrariaUtils.Tiles[tileLocation];
      BlockType blockType = (BlockType)tile.type;
      if (blockType != BlockType.Chest && blockType != BlockType.Dresser)
        throw new InvalidBlockTypeException(blockType);

      if (checkPermissions && !player.Group.HasPermission(ProtectorPlugin.SetRefillChests_Permission))
        throw new MissingPermissionException(ProtectorPlugin.SetRefillChests_Permission);

      DPoint chestLocation = TerrariaUtils.Tiles.MeasureObject(tileLocation).OriginTileLocation;
      ProtectionEntry protection;
      lock (this.WorldMetadata.Protections)
        if (!this.WorldMetadata.Protections.TryGetValue(chestLocation, out protection))
          throw new NoProtectionException(chestLocation);

      if (
        !player.Group.HasPermission(ProtectorPlugin.ProtectionMaster_Permission) && 
        !this.CheckBlockAccess(player, chestLocation, true)
      )
        throw new TileProtectedException(chestLocation);

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

        lock (this.RefillTimers)
          if (this.RefillTimers.IsTimerRunning(refillChestData.RefillTimer))
            this.RefillTimers.RemoveTimer(refillChestData.RefillTimer);

        return false;
      }

      int tChestIndex = Chest.FindChest(chestLocation.X, chestLocation.Y);
      if (tChestIndex == -1 || Main.chest[tChestIndex] == null)
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
      Chest tChest = Main.chest[tChestIndex];
      for (int i = 0; i < Chest.maxItems; i++) {
        ItemData item = ItemData.FromItem(tChest.item[i]);
        if (item.StackSize == 0 && fairLootPutItem) {
          try {
            bool isLocked;
            item.Type = TerrariaUtils.Tiles.GetItemTypeFromChestType(TerrariaUtils.Tiles.GetChestStyle(tile, out isLocked));

            item.StackSize = 1;
            tChest.item[i] = item.ToItem();
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

      Tile tile = TerrariaUtils.Tiles[tileLocation];
      BlockType blockType = (BlockType)tile.type;
      if (blockType != BlockType.Chest && blockType != BlockType.Dresser)
        throw new InvalidBlockTypeException(blockType);

      if (checkPermissions && !player.Group.HasPermission(ProtectorPlugin.SetBankChests_Permission))
        throw new MissingPermissionException(ProtectorPlugin.SetBankChests_Permission);

      if (
        checkPermissions && !player.Group.HasPermission(ProtectorPlugin.NoBankChestLimits_Permision)
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

      if (!this.CheckBlockAccess(player, chestLocation, true))
        throw new TileProtectedException(chestLocation);

      if (protection.RefillChestData != null)
        throw new ChestIncompatibilityException();
     
      int tChestIndex = Chest.FindChest(chestLocation.X, chestLocation.Y);
      if (tChestIndex == -1 || Main.chest[tChestIndex] == null)
        throw new NoChestDataException(chestLocation);

      if (protection.BankChestKey != BankChestDataKey.Invalid)
        throw new ChestTypeAlreadyDefinedException();

      BankChestDataKey bankChestKey = new BankChestDataKey(player.User.ID, bankChestIndex);
      lock (this.WorldMetadata.Protections) {
        if (this.WorldMetadata.Protections.Values.Count(p => p.BankChestKey == bankChestKey) > 0)
          throw new BankChestAlreadyInstancedException();
      }

      if (checkPermissions && !player.Group.HasPermission(ProtectorPlugin.BankChestShare_Permission))
        protection.Unshare();

      Chest tChest = Main.chest[tChestIndex];
      BankChestMetadata bankChest = this.ServerMetadataHandler.EnqueueGetBankChestMetadata(bankChestKey).Result;
      if (bankChest == null) {
        bankChest = new BankChestMetadata();
        for (int i = 0; i < Chest.maxItems; i++)
          bankChest.Items[i] = ItemData.FromItem(tChest.item[i]);

        this.ServerMetadataHandler.EnqueueAddOrUpdateBankChest(bankChestKey, bankChest);
      } else {
        for (int i = 0; i < tChest.item.Length; i++) {
          if (tChest.item[i].stack > 0)
            throw new ChestNotEmptyException(chestLocation);
        }

        for (int i = 0; i < Chest.maxItems; i++)
          tChest.item[i] = bankChest.Items[i].ToItem();
      }

      protection.BankChestKey = bankChestKey;
    }

    public bool TryRefillChest(DPoint chestLocation, RefillChestMetadata refillChestData) {
      int tChestIndex = Chest.FindChest(chestLocation.X, chestLocation.Y);
      if (tChestIndex == -1)
        return false;

      Chest tChest = Main.chest[tChestIndex];
      if (tChest == null)
        return false;

      for (int i = 0; i < Chest.maxItems; i++)
        tChest.item[i] = refillChestData.RefillItems[i].ToItem();

      if (
        refillChestData.AutoLock && refillChestData.RefillTime != TimeSpan.Zero && 
        !TerrariaUtils.Tiles.IsChestLocked(TerrariaUtils.Tiles[chestLocation])
      ) {
        TerrariaUtils.Tiles.LockChest(chestLocation);
      }

      return true;
    }

    public void EnsureProtectionData(
      out int invalidProtectionsCount, out int invalidRefillChestCount, out int invalidBankChestCount
    ) {
      invalidRefillChestCount = 0;
      invalidBankChestCount = 0;

      lock (this.WorldMetadata.Protections) {
        List<DPoint> invalidProtectionLocations = new List<DPoint>();

        foreach (KeyValuePair<DPoint,ProtectionEntry> protectionPair in this.WorldMetadata.Protections) {
          DPoint location = protectionPair.Key;
          ProtectionEntry protection = protectionPair.Value;
          Tile tile = TerrariaUtils.Tiles[location];

          if (!tile.active() || (BlockType)tile.type != protection.BlockType) {
            invalidProtectionLocations.Add(location);
            continue;
          }

          if (protection.RefillChestData != null) {
            int tChestIndex = Chest.FindChest(location.X, location.Y);
            if (!tile.active() || (tile.type != (int)BlockType.Chest && tile.type != (int)BlockType.Dresser) || tChestIndex == -1) {
              protection.RefillChestData = null;
              invalidRefillChestCount++;
              continue;
            }

            protection.RefillChestData.RefillTimer.Data = protection.RefillChestData;
            protection.RefillChestData.RefillTimer.Callback = this.RefillTimerCallbackHandler;
            this.RefillTimers.ContinueTimer(protection.RefillChestData.RefillTimer);
          }
          if (protection.BankChestKey != BankChestDataKey.Invalid) {
            BankChestMetadata bankChest = this.ServerMetadataHandler.EnqueueGetBankChestMetadata(protection.BankChestKey).Result;
            if (bankChest == null) {
              protection.BankChestKey = BankChestDataKey.Invalid;
              invalidBankChestCount++;
              continue;
            }

            int tChestIndex = Chest.FindChest(location.X, location.Y);
            if (!tile.active() || (tile.type != (int)BlockType.Chest && tile.type != (int)BlockType.Dresser) || tChestIndex == -1) {
              protection.BankChestKey = BankChestDataKey.Invalid;
              invalidBankChestCount++;
              continue;
            }

            Chest tChest = Main.chest[tChestIndex];
            for (int i = 0; i < Chest.maxItems; i++)
              tChest.item[i] = bankChest.Items[i].ToItem();
          }
        }
        
        foreach (DPoint invalidProtectionLocation in invalidProtectionLocations)
          this.WorldMetadata.Protections.Remove(invalidProtectionLocation);

        
        invalidProtectionsCount = invalidProtectionLocations.Count;
      } 
    }

    public void HandleGameUpdate() {
      lock (this.RefillTimers) {
        this.RefillTimers.HandleGameUpdate();
      }
    }

    private bool RefillChestTimer_Callback(TimerBase timer) {
      RefillChestMetadata refillChest = (RefillChestMetadata)timer.Data;
      lock (this.WorldMetadata.Protections) {
        ProtectionEntry protection = this.WorldMetadata.Protections.Values.SingleOrDefault(p => p.RefillChestData == refillChest);
        if (protection == null)
          return false;

        DPoint chestLocation = protection.TileLocation;
        this.TryRefillChest(chestLocation, refillChest);
        
        // Returning true would mean the Timer would repeat.
        return false;
      }
    }
  }
}
