using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Collections.ObjectModel;
using System.Linq;
using Terraria.ID;
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

    public ChestManager ChestManager { get; }
    public ServerMetadataHandler ServerMetadataHandler { get; private set; }
    public WorldMetadata WorldMetadata { get; private set; }

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
      PluginTrace pluginTrace, Configuration config, ChestManager chestManager, ServerMetadataHandler serverMetadataHandler, WorldMetadata worldMetadata
    ) {
      this.PluginTrace = pluginTrace;
      this.config = config;
      this.ChestManager = chestManager;
      this.ServerMetadataHandler = serverMetadataHandler;
      this.WorldMetadata = worldMetadata;
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

      lock (this.WorldMetadata.Protections) {
        ProtectionEntry protection;

        if (this.WorldMetadata.Protections.TryGetValue(tileLocation, out protection)) {
          if (protection.Owner == player.User.ID)
            throw new AlreadyProtectedException();

          throw new TileProtectedException(tileLocation);
        }

        protection = new ProtectionEntry(player.User.ID, tileLocation, (BlockType)tile.type);
        this.WorldMetadata.Protections.Add(tileLocation, protection);

        return protection;
      }
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
        IChest chest = this.ChestManager.ChestFromLocation(tileLocation);
        if (chest != null)
          for (int i = 0; i < Chest.maxItems; i++)
            chest.SetItem(i, ItemData.None);
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

    public void EnsureProtectionData(
      bool resetBankChestContent, out int invalidProtectionsCount, out int invalidRefillChestCount, out int invalidBankChestCount
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

          if (protection.RefillChestData != null && !this.ChestManager.EnsureRefillChest(protection))
            invalidRefillChestCount++;
          else if (protection.BankChestKey != BankChestDataKey.Invalid && !this.ChestManager.EnsureBankChest(protection, resetBankChestContent))
            invalidBankChestCount++;
        }
        
        foreach (DPoint invalidProtectionLocation in invalidProtectionLocations)
          this.WorldMetadata.Protections.Remove(invalidProtectionLocation);

        
        invalidProtectionsCount = invalidProtectionLocations.Count;
      } 
    }
  }
}
