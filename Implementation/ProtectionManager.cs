using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Collections.ObjectModel;
using System.Linq;
using OTAPI.Tile;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ObjectData;
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

    public static bool IsShareableBlockType(int blockType) {
      return (
        blockType == TileID.Containers ||
        blockType == TileID.Containers2 ||
        blockType == TileID.Dressers ||
        blockType == TileID.Signs ||
        blockType == TileID.Tombstones ||
        blockType == TileID.Beds ||
        blockType == TileID.OpenDoor ||
        blockType == TileID.ClosedDoor ||
        TerrariaUtils.Tiles.IsSwitchableObject(blockType)
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

    public ProtectionEntry GetProtectionAt(DPoint tileLocation) {
      foreach (ProtectionEntry protection in this.EnumerateProtectionEntries(tileLocation))
        return protection;

      return null;
    }

    public IEnumerable<ProtectionEntry> EnumerateProtectionEntries(DPoint tileLocation) {
      ITile tile = TerrariaUtils.Tiles[tileLocation];
      if (!tile.active())
        yield break;

      lock (this.WorldMetadata.Protections) {
        ProtectionEntry protection;
        if (TerrariaUtils.Tiles.IsSolidBlockType(tile.type, true) || tile.type == TileID.WoodenBeam) {
          if (this.WorldMetadata.Protections.TryGetValue(tileLocation, out protection))
            yield return protection;

          // ** Enumerate Adjacent Object Protections **
          DPoint topTileLocation = new DPoint(tileLocation.X, tileLocation.Y - 1);
          DPoint leftTileLocation = new DPoint(tileLocation.X - 1, tileLocation.Y);
          DPoint rightTileLocation = new DPoint(tileLocation.X + 1, tileLocation.Y);
          DPoint bottomTileLocation = new DPoint(tileLocation.X, tileLocation.Y + 1);
          ITile topTile = TerrariaUtils.Tiles[topTileLocation];
          ITile leftTile = TerrariaUtils.Tiles[leftTileLocation];
          ITile rightTile = TerrariaUtils.Tiles[rightTileLocation];
          ITile bottomTile = TerrariaUtils.Tiles[bottomTileLocation];
          TileObjectData topTileData = TileObjectData.GetTileData(topTile);
          TileObjectData leftTileData = TileObjectData.GetTileData(leftTile);
          TileObjectData rightTileData = TileObjectData.GetTileData(rightTile);
          TileObjectData bottomTileData = TileObjectData.GetTileData(bottomTile);

          // Top tile is object and is object placed on top of this tile?
          if (topTileData != null && topTileData.AnchorBottom.type != AnchorType.None && topTile.type != TileID.Containers && topTile.type != TileID.Containers2 && topTile.type != TileID.Dressers) {
            ObjectMeasureData topObjectMeasureData = TerrariaUtils.Tiles.MeasureObject(topTileLocation);
            bool isObjectAllowingWallPlacement = (
              topTile.type == TileID.Signs ||
              topTile.type == TileID.Switches ||
              topTile.type == TileID.Lever
            );

            bool isProtected = this.WorldMetadata.Protections.TryGetValue(topObjectMeasureData.OriginTileLocation, out protection);
            if (isProtected) {
              bool hasWallsBehind = false;
              if (isObjectAllowingWallPlacement)
                hasWallsBehind = TerrariaUtils.Tiles.EnumerateObjectTiles(topObjectMeasureData).All((t) => t.wall != 0);

              if (!isObjectAllowingWallPlacement || !hasWallsBehind)
                yield return protection;
            }

            // There may also be protected objects on top of the object.
            foreach (ProtectionEntry topProtection in this.EnumProtectionEntriesOnTopOfObject(topObjectMeasureData))
              yield return topProtection;
          }

          // Left tile is object and is object placed on the left edge of this tile?
          if (leftTileData != null && leftTileData.AnchorRight.type != AnchorType.None) {
            ObjectMeasureData leftObjectMeasureData = TerrariaUtils.Tiles.MeasureObject(leftTileLocation);
            bool isObjectAllowingWallPlacement = (
              leftTile.type == TileID.Signs ||
              leftTile.type == TileID.Switches
            );

            bool isProtected = this.WorldMetadata.Protections.TryGetValue(leftObjectMeasureData.OriginTileLocation, out protection);
            if (isProtected) {
              bool hasWallsBehind = false;
              if (isObjectAllowingWallPlacement)
                hasWallsBehind = TerrariaUtils.Tiles.EnumerateObjectTiles(leftObjectMeasureData).All((t) => t.wall != 0);

              if (!isObjectAllowingWallPlacement || !hasWallsBehind)
                yield return protection;
            }
          }

          // Right tile is object and is object placed on the right edge of this tile?
          if (rightTileData != null && rightTileData.AnchorLeft.type != AnchorType.None) {
            ObjectMeasureData rightObjectMeasureData = TerrariaUtils.Tiles.MeasureObject(rightTileLocation);
            bool isObjectAllowingWallPlacement = (
              rightTile.type == TileID.Signs ||
              rightTile.type == TileID.Switches
            );

            bool isProtected = this.WorldMetadata.Protections.TryGetValue(rightObjectMeasureData.OriginTileLocation, out protection);
            if (isProtected) {
              bool hasWallsBehind = false;
              if (isObjectAllowingWallPlacement)
                hasWallsBehind = TerrariaUtils.Tiles.EnumerateObjectTiles(rightObjectMeasureData).All((t) => t.wall != 0);

              if (!isObjectAllowingWallPlacement || !hasWallsBehind)
                yield return protection;
            }
          }

          // Bottom tile is object and is object placed on the bottom edge of this tile?
          if (bottomTileData != null && bottomTileData.AnchorTop.type != AnchorType.None) {
            ObjectMeasureData bottomObjectMeasureData = TerrariaUtils.Tiles.MeasureObject(bottomTileLocation);
            bool isObjectAllowingWallPlacement = (
              bottomTile.type == TileID.Signs
            );

            bool isProtected = this.WorldMetadata.Protections.TryGetValue(bottomObjectMeasureData.OriginTileLocation, out protection);
            if (isProtected) {
              bool hasWallsBehind = false;
              if (isObjectAllowingWallPlacement)
                hasWallsBehind = TerrariaUtils.Tiles.EnumerateObjectTiles(bottomObjectMeasureData).All((t) => t.wall != 0);

              if (!isObjectAllowingWallPlacement || !hasWallsBehind)
                yield return protection;
            }
          }
        } else {
          // This tile represents a sprite, no solid block.
          ObjectMeasureData measureData = TerrariaUtils.Tiles.MeasureObject(tileLocation);
        
          tileLocation = measureData.OriginTileLocation;
          if (this.WorldMetadata.Protections.TryGetValue(tileLocation, out protection))
            yield return protection;

          if (tile.type >= TileID.ImmatureHerbs && tile.type <= TileID.BloomingHerbs) {
            // Clay Pots and their plants have a special handling - the plant should not be removable if the pot is protected.
            ITile tileBeneath = TerrariaUtils.Tiles[tileLocation.X, tileLocation.Y + 1];
            if (
              tileBeneath.type == TileID.ClayPot && 
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

        ITile topTile = TerrariaUtils.Tiles[absoluteLocation];
        TileObjectData topData = TileObjectData.GetTileData(topTile);
        if (topData != null && (topData.AnchorBottom.type & AnchorType.Table) != 0) {
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
        hasAccess = player.Group.HasPermission(ProtectorPlugin.UseEverything_Permission);
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

      ITile tile = TerrariaUtils.Tiles[tileLocation];
      int blockType = tile.type;
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

        protection = new ProtectionEntry(player.User.ID, tileLocation, tile.type);
        this.WorldMetadata.Protections.Add(tileLocation, protection);

        return protection;
      }
    }

    public void RemoveProtection(TSPlayer player, DPoint tileLocation, bool checkIfBlockTypeDeprotectableByConfig = true) {
      Contract.Requires<ArgumentNullException>(player != null);

      bool canDeprotectEverything = player.Group.HasPermission(ProtectorPlugin.ProtectionMaster_Permission);
      if (TerrariaUtils.Tiles.IsValidCoord(tileLocation)) {
        ITile tile = TerrariaUtils.Tiles[tileLocation];
        if (tile.active()) {
          if (!canDeprotectEverything && checkIfBlockTypeDeprotectableByConfig && this.Config.NotDeprotectableTiles[tile.type])
            throw new InvalidBlockTypeException(tile.type);
        
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
            chest.Items[i] = ItemData.None;
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
      ITile tile = TerrariaUtils.Tiles[tileLocation];
      int blockType = tile.type;
      if (!ProtectionManager.IsShareableBlockType(blockType))
        throw new InvalidBlockTypeException(blockType);

      if (checkPermissions) {
        if (
          (tile.type == TileID.Containers || tile.type == TileID.Containers2 || tile.type == TileID.Dressers) &&
          !player.Group.HasPermission(ProtectorPlugin.ChestSharing_Permission)
        ) {
          throw new MissingPermissionException(ProtectorPlugin.ChestSharing_Permission);
        }

        if (
          TerrariaUtils.Tiles.IsSwitchableObject(tile.type) && 
          !player.Group.HasPermission(ProtectorPlugin.SwitchSharing_Permission)
        ) {
          throw new MissingPermissionException(ProtectorPlugin.SwitchSharing_Permission);
        }

        if (
          (
            tile.type == TileID.Signs || 
            tile.type == TileID.Tombstones ||
            tile.type == TileID.Beds ||
            tile.type == TileID.OpenDoor ||
            tile.type == TileID.ClosedDoor
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
          ITile tile = TerrariaUtils.Tiles[location];

          if (!tile.active() || tile.type != protection.BlockType) {
            invalidProtectionLocations.Add(location);
            continue;
          }

          User owner = TShock.Users.GetUserByID(protection.Owner);
          if (owner == null) {
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
