using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Xna.Framework;
using OTAPI.Tile;
using Terraria.ID;
using Terraria.Localization;
using DPoint = System.Drawing.Point;

using Terraria.Plugins.Common;
using Terraria.Plugins.Common.Collections;
using TShockAPI;
using TShockAPI.DB;

namespace Terraria.Plugins.CoderCow.Protector {
  public class UserInteractionHandler: UserInteractionHandlerBase, IDisposable {
    protected PluginInfo PluginInfo { get; }
    protected Configuration Config { get; private set; }
    protected ServerMetadataHandler ServerMetadataHandler { get; }
    protected WorldMetadata WorldMetadata { get; }
    protected ChestManager ChestManager { get; }
    protected ProtectionManager ProtectionManager { get; }
    public PluginCooperationHandler PluginCooperationHandler { get; }
    protected Func<Configuration> ReloadConfigurationCallback { get; private set; }
    // Which player has currently opened which chest and the other way round for faster lookup.
    protected Dictionary<int,DPoint> PlayerIndexChestDictionary { get; }
    protected Dictionary<DPoint,int> ChestPlayerIndexDictionary { get; }
    
    public UserInteractionHandler(
      PluginTrace trace, PluginInfo pluginInfo, Configuration config, ServerMetadataHandler serverMetadataHandler, 
      WorldMetadata worldMetadata, ProtectionManager protectionManager, ChestManager chestManager, 
      PluginCooperationHandler pluginCooperationHandler, Func<Configuration> reloadConfigurationCallback
    ): base(trace) {
      if (trace == null) throw new ArgumentNullException();
      if (!(!pluginInfo.Equals(PluginInfo.Empty))) throw new ArgumentException();
      if (config == null) throw new ArgumentNullException();
      if (serverMetadataHandler == null) throw new ArgumentNullException();
      if (worldMetadata == null) throw new ArgumentNullException();
      if (protectionManager == null) throw new ArgumentNullException();
      if (pluginCooperationHandler == null) throw new ArgumentNullException();
      if (reloadConfigurationCallback == null) throw new ArgumentNullException();

      this.PluginInfo = pluginInfo;
      this.Config = config;
      this.ServerMetadataHandler = serverMetadataHandler;
      this.WorldMetadata = worldMetadata;
      this.ChestManager = chestManager;
      this.ProtectionManager = protectionManager;
      this.PluginCooperationHandler = pluginCooperationHandler;
      this.ReloadConfigurationCallback = reloadConfigurationCallback;

      this.PlayerIndexChestDictionary = new Dictionary<int,DPoint>(20);
      this.ChestPlayerIndexDictionary = new Dictionary<DPoint,int>(20);

      #region Command Setup
      base.RegisterCommand(
        new[] { "protector" }, this.RootCommand_Exec, this.RootCommand_HelpCallback
      );
      base.RegisterCommand(
        new[] { "protect", "pt" },
        this.ProtectCommand_Exec, this.ProtectCommand_HelpCallback, ProtectorPlugin.ManualProtect_Permission, 
        allowServer: false
      );
      base.RegisterCommand(
        new[] { "deprotect", "dp" },
        this.DeprotectCommand_Exec, this.DeprotectCommand_HelpCallback, ProtectorPlugin.ManualDeprotect_Permission,
        allowServer: false
      );
      base.RegisterCommand(
        new[] { "protectioninfo", "ptinfo", "pi" }, this.ProtectionInfoCommand_Exec, this.ProtectionInfoCommand_HelpCallback,
        allowServer: false
      );
      base.RegisterCommand(
        new[] { "share" }, this.ShareCommand_Exec, this.ShareCommandHelpCallback,
        allowServer: false
      );
      base.RegisterCommand(
        new[] { "unshare" }, this.UnshareCommand_Exec, this.UnshareCommand_HelpCallback,
        allowServer: false
      );
      base.RegisterCommand(
        new[] { "sharepublic" }, this.SharePublicCommand_Exec, this.SharePublicCommandHelpCallback,
        allowServer: false
      );
      base.RegisterCommand(
        new[] { "unsharepublic" }, this.UnsharePublicCommand_Exec, this.UnsharePublicCommand_HelpCallback,
        allowServer: false
      );
      base.RegisterCommand(
        new[] { "sharegroup" }, this.ShareGroupCommand_Exec, this.ShareGroupCommand_HelpCallback, 
        ProtectorPlugin.ShareWithGroups_Permission,
        allowServer: false
      );
      base.RegisterCommand(
        new[] { "unsharegroup" }, this.UnshareGroupCommand_Exec, this.UnshareGroup_HelpCallback, 
        ProtectorPlugin.ShareWithGroups_Permission,
        allowServer: false
      );
      base.RegisterCommand(
        new[] { "lockchest", "lchest" },
        this.LockChestCommand_Exec, this.LockChestCommand_HelpCallback, ProtectorPlugin.Utility_Permission,
        allowServer: false
      );
      base.RegisterCommand(
        new[] { "swapchest", "schest" },
        this.SwapChestCommand_Exec, this.SwapChestCommand_HelpCallback, ProtectorPlugin.Utility_Permission,
        allowServer: false
      );
      base.RegisterCommand(
        new[] { "refillchest", "rchest" },
        this.RefillChestCommand_Exec, this.RefillChestCommand_HelpCallback, ProtectorPlugin.SetRefillChests_Permission,
        allowServer: false
      );
      base.RegisterCommand(
        new[] { "refillchestmany", "rchestmany" },
        this.RefillChestManyCommand_Exec, this.RefillChestManyCommand_HelpCallback, ProtectorPlugin.Utility_Permission
      );
      base.RegisterCommand(
        new[] { "bankchest", "bchest" },
        this.BankChestCommand_Exec, this.BankChestCommand_HelpCallback, ProtectorPlugin.SetBankChests_Permission,
        allowServer: false
      );
      base.RegisterCommand(
        new[] { "dumpbankchest", "dbchest" },
        this.DumpBankChestCommand_Exec, this.DumpBankChestCommand_HelpCallback, ProtectorPlugin.DumpBankChests_Permission,
        allowServer: false
      );
      base.RegisterCommand(
        new[] { "tradechest", "tchest" },
        this.TradeChestCommand_Exec, this.TradeChestCommand_HelpCallback, ProtectorPlugin.SetTradeChests_Permission,
        allowServer: false
      );
      base.RegisterCommand(
        new[] { "scanchests" },
        this.ScanChestsCommand_Exec, this.ScanChestsCommand_HelpCallback, ProtectorPlugin.ScanChests_Permission
      );
      base.RegisterCommand(
        new[] { "tpchest" }, 
        this.TpChestCommand_Exec, this.TpChestCommand_HelpCallback, ProtectorPlugin.ScanChests_Permission,
        allowServer: false
      );
      #endregion
      
      #if DEBUG
      base.RegisterCommand(new[] { "fc" }, args => {
        for (int i= 0; i < Main.chest.Length; i++) {
          if (i != ChestManager.DummyChestIndex)
            Main.chest[i] = Main.chest[i] ?? new Chest();
        }
      }, requiredPermission: Permissions.maintenance);
      base.RegisterCommand(new[] { "fcnames" }, args => {
        for (int i= 0; i < Main.chest.Length; i++) {
          if (i != ChestManager.DummyChestIndex) {
            Main.chest[i] = Main.chest[i] ?? new Chest();
            Main.chest[i].name = "Chest!";
          }
        }
      }, requiredPermission: Permissions.maintenance);
      #endif
    }

    #region [Command Handling /protector]
    private void RootCommand_Exec(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;
      
      base.StopInteraction(args.Player);

      if (args.Parameters.Count >= 1) {
        string subCommand = args.Parameters[0].ToLowerInvariant();

        if (this.TryExecuteSubCommand(subCommand, args))
          return;
      }

      args.Player.SendMessage(this.PluginInfo.ToString(), Color.White);
      args.Player.SendMessage(this.PluginInfo.Description, Color.White);
      args.Player.SendMessage(string.Empty, Color.Yellow);

      int playerProtectionCount = 0;
      lock (this.WorldMetadata.Protections) {
        foreach (KeyValuePair<DPoint,ProtectionEntry> protection in this.WorldMetadata.Protections) {
          if (protection.Value.Owner == args.Player.Account.ID)
            playerProtectionCount++;
        }
      }

      string statsMessage = string.Format(
        "You've created {0} of {1} possible protections so far.", playerProtectionCount, 
        this.Config.MaxProtectionsPerPlayerPerWorld
      );
      args.Player.SendMessage(statsMessage, Color.Yellow);
      args.Player.SendMessage("Type \"/protector commands\" to get a list of available commands.", Color.Yellow);
      args.Player.SendMessage("To get more general information about this plugin type \"/protector help\".", Color.Yellow);
    }

    private bool TryExecuteSubCommand(string commandNameLC, CommandArgs args) {
      switch (commandNameLC) {
        case "commands":
        case "cmds": {
          int pageNumber = 1;
          if (args.Parameters.Count > 1 && (!int.TryParse(args.Parameters[1], out pageNumber) || pageNumber < 1)) {
            args.Player.SendErrorMessage($"\"{args.Parameters[1]}\" is not a valid page number.");
            return true;
          }

          List<string> terms = new List<string>();
          if (args.Player.Group.HasPermission(ProtectorPlugin.ManualProtect_Permission))
            terms.Add("/protect");
          if (args.Player.Group.HasPermission(ProtectorPlugin.ManualDeprotect_Permission))
            terms.Add("/deprotect");

          terms.Add("/protectioninfo");
          if (
            args.Player.Group.HasPermission(ProtectorPlugin.ChestSharing_Permission) ||
            args.Player.Group.HasPermission(ProtectorPlugin.SwitchSharing_Permission) ||
            args.Player.Group.HasPermission(ProtectorPlugin.OtherSharing_Permission)
          ) {
            terms.Add("/share");
            terms.Add("/unshare");
            terms.Add("/sharepublic");
            terms.Add("/unsharepublic");

            if (args.Player.Group.HasPermission(ProtectorPlugin.ShareWithGroups_Permission)) {
              terms.Add("/sharegroup");
              terms.Add("/unsharegroup");
            }
          }
          if (args.Player.Group.HasPermission(ProtectorPlugin.SetRefillChests_Permission)) {
            terms.Add("/refillchest");
            if (args.Player.Group.HasPermission(ProtectorPlugin.Utility_Permission))
              terms.Add("/refillchestmany");
          }
          if (args.Player.Group.HasPermission(ProtectorPlugin.SetBankChests_Permission))
            terms.Add("/bankchest");
          if (args.Player.Group.HasPermission(ProtectorPlugin.DumpBankChests_Permission))
            terms.Add("/dumpbankchest");
          if (args.Player.Group.HasPermission(ProtectorPlugin.SetTradeChests_Permission))
            terms.Add("/tradechest");
          if (args.Player.Group.HasPermission(ProtectorPlugin.Utility_Permission)) {
            terms.Add("/lockchest");
            terms.Add("/swapchest");
            terms.Add("/protector invalidate");
            if (args.Player.Group.HasPermission(ProtectorPlugin.ProtectionMaster_Permission)) {
              terms.Add("/protector cleanup");
              terms.Add("/protector removeall");
            }

            terms.Add("/protector removeemptychests");
            terms.Add("/protector summary");
          }
          if (args.Player.Group.HasPermission(ProtectorPlugin.Cfg_Permission)) {
            terms.Add("/protector importinfinitechests");
            terms.Add("/protector importinfinitesigns");
            terms.Add("/protector reloadconfig");
          }

          List<string> lines = PaginationTools.BuildLinesFromTerms(terms);
          PaginationTools.SendPage(args.Player, pageNumber, lines, new PaginationTools.Settings {
            HeaderFormat = "Protector Commands (Page {0} of {1})",
            LineTextColor = Color.LightGray,
          });

          return true;
        }
        case "cleanup": {
          if (
            !args.Player.Group.HasPermission(ProtectorPlugin.Utility_Permission) ||
            !args.Player.Group.HasPermission(ProtectorPlugin.ProtectionMaster_Permission)
          ) {
            args.Player.SendErrorMessage("You do not have the necessary permission to do that.");
            return true;
          }

          if (args.Parameters.Count == 2 && args.Parameters[1].Equals("help", StringComparison.InvariantCultureIgnoreCase)) {
            args.Player.SendMessage("Command reference for /protector cleanup (Page 1 of 1)", Color.Lime);
            args.Player.SendMessage("/protector cleanup", Color.White);
            args.Player.SendMessage("Removes all protections owned by user ids which do not exists in the TShock", Color.LightGray);
            args.Player.SendMessage("database anymore.", Color.LightGray);
            args.Player.SendMessage(string.Empty, Color.LightGray);
            args.Player.SendMessage("-d = Does not destroy the tiles where the protections were set for.", Color.LightGray);
            return true;
          }

          bool destroyRelatedTiles = true;
          if (args.Parameters.Count > 1) {
            if (args.Parameters[1].Equals("-d", StringComparison.InvariantCultureIgnoreCase)) {
              destroyRelatedTiles = false;
            } else {
              args.Player.SendErrorMessage("Proper syntax: /protector cleanup [-d]");
              args.Player.SendErrorMessage("Type /protector cleanup help to get more help to this command.");
              return true;
            }
          }

          List<DPoint> protectionsToRemove = new List<DPoint>();
          lock (this.WorldMetadata.Protections) {
            foreach (KeyValuePair<DPoint,ProtectionEntry> protectionPair in this.WorldMetadata.Protections) {
              DPoint location = protectionPair.Key;
              ProtectionEntry protection = protectionPair.Value;

              TShockAPI.DB.UserAccount tsUser = TShock.UserAccounts.GetUserAccountByID(protection.Owner);
              if (tsUser == null)
                protectionsToRemove.Add(location);
            }

            foreach (DPoint protectionLocation in protectionsToRemove) {
              this.WorldMetadata.Protections.Remove(protectionLocation);
              if (destroyRelatedTiles)
                this.DestroyBlockOrObject(protectionLocation);
            }
          }
          if (args.Player != TSPlayer.Server)
            args.Player.SendSuccessMessage("{0} protections removed.", protectionsToRemove.Count);
          this.PluginTrace.WriteLineInfo("{0} protections removed.", protectionsToRemove.Count);

          return true;
        }
        case "removeall": {
          if (
            !args.Player.Group.HasPermission(ProtectorPlugin.Utility_Permission) ||
            !args.Player.Group.HasPermission(ProtectorPlugin.ProtectionMaster_Permission)
          ) {
            args.Player.SendErrorMessage("You do not have the necessary permission to do that.");
            return true;
          }

          if (args.Parameters.Count == 2 && args.Parameters[1].Equals("help", StringComparison.InvariantCultureIgnoreCase)) {
            args.Player.SendMessage("Command reference for /protector removeall (Page 1 of 1)", Color.Lime);
            args.Player.SendMessage("/protector removeall <region <region>|user <user>> [-d]", Color.White);
            args.Player.SendMessage("Removes all protections either from the given region or owned by the given user.", Color.LightGray);
            args.Player.SendMessage(string.Empty, Color.LightGray);
            args.Player.SendMessage("region <region> = Removes all protections inside <region>.", Color.LightGray);
            args.Player.SendMessage("user <user> = Removes all protections owned by <user> in this world.", Color.LightGray);
            args.Player.SendMessage("-d = Does not destroy the tiles where the protections were set for.", Color.LightGray);
            return true;
          }

          bool destroyRelatedTiles = true;
          bool regionMode = true;
          string target = null;
          bool invalidSyntax = (args.Parameters.Count < 3 || args.Parameters.Count > 4);
          if (!invalidSyntax) {
            if (args.Parameters[1].Equals("region", StringComparison.InvariantCultureIgnoreCase))
              regionMode = true;
            else if (args.Parameters[1].Equals("user", StringComparison.InvariantCultureIgnoreCase))
              regionMode = false;
            else
              invalidSyntax = true;
          }
          if (!invalidSyntax) {
            target = args.Parameters[2];
            
            if (args.Parameters.Count == 4) {
              if (args.Parameters[3].Equals("-d", StringComparison.InvariantCultureIgnoreCase))
                destroyRelatedTiles = false;
              else
                invalidSyntax = true;
            }
          }
          if (invalidSyntax) {
            args.Player.SendErrorMessage("Proper syntax: /protector removeall <region <region>|user <user>> [-d]");
            args.Player.SendErrorMessage("Type /protector removeall help to get more help to this command.");
            return true;
          }

          List<DPoint> protectionsToRemove;
          lock (this.WorldMetadata.Protections) {
            if (regionMode) {
              TShockAPI.DB.Region tsRegion = TShock.Regions.GetRegionByName(target);
              if (tsRegion == null) {
                args.Player.SendErrorMessage("Region \"{0}\" does not exist.", target);
                return true;
              }

              protectionsToRemove = new List<DPoint>(
                from loc in this.WorldMetadata.Protections.Keys
                where tsRegion.InArea(loc.X, loc.Y)
                select loc
              );
            } else {
              int userId;
              if (!TShockEx.MatchUserIdByPlayerName(target, out userId, args.Player))
                return true;

              protectionsToRemove = new List<DPoint>(
                from pt in this.WorldMetadata.Protections.Values
                where pt.Owner == userId
                select pt.TileLocation
              );
            }

            foreach (DPoint protectionLocation in protectionsToRemove) {
              this.WorldMetadata.Protections.Remove(protectionLocation);
              if (destroyRelatedTiles)
                this.DestroyBlockOrObject(protectionLocation);
            }
          }

          if (args.Player != TSPlayer.Server)
            args.Player.SendSuccessMessage("{0} protections removed.", protectionsToRemove.Count);
          this.PluginTrace.WriteLineInfo("{0} protections removed.", protectionsToRemove.Count);

          return true;
        }
        case "removeemptychests":
        case "cleanupchests": {
          if (!args.Player.Group.HasPermission(ProtectorPlugin.Utility_Permission)) {
            args.Player.SendErrorMessage("You do not have the necessary permission to do that.");
            return true;
          }

          if (args.Parameters.Count == 2 && args.Parameters[1].Equals("help", StringComparison.InvariantCultureIgnoreCase)) {
            args.Player.SendMessage("Command reference for /protector removeemptychests (Page 1 of 1)", Color.Lime);
            args.Player.SendMessage("/protector removeemptychests|cleanupchests", Color.White);
            args.Player.SendMessage("Removes all empty and unprotected chests from the world.", Color.LightGray);
            return true;
          }

          int cleanedUpChestsCount = 0;
          int cleanedUpInvalidChestDataCount = 0;
          for (int i = 0; i < Main.chest.Length; i++) {
            if (i == ChestManager.DummyChestIndex)
              continue;

            Chest tChest = Main.chest[i];
            if (tChest == null)
              continue;

            bool isEmpty = true;
            for (int j = 0; j < tChest.item.Length; j++) {
              if (tChest.item[j].stack > 0) {
                isEmpty = false;
                break;
              }
            }

            if (!isEmpty)
              continue;

            bool isInvalidEntry = false;
            DPoint chestLocation = new DPoint(tChest.x, tChest.y);
            ITile chestTile = TerrariaUtils.Tiles[chestLocation];
            if (chestTile.active() && (chestTile.type == TileID.Containers || chestTile.type == TileID.Containers2 || chestTile.type == TileID.Dressers)) {
              chestLocation = TerrariaUtils.Tiles.MeasureObject(chestLocation).OriginTileLocation;
              lock (this.WorldMetadata.Protections) {
                if (this.WorldMetadata.Protections.ContainsKey(chestLocation))
                  continue;
              }
            } else {
              Main.chest[i] = null;
              isInvalidEntry = true;
            }

            if (!isInvalidEntry) {
              WorldGen.KillTile(chestLocation.X, chestLocation.Y, false, false, true);
              TSPlayer.All.SendTileSquare(chestLocation, 4);
              cleanedUpChestsCount++;
            } else {
              cleanedUpInvalidChestDataCount++;
            }
          }

          if (args.Player != TSPlayer.Server) {
            args.Player.SendSuccessMessage(string.Format(
              "{0} empty and unprotected chests were removed. {1} invalid chest entries were removed.", 
              cleanedUpChestsCount, cleanedUpInvalidChestDataCount
            ));
          }
          this.PluginTrace.WriteLineInfo(
            "{0} empty and unprotected chests were removed. {1} invalid chest entries were removed.",
            cleanedUpChestsCount, cleanedUpInvalidChestDataCount
          );

          return true;
        }
        case "invalidate":
        case "ensure": {
          if (!args.Player.Group.HasPermission(ProtectorPlugin.Utility_Permission)) {
            args.Player.SendErrorMessage("You do not have the necessary permission to do that.");
            return true;
          }

          if (args.Parameters.Count > 1 && args.Parameters[1].Equals("help", StringComparison.InvariantCultureIgnoreCase)) {
            args.Player.SendMessage("Command reference for /protector invalidate (Page 1 of 1)", Color.Lime);
            args.Player.SendMessage("/protector invalidate|ensure", Color.White);
            args.Player.SendMessage("Removes or fixes all invalid protections of the current world.", Color.LightGray);
            return true;
          }

          this.EnsureProtectionData(args.Player, false);
          return true;
        }
        case "summary":
        case "stats": {
          if (!args.Player.Group.HasPermission(ProtectorPlugin.Utility_Permission)) {
            args.Player.SendErrorMessage("You do not have the necessary permission to do that.");
            return true;
          }

          if (args.Parameters.Count == 2 && args.Parameters[1].Equals("help", StringComparison.InvariantCultureIgnoreCase)) {
            args.Player.SendMessage("Command reference for /protector summary (Page 1 of 1)", Color.Lime);
            args.Player.SendMessage("/protector summary|stats", Color.White);
            args.Player.SendMessage("Measures global world information regarding chests, signs, protections and bank chests.", Color.LightGray);
            return true;
          }

          int protectorChestCount = this.WorldMetadata.ProtectorChests.Count;
          int chestCount = Main.chest.Count(chest => chest != null) + protectorChestCount - 1;
          int signCount = Main.sign.Count(sign => sign != null);
          int protectionsCount = this.WorldMetadata.Protections.Count;
          int sharedProtectionsCount = this.WorldMetadata.Protections.Values.Count(p => p.IsShared);
          int refillChestsCount = this.WorldMetadata.Protections.Values.Count(p => p.RefillChestData != null);

          Dictionary<int,int> userProtectionCounts = new Dictionary<int,int>(100);
          lock (this.WorldMetadata.Protections) {
            foreach (ProtectionEntry protection in this.WorldMetadata.Protections.Values) {
              if (!userProtectionCounts.ContainsKey(protection.Owner))
                userProtectionCounts.Add(protection.Owner, 1);
              else
                userProtectionCounts[protection.Owner]++;
            }
          }
          int usersWhoReachedProtectionLimitCount = userProtectionCounts.Values.Count(
            protectionCount => protectionsCount == this.Config.MaxProtectionsPerPlayerPerWorld
          );

          int bankChestCount = this.ServerMetadataHandler.EnqueueGetBankChestCount().Result;
          int bankChestInstancesCount;
          lock (this.WorldMetadata.Protections) {
            bankChestInstancesCount = this.WorldMetadata.Protections.Values.Count(
              p => p.BankChestKey != BankChestDataKey.Invalid
            );
          }
          
          if (args.Player != TSPlayer.Server) {
            args.Player.SendInfoMessage(string.Format(
              "There are {0} of {1} chests ({2} Protector chests) and {3} of {4} signs in this world.", 
              chestCount, Main.chest.Length + this.Config.MaxProtectorChests - 1, protectorChestCount, signCount, Sign.maxSigns
            ));
            args.Player.SendInfoMessage(string.Format(
              "{0} protections are intact, {1} of them are shared with other players,",
              protectionsCount, sharedProtectionsCount
            ));
            args.Player.SendInfoMessage(string.Format(
              "{0} refill chests have been set up and {1} users reached their protection limit.",
              refillChestsCount, usersWhoReachedProtectionLimitCount
            ));
            args.Player.SendInfoMessage(string.Format(
              "The database holds {0} bank chests, {1} of them are instanced in this world.",
              bankChestCount, bankChestInstancesCount
            ));
          }
          this.PluginTrace.WriteLineInfo(string.Format(
            "There are {0} of {1} chests and {2} of {3} signs in this world.", 
            chestCount, Main.chest.Length, signCount, Sign.maxSigns
          ));
          this.PluginTrace.WriteLineInfo(string.Format(
            "{0} protections are intact, {1} of them are shared with other players,",
            protectionsCount, sharedProtectionsCount
          ));
          this.PluginTrace.WriteLineInfo(string.Format(
            "{0} refill chests have been set up and {1} users reached their protection limit.",
            refillChestsCount, usersWhoReachedProtectionLimitCount
          ));
          this.PluginTrace.WriteLineInfo(string.Format(
            "The database holds {0} bank chests, {1} of them are instanced in this world.",
            bankChestCount, bankChestInstancesCount
          ));

          return true;
        }
        case "importinfinitechests": {
          if (!args.Player.Group.HasPermission(ProtectorPlugin.Cfg_Permission)) {
            args.Player.SendErrorMessage("You do not have the necessary permission to do that.");
            return true;
          }

          if (args.Parameters.Count == 2 && args.Parameters[1].Equals("help", StringComparison.InvariantCultureIgnoreCase)) {
            args.Player.SendMessage("Command reference for /protector importinfinitechests (Page 1 of 1)", Color.Lime);
            args.Player.SendMessage("/protector importinfinitechests", Color.White);
            args.Player.SendMessage("Attempts to import all chest data from the InfiniteChests' database.", Color.LightGray);
            args.Player.SendMessage("The InfiniteChests plugin must not be installed for this.", Color.LightGray);
            args.Player.SendMessage("Existing chest data will be overwritten, imported refill chests will", Color.LightGray);
            args.Player.SendMessage("loose their timer.", Color.LightGray);
            return true;
          }

          args.Player.SendInfoMessage("Importing InfiniteChests data...");
          this.PluginTrace.WriteLineInfo("Importing InfiniteChests data...");

          int importedChests;
          int protectFailures;
          try {
            this.PluginCooperationHandler.InfiniteChests_ChestDataImport(
              this.ChestManager, this.ProtectionManager, out importedChests, out protectFailures
            );
          } catch (FileNotFoundException ex) {
            args.Player.SendErrorMessage($"The \"{ex.FileName}\" database file was not found.");
            return true;
          }

          args.Player.SendSuccessMessage($"Imported {importedChests} chests. Failed to protect {protectFailures} chests.");

          return true;
        }
        case "importinfinitesigns": {
          if (!args.Player.Group.HasPermission(ProtectorPlugin.Cfg_Permission)) {
            args.Player.SendErrorMessage("You do not have the necessary permission to do that.");
            return true;
          }

          if (args.Parameters.Count == 2 && args.Parameters[1].Equals("help", StringComparison.InvariantCultureIgnoreCase)) {
            args.Player.SendMessage("Command reference for /protector importinfinitesigns (Page 1 of 1)", Color.Lime);
            args.Player.SendMessage("/protector importinfinitesigns", Color.White);
            args.Player.SendMessage("Attempts to import all sign data from the InfiniteSigns' database.", Color.LightGray);
            args.Player.SendMessage("The InfiniteSigns plugin must not be installed for this.", Color.LightGray);
            args.Player.SendMessage("Existing sign data will be overwritten.", Color.LightGray);
            return true;
          }

          args.Player.SendInfoMessage("Importing InfiniteSigns data...");
          this.PluginTrace.WriteLineInfo("Importing InfiniteSigns data...");

          int importedSigns;
          int protectFailures;
          try {
            this.PluginCooperationHandler.InfiniteSigns_SignDataImport(
              this.ProtectionManager, out importedSigns, out protectFailures
            );
          } catch (FileNotFoundException ex) {
            args.Player.SendErrorMessage(string.Format("The \"{0}\" database file was not found.", ex.FileName));
            return true;
          }

          args.Player.SendSuccessMessage(string.Format(
            "Imported {0} signs. Failed to protect {1} signs.", importedSigns, protectFailures
          ));

          return true;
        }
        case "reloadconfiguration":
        case "reloadconfig":
        case "reloadcfg": {
          if (!args.Player.Group.HasPermission(ProtectorPlugin.Cfg_Permission)) {
            args.Player.SendErrorMessage("You do not have the necessary permission to do that.");
            return true;
          }

          if (args.Parameters.Count == 2 && args.Parameters[1].Equals("help", StringComparison.InvariantCultureIgnoreCase)) {
            args.Player.SendMessage("Command reference for /protector reloadconfiguration (Page 1 of 1)", Color.Lime);
            args.Player.SendMessage("/protector reloadconfiguration|reloadconfig|reloadcfg", Color.White);
            args.Player.SendMessage("Reloads Protector's configuration file and applies all new settings.", Color.LightGray);
            args.Player.SendMessage("If the limit of bank chests was decreased then existing bank chests going", Color.LightGray);
            args.Player.SendMessage("over this limit will still be accessible until the server is restarted.", Color.LightGray);
            return true;
          }

          this.PluginTrace.WriteLineInfo("Reloading configuration file.");
          try {
            this.Config = this.ReloadConfigurationCallback();
            this.PluginTrace.WriteLineInfo("Configuration file successfully reloaded.");

            if (args.Player != TSPlayer.Server)
              args.Player.SendSuccessMessage("Configuration file successfully reloaded.");
          } catch (Exception ex) {
            this.PluginTrace.WriteLineError(
              "Reloading the configuration file failed. Keeping old configuration. Exception details:\n{0}", ex
            );
          }

          return true;
        }
      }

      return false;
    }

    private bool RootCommand_HelpCallback(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return true;

      int pageNumber;
      if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
        return false;

      switch (pageNumber) {
        default:
          args.Player.SendMessage("Protector Overview (Page 1 of 2)", Color.Lime);
          args.Player.SendMessage("This plugin provides players on TShock driven Terraria servers the possibility", Color.LightGray);
          args.Player.SendMessage("of taking ownership of certain objects or blocks, so that other players can not ", Color.LightGray);
          args.Player.SendMessage("change or use them.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("The content of a protected chest can not be altered by other players, protected ", Color.LightGray);
          break;
        case 2:
          args.Player.SendMessage("switches can not be hit by other players, signs can not be edited, beds can not ", Color.LightGray);
          args.Player.SendMessage("be used, doors not used and even plants in protected clay pots can not be ", Color.LightGray);
          args.Player.SendMessage("harvested without owning the clay pot.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("For more information and support visit Protector's thread on the TShock forums.", Color.LightGray);
          break;
      }

      return true;
    }
    #endregion

    #region [Command Handling /protect]
    private void ProtectCommand_Exec(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      bool persistentMode = false;
      if (args.Parameters.Count > 0) {
        if (args.ContainsParameter("-p", StringComparison.InvariantCultureIgnoreCase)) {
          persistentMode = true;
        } else {
          args.Player.SendErrorMessage("Proper syntax: /protect [-p]");
          args.Player.SendInfoMessage("Type /protect help to get more help to this command.");
          return;
        }
      }

      CommandInteraction interaction = this.StartOrResetCommandInteraction(args.Player);
      interaction.DoesNeverComplete = persistentMode;
      interaction.TileEditCallback += (playerLocal, editType, tileId, location, objectStyle) => {
        if (
          editType != TileEditType.PlaceTile || 
          editType != TileEditType.PlaceWall || 
          editType != TileEditType.DestroyWall || 
          editType != TileEditType.PlaceActuator
        ) {
          this.TryCreateProtection(playerLocal, location);

          playerLocal.SendTileSquare(location);
          return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
        } else if (editType == TileEditType.DestroyWall) {
          playerLocal.SendErrorMessage("Walls can not be protected.");

          playerLocal.SendTileSquare(location);
          return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
        }

        return new CommandInteractionResult { IsHandled = false, IsInteractionCompleted = false };
      };
      Func<TSPlayer,DPoint,CommandInteractionResult> usageCallbackFunc = (playerLocal, location) => {
        this.TryCreateProtection(playerLocal, location);
        playerLocal.SendTileSquare(location, 3);

        return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
      };
      interaction.SignReadCallback += usageCallbackFunc;
      interaction.ChestOpenCallback += usageCallbackFunc;
      interaction.HitSwitchCallback += usageCallbackFunc;
      interaction.SignEditCallback += (playerLocal, signIndex, location, newText) => {
        this.TryCreateProtection(playerLocal, location);
        return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
      };
      interaction.TimeExpiredCallback += (playerLocal) => {
        playerLocal.SendErrorMessage("Waited too long. The next hit object or block will not be protected.");
      };

      args.Player.SendInfoMessage("Hit or use an object or block to protect it.");
    }

    private bool ProtectCommand_HelpCallback(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return true;

      int pageNumber;
      if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
        return false;

      switch (pageNumber) {
        default:
          args.Player.SendMessage("Command reference for /protect (Page 1 of 1)", Color.Lime);
          args.Player.SendMessage("/protect|pt [-p]", Color.White);
          args.Player.SendMessage("Protects the selected object or block.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("-p = Activates persistent mode. The command will stay persistent until it times", Color.LightGray);  
          args.Player.SendMessage("     out or any other protector command is entered.", Color.LightGray);
          break;
      }

      return true;
    }
    #endregion

    #region [Command Handling /deprotect]
    private void DeprotectCommand_Exec(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      bool persistentMode = false;
      if (args.Parameters.Count > 0) {
        if (args.ContainsParameter("-p", StringComparison.InvariantCultureIgnoreCase)) {
          persistentMode = true;
        } else {
          args.Player.SendErrorMessage("Proper syntax: /deprotect [-p]");
          args.Player.SendInfoMessage("Type /deprotect help to get more help to this command.");
          return;
        }
      }

      CommandInteraction interaction = this.StartOrResetCommandInteraction(args.Player);
      interaction.DoesNeverComplete = persistentMode;
      interaction.TileEditCallback += (playerLocal, editType, tileId, location, objectStyle) => {
        if (
          editType != TileEditType.PlaceTile || 
          editType != TileEditType.PlaceWall || 
          editType != TileEditType.DestroyWall || 
          editType != TileEditType.PlaceActuator
        ) {
          this.TryRemoveProtection(playerLocal, location);

          playerLocal.SendTileSquare(location);
          return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
        }

        playerLocal.SendTileSquare(location);
        return new CommandInteractionResult { IsHandled = false, IsInteractionCompleted = false };
      };
      Func<TSPlayer,DPoint,CommandInteractionResult> usageCallbackFunc = (playerLocal, location) => {
        this.TryRemoveProtection(playerLocal, location);
        playerLocal.SendTileSquare(location, 3);

        return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
      };
      interaction.SignReadCallback += usageCallbackFunc;
      interaction.ChestOpenCallback += usageCallbackFunc;
      interaction.HitSwitchCallback += usageCallbackFunc;
      interaction.SignEditCallback += (playerLocal, signIndex, location, newText) => {
        this.TryGetProtectionInfo(playerLocal, location);
        return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
      };
      interaction.TimeExpiredCallback += (playerLocal) => {
        playerLocal.SendMessage("Waited too long. The next hit object or block will not be deprotected anymore.", Color.Red);
      };

      args.Player.SendInfoMessage("Hit or use a protected object or block to deprotect it.");
    }

    private bool DeprotectCommand_HelpCallback(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return true;

      int pageNumber;
      if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
        return false;

      switch (pageNumber) {
        default:
          args.Player.SendMessage("Command reference for /deprotect (Page 1 of 2)", Color.Lime);
          args.Player.SendMessage("/deprotect|dp [-p]", Color.White);
          args.Player.SendMessage("Deprotects the selected object or block.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("-p = Activates persistent mode. The command will stay persistent until it times", Color.LightGray);  
          args.Player.SendMessage("     out or any other Protector command is entered.", Color.LightGray);
          break;
        case 2:
          args.Player.SendMessage("Only the owner or an administrator can remove a protection. If the selected object", Color.LightGray);
          args.Player.SendMessage("is a bank chest, this bank chest instance will be removed from the world so that", Color.LightGray);
          args.Player.SendMessage("it might be instanced again.", Color.LightGray);
          break;
      }

      return true;
    }
    #endregion

    #region [Command Handling /protectioninfo]
    private void ProtectionInfoCommand_Exec(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      bool persistentMode = false;
      if (args.Parameters.Count > 0) {
        if (args.ContainsParameter("-p", StringComparison.InvariantCultureIgnoreCase)) {
          persistentMode = true;
        } else {
          args.Player.SendErrorMessage("Proper syntax: /protectioninfo [-p]");
          args.Player.SendInfoMessage("Type /protectioninfo help to get more help to this command.");
          return;
        }
      }

      CommandInteraction interaction = this.StartOrResetCommandInteraction(args.Player);
      interaction.DoesNeverComplete = persistentMode;
      interaction.TileEditCallback += (playerLocal, editType, tileId, location, objectStyle) => {
        if (
          editType != TileEditType.PlaceTile || 
          editType != TileEditType.PlaceWall || 
          editType != TileEditType.DestroyWall || 
          editType != TileEditType.PlaceActuator
        ) {
          this.TryGetProtectionInfo(playerLocal, location);

          playerLocal.SendTileSquare(location);
          return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
        }

        playerLocal.SendTileSquare(location);
        return new CommandInteractionResult { IsHandled = false, IsInteractionCompleted = false };
      };
      Func<TSPlayer,DPoint,CommandInteractionResult> usageCallbackFunc = (playerLocal, location) => {
        this.TryGetProtectionInfo(playerLocal, location);
        playerLocal.SendTileSquare(location, 3);

        return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
      };
      interaction.SignReadCallback += usageCallbackFunc;
      interaction.ChestOpenCallback += usageCallbackFunc;
      interaction.HitSwitchCallback += usageCallbackFunc;
      interaction.SignEditCallback += (playerLocal, signIndex, location, newText) => {
        this.TryGetProtectionInfo(playerLocal, location);
        return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
      };
      interaction.TimeExpiredCallback += (playerLocal) => {
        playerLocal.SendMessage("Waited too long. No protection info for the next object or block being hit will be shown.", Color.Red);
      };
      
      args.Player.SendInfoMessage("Hit or use a protected object or block to get some info about it.");
    }

    private bool ProtectionInfoCommand_HelpCallback(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return true;

      int pageNumber;
      if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
        return false;

      switch (pageNumber) {
        default:
          args.Player.SendMessage("Command reference for /protectioninfo (Page 1 of 1)", Color.Lime);
          args.Player.SendMessage("/protectioninfo|ptinfo|pi [-p]", Color.White);
          args.Player.SendMessage("Displays some information about the selected protection.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("-p = Activates persistent mode. The command will stay persistent until it times", Color.LightGray);  
          args.Player.SendMessage("     out or any other Protector command is entered.", Color.LightGray);
          break;
      }

      return true;
    }
    #endregion

    #region [Command Handling /share]
    private void ShareCommand_Exec(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      if (args.Parameters.Count < 1) {
        args.Player.SendErrorMessage("Proper syntax: /share <player name> [-p]");
        args.Player.SendInfoMessage("Type /share help to get more help to this command.");
        return;
      }

      bool persistentMode;
      string playerName;
      if (args.Parameters[args.Parameters.Count - 1].Equals("-p", StringComparison.InvariantCultureIgnoreCase)) {
        persistentMode = true;
        playerName = args.ParamsToSingleString(0, 1);
      } else {
        persistentMode = false;
        playerName = args.ParamsToSingleString();
      }
      
      TShockAPI.DB.UserAccount tsUser;
      if (!TShockEx.MatchUserByPlayerName(playerName, out tsUser, args.Player))
        return;

      this.StartShareCommandInteraction(args.Player, persistentMode, true, false, false, tsUser.ID, tsUser.Name);
    }

    private bool ShareCommandHelpCallback(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return true;

      int pageNumber;
      if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
        return false;

      switch (pageNumber) {
        default:
          args.Player.SendMessage("Command reference for /share (Page 1 of 2)", Color.Lime);
          args.Player.SendMessage("/share <player name> [-p]", Color.White);
          args.Player.SendMessage("Adds a player share to the selected protection.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("player name = The name of the player to be added. Can either be an exact user", Color.LightGray);
          args.Player.SendMessage("name or part of the name of a player being currently online.", Color.LightGray);
          break;
        case 2:
          args.Player.SendMessage("-p = Activates persistent mode. The command will stay persistent until it times", Color.LightGray);  
          args.Player.SendMessage("     out or any other protector command is entered.", Color.LightGray);
          break;
      }

      return true;
    }
    #endregion

    #region [Command Handling /unshare]
    private void UnshareCommand_Exec(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      if (args.Parameters.Count < 1) {
        args.Player.SendErrorMessage("Proper syntax: /unshare <player name>");
        args.Player.SendErrorMessage("Type /unshare help to get more help to this command.");
        return;
      }

      bool persistentMode;
      string playerName;
      if (args.Parameters[args.Parameters.Count - 1].Equals("-p", StringComparison.InvariantCultureIgnoreCase)) {
        persistentMode = true;
        playerName = args.ParamsToSingleString(0, 1);
      } else {
        persistentMode = false;
        playerName = args.ParamsToSingleString();
      }

      TShockAPI.DB.UserAccount tsUser;
      if (!TShockEx.MatchUserByPlayerName(playerName, out tsUser, args.Player))
        return;

      this.StartShareCommandInteraction(args.Player, persistentMode, false, false, false, tsUser.ID, tsUser.Name);
    }

    private bool UnshareCommand_HelpCallback(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return true;

      int pageNumber;
      if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
        return false;

      switch (pageNumber) {
        default:
          args.Player.SendMessage("Command reference for /unshare (Page 1 of 2)", Color.Lime);
          args.Player.SendMessage("/unshare <player name>", Color.White);
          args.Player.SendMessage("Removes a player share from the selected protection.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("-p = Activates persistent mode. The command will stay persistent until it times", Color.LightGray);  
          args.Player.SendMessage("     out or any other Protector command is entered.", Color.LightGray);
          break;
        case 2:
          args.Player.SendMessage("player name = The name of the player to be added. Can either be an exact user", Color.LightGray);
          args.Player.SendMessage("name or part of the name of a player being currently online.", Color.LightGray);
          break;
      }

      return true;
    }
    #endregion

    #region [Command Handling /sharepublic]
    private void SharePublicCommand_Exec(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      bool persistentMode = false;
      if (args.Parameters.Count > 0) {
        if (args.ContainsParameter("-p", StringComparison.InvariantCultureIgnoreCase)) {
          persistentMode = true;
        } else {
          args.Player.SendErrorMessage("Proper syntax: /sharepublic [-p]");
          args.Player.SendInfoMessage("Type /sharepublic help to get more help to this command.");
          return;
        }
      }

      this.StartShareCommandInteraction(args.Player, persistentMode, true, false, true);
    }

    private bool SharePublicCommandHelpCallback(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return true;

      int pageNumber;
      if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
        return false;

      switch (pageNumber) {
        default:
          args.Player.SendMessage("Command reference for /sharepublic (Page 1 of 1)", Color.Lime);
          args.Player.SendMessage("/sharepublic [-p]", Color.White);
          args.Player.SendMessage("Allows everyone to use the selected object.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("-p = Activates persistent mode. The command will stay persistent until it times", Color.LightGray);  
          args.Player.SendMessage("     out or any other protector command is entered.", Color.LightGray);
          break;
      }

      return true;
    }
    #endregion

    #region [Command Handling /unsharepublic]
    private void UnsharePublicCommand_Exec(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      bool persistentMode = false;
      if (args.Parameters.Count > 0) {
        if (args.ContainsParameter("-p", StringComparison.InvariantCultureIgnoreCase)) {
          persistentMode = true;
        } else {
          args.Player.SendErrorMessage("Proper syntax: /unsharepublic [-p]");
          args.Player.SendInfoMessage("Type /unsharepublic help to get more help to this command.");
          return;
        }
      }

      this.StartShareCommandInteraction(args.Player, persistentMode, false, false, true);
    }

    private bool UnsharePublicCommand_HelpCallback(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return true;

      int pageNumber;
      if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
        return false;

      switch (pageNumber) {
        default:
          args.Player.SendMessage("Command reference for /unsharepublic (Page 1 of 1)", Color.Lime);
          args.Player.SendMessage("/unsharepublic [-p]", Color.White);
          args.Player.SendMessage("Revokes the permission for everyone to use the selected object.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("-p = Activates persistent mode. The command will stay persistent until it times", Color.LightGray);  
          args.Player.SendMessage("     out or any other protector command is entered.", Color.LightGray);
          break;
      }

      return true;
    }
    #endregion

    #region [Command Handling /sharegroup]
    private void ShareGroupCommand_Exec(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      if (args.Parameters.Count < 1) {
        args.Player.SendErrorMessage("Proper syntax: /sharegroup <group name>");
        args.Player.SendErrorMessage("Type /sharegroup help to get more help to this command.");
        return;
      }

      bool persistentMode;
      string groupName;
      if (args.Parameters[args.Parameters.Count - 1].Equals("-p", StringComparison.InvariantCultureIgnoreCase)) {
        persistentMode = true;
        groupName = args.ParamsToSingleString(0, 1);
      } else {
        persistentMode = false;
        groupName = args.ParamsToSingleString();
      }

      if (TShock.Groups.GetGroupByName(groupName) == null) {
        args.Player.SendErrorMessage($"The group \"{groupName}\" does not exist.");

        return;
      }

      this.StartShareCommandInteraction(args.Player, persistentMode, true, true, false, groupName, groupName);
    }

    private bool ShareGroupCommand_HelpCallback(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return true;

      int pageNumber;
      if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
        return false;

      switch (pageNumber) {
        default:
          args.Player.SendMessage("Command reference for /sharegroup (Page 1 of 2)", Color.Lime);
          args.Player.SendMessage("/sharegroup <group name> [-p]", Color.White);
          args.Player.SendMessage("Adds a group share to the selected protection.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("group name = The name of the TShock group to be added.", Color.LightGray);
          args.Player.SendMessage("-p = Activates persistent mode. The command will stay persistent until it times", Color.LightGray);  
          break;
        case 2:
          args.Player.SendMessage("     out or any other Protector command is entered.", Color.LightGray);
          break;
      }

      return true;
    }
    #endregion

    #region [Command Handling /unsharegroup]
    private void UnshareGroupCommand_Exec(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      if (args.Parameters.Count < 1) {
        args.Player.SendErrorMessage("Proper syntax: /unsharegroup <groupname>");
        args.Player.SendErrorMessage("Type /unsharegroup help to get more help to this command.");
        return;
      }

      bool persistentMode;
      string groupName;
      if (args.Parameters[args.Parameters.Count - 1].Equals("-p", StringComparison.InvariantCultureIgnoreCase)) {
        persistentMode = true;
        groupName = args.ParamsToSingleString(0, 1);
      } else {
        persistentMode = false;
        groupName = args.ParamsToSingleString();
      }

      this.StartShareCommandInteraction(args.Player, persistentMode, false, true, false, groupName, groupName);
    }

    private bool UnshareGroup_HelpCallback(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return true;

      int pageNumber;
      if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
        return false;

      switch (pageNumber) {
        default:
          args.Player.SendMessage("Command reference for /unsharegroup (Page 1 of 2)", Color.Lime);
          args.Player.SendMessage("/unsharegroup <group name> [-p]", Color.White);
          args.Player.SendMessage("Removes a group share from the selected protection.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("group name = The name of the TShock group to be removed.", Color.LightGray);
          args.Player.SendMessage("-p = Activates persistent mode. The command will stay persistent until it times", Color.LightGray);  
          break;
        case 2:
          args.Player.SendMessage("     out or any other Protector command is entered.", Color.LightGray);
          break;
      }

      return true;
    }
    #endregion

    #region [Method: StartShareCommandInteraction]
    private void StartShareCommandInteraction(
      TSPlayer player, bool isPersistent, bool isShareOrUnshare, bool isGroup, bool isShareAll, 
      object shareTarget = null, string shareTargetName = null
    ) {
      CommandInteraction interaction = this.StartOrResetCommandInteraction(player);
      interaction.DoesNeverComplete = isPersistent;
      interaction.TileEditCallback += (playerLocal, editType, tileId, location, objectStyle) => {
        if (
          editType != TileEditType.PlaceTile || 
          editType != TileEditType.PlaceWall || 
          editType != TileEditType.DestroyWall || 
          editType != TileEditType.PlaceActuator
        ) {
          this.TryAlterProtectionShare(playerLocal, location, isShareOrUnshare, isGroup, isShareAll, shareTarget, shareTargetName);

          playerLocal.SendTileSquare(location);
          return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
        }

        playerLocal.SendTileSquare(location);
        return new CommandInteractionResult { IsHandled = false, IsInteractionCompleted = false };
      };
      Func<TSPlayer,DPoint,CommandInteractionResult> usageCallbackFunc = (playerLocal, location) => {
        this.TryAlterProtectionShare(playerLocal, location, isShareOrUnshare, isGroup, isShareAll, shareTarget, shareTargetName);
        playerLocal.SendTileSquare(location, 3);

        return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
      };
      interaction.SignReadCallback += usageCallbackFunc;
      interaction.ChestOpenCallback += usageCallbackFunc;
      interaction.HitSwitchCallback += usageCallbackFunc;
      interaction.SignEditCallback += (playerLocal, signIndex, location, newText) => {
        this.TryAlterProtectionShare(playerLocal, location, isShareOrUnshare, isGroup, isShareAll, shareTarget, shareTargetName);
        return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
      };

      interaction.TimeExpiredCallback += (playerLocal) => {
        if (isShareOrUnshare)
          playerLocal.SendMessage("Waited too long. No protection will be shared.", Color.Red);
        else
          playerLocal.SendMessage("Waited too long. No protection will be unshared.", Color.Red);
      };

      if (isShareOrUnshare)
        player.SendInfoMessage("Hit or use the protected object or block you want to share.");
      else
        player.SendInfoMessage("Hit or use the protected object or block you want to unshare.");
    }
    #endregion

    #region [Command Handling /lockchest]
    private void LockChestCommand_Exec(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      bool persistentMode = false;
      if (args.Parameters.Count > 0) {
        if (args.ContainsParameter("-p", StringComparison.InvariantCultureIgnoreCase)) {
          persistentMode = true;
        } else {
          args.Player.SendErrorMessage("Proper syntax: /lockchest [-p]");
          args.Player.SendInfoMessage("Type /lockchest help to get more help to this command.");
          return;
        }
      }

      CommandInteraction interaction = this.StartOrResetCommandInteraction(args.Player);
      interaction.DoesNeverComplete = persistentMode;
      interaction.TileEditCallback += (playerLocal, editType, tileId, location, objectStyle) => {
        if (
          editType != TileEditType.PlaceTile || 
          editType != TileEditType.PlaceWall || 
          editType != TileEditType.DestroyWall || 
          editType != TileEditType.PlaceActuator
        ) {
          this.TryLockChest(playerLocal, location);

          playerLocal.SendTileSquare(location);
          return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
        }

        playerLocal.SendTileSquare(location);
        return new CommandInteractionResult { IsHandled = false, IsInteractionCompleted = false };
      };
      interaction.ChestOpenCallback += (playerLocal, location) => {
        this.TryLockChest(playerLocal, location);
        playerLocal.SendTileSquare(location, 3);

        return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
      };
      interaction.TimeExpiredCallback += (playerLocal) => {
        playerLocal.SendErrorMessage("Waited too long. The next hit or opened chest will not be locked.");
      };

      args.Player.SendInfoMessage("Hit or open a chest to lock it.");
    }

    private bool LockChestCommand_HelpCallback(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return true;

      int pageNumber;
      if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
        return false;

      switch (pageNumber) {
        default:
          args.Player.SendMessage("Command reference for /lockchest (Page 1 of 2)", Color.Lime);
          args.Player.SendMessage("/lockchest|/lchest [-p]", Color.White);
          args.Player.SendMessage("Locks the selected chest so that a key is needed to open it.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("-p = Activates persistent mode. The command will stay persistent until it times", Color.LightGray);  
          args.Player.SendMessage("     out or any other protector command is entered.", Color.LightGray);
          break;
        case 2:
          args.Player.SendMessage("Note that not all types of chests can be locked.", Color.LightGray);
          break;
      }

      return false;
    }
    #endregion

    #region [Command Handling /swapchest]
    private void SwapChestCommand_Exec(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      bool persistentMode = false;
      if (args.Parameters.Count > 0) {
        if (args.ContainsParameter("-p", StringComparison.InvariantCultureIgnoreCase)) {
          persistentMode = true;
        } else {
          args.Player.SendErrorMessage("Proper syntax: /swapchest [-p]");
          args.Player.SendInfoMessage("Type /swapchest help to get more help to this command.");
          return;
        }
      }

      CommandInteraction interaction = this.StartOrResetCommandInteraction(args.Player);
      interaction.DoesNeverComplete = persistentMode;
      interaction.TileEditCallback += (playerLocal, editType, tileId, location, objectStyle) => {
        if (
          editType != TileEditType.PlaceTile || 
          editType != TileEditType.PlaceWall || 
          editType != TileEditType.DestroyWall || 
          editType != TileEditType.PlaceActuator
        ) {
          IChest newChest;
          this.TrySwapChestData(playerLocal, location, out newChest);

          playerLocal.SendTileSquare(location);
          return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
        }

        playerLocal.SendTileSquare(location);
        return new CommandInteractionResult { IsHandled = false, IsInteractionCompleted = false };
      };
      interaction.ChestOpenCallback += (playerLocal, location) => {
        IChest newChest;
        this.TrySwapChestData(playerLocal, location, out newChest);
        playerLocal.SendTileSquare(location, 3);

        return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
      };
      interaction.TimeExpiredCallback += (playerLocal) => {
        playerLocal.SendErrorMessage("Waited too long. The next hit or opened chest will not swapped.");
      };

      args.Player.SendInfoMessage("Hit or open a chest to swap its data storage.");
    }

    private bool SwapChestCommand_HelpCallback(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return true;

      int pageNumber;
      if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
        return false;

      switch (pageNumber) {
        default:
          args.Player.SendMessage("Command reference for /swapchest (Page 1 of 1)", Color.Lime);
          args.Player.SendMessage("/swapchest|/schest [-p]", Color.White);
          args.Player.SendMessage("Swaps the data of the selected chest to the world's data or to Protector data.", Color.LightGray);
          args.Player.SendMessage("This will not change the content of the chest or its protection, its name will be removed though.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("-p = Activates persistent mode. The command will stay persistent until it times", Color.LightGray);  
          args.Player.SendMessage("     out or any other protector command is entered.", Color.LightGray);
          break;
      }

      return false;
    }
    #endregion

    #region [Command Handling /refillchest]
    private void RefillChestCommand_Exec(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      bool persistentMode = false;
      bool? oneLootPerPlayer = null;
      int? lootLimit = null;
      bool? autoLock = null;
      TimeSpan? refillTime = null;
      bool invalidSyntax = false;
      int timeParameters = 0;
      bool? autoEmpty = null;
      for (int i = 0; i < args.Parameters.Count; i++) {
        string param = args.Parameters[i];
        if (param.Equals("-p", StringComparison.InvariantCultureIgnoreCase))
          persistentMode = true;
        else if (param.Equals("+ot", StringComparison.InvariantCultureIgnoreCase))
          oneLootPerPlayer = true;
        else if (param.Equals("-ot", StringComparison.InvariantCultureIgnoreCase))
          oneLootPerPlayer = false;
        else if (param.Equals("-ll", StringComparison.InvariantCultureIgnoreCase))
          lootLimit = -1;
        else if (param.Equals("+ll", StringComparison.InvariantCultureIgnoreCase)) {
          if (args.Parameters.Count - 1 == i) {
            invalidSyntax = true;
            break;
          }

          int lootTimeAmount;
          if (!int.TryParse(args.Parameters[i + 1], out lootTimeAmount) || lootTimeAmount < 0) {
            invalidSyntax = true;
            break;
          }

          lootLimit = lootTimeAmount;
          i++;
        } else if (param.Equals("+al", StringComparison.InvariantCultureIgnoreCase))
          autoLock = true;
        else if (param.Equals("-al", StringComparison.InvariantCultureIgnoreCase))
          autoLock = false;
        else if (param.Equals("+ae", StringComparison.InvariantCultureIgnoreCase))
          autoEmpty = true;
        else if (param.Equals("-ae", StringComparison.InvariantCultureIgnoreCase))
          autoEmpty = false;
        else
          timeParameters++;
      }

      if (!invalidSyntax && timeParameters > 0) {
        if (!TimeSpanEx.TryParseShort(
          args.ParamsToSingleString(0, args.Parameters.Count - timeParameters), out refillTime
        )) {
          invalidSyntax = true;
        }
      }

      if (invalidSyntax) {
        args.Player.SendErrorMessage("Proper syntax: /refillchest [time] [+ot|-ot] [+ll amount|-ll] [+al|-al] [+ae|-ae] [-p]");
        args.Player.SendErrorMessage("Type /refillchest help to get more help to this command.");
        return;
      }

      CommandInteraction interaction = this.StartOrResetCommandInteraction(args.Player);
      interaction.DoesNeverComplete = persistentMode;
      interaction.TileEditCallback += (playerLocal, editType, tileId, location, objectStyle) => {
        if (
          editType != TileEditType.PlaceTile || 
          editType != TileEditType.PlaceWall || 
          editType != TileEditType.DestroyWall || 
          editType != TileEditType.PlaceActuator
        ) {
          this.TrySetUpRefillChest(playerLocal, location, refillTime, oneLootPerPlayer, lootLimit, autoLock, autoEmpty);

          playerLocal.SendTileSquare(location);
          return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
        }

        playerLocal.SendTileSquare(location);
        return new CommandInteractionResult { IsHandled = false, IsInteractionCompleted = false };
      };
      interaction.ChestOpenCallback += (playerLocal, location) => {
        this.TrySetUpRefillChest(playerLocal, location, refillTime, oneLootPerPlayer, lootLimit, autoLock, autoEmpty);
        return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
      };
      interaction.TimeExpiredCallback += (playerLocal) => {
        playerLocal.SendMessage("Waited too long. No refill chest will be created.", Color.Red);
      };

      args.Player.SendInfoMessage("Open a chest to convert it into a refill chest.");
    }

    private bool RefillChestCommand_HelpCallback(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return true;

      int pageNumber;
      if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
        return false;

      switch (pageNumber) {
        default:
          args.Player.SendMessage("Command reference for /refillchest (Page 1 of 5)", Color.Lime);
          args.Player.SendMessage("/refillchest|/rchest [time] [+ot|-ot] [+ll amount|-ll] [+al|-al] [+ae|-ae] [-p]", Color.White);
          args.Player.SendMessage("Converts a chest to a special chest which can automatically refill its content.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("time = Examples: 2h, 2h30m, 2h30m10s, 1d6h etc.", Color.LightGray);
          args.Player.SendMessage("+ot = The chest can only be looted once per player.", Color.LightGray);
          break;
        case 2:
          args.Player.SendMessage("+ll amount = The chest can only be looted the given amount of times in total.", Color.LightGray);
          args.Player.SendMessage("+al = After being looted, the chest is automatically locked.", Color.LightGray);
          args.Player.SendMessage("+ae = After being looted, the chest is automatically emptied, regardless of content.", Color.LightGray);
          args.Player.SendMessage("-p = Activates persistent mode. The command will stay persistent until it times", Color.LightGray);  
          args.Player.SendMessage("     out or any other protector command is entered.", Color.LightGray);
          args.Player.SendMessage("If +ot or +ll is applied, a player must be logged in in order to loot it.", Color.LightGray);
          break;
        case 3:
          args.Player.SendMessage("To remove a feature from an existing refill chest, put a '-' before it:", Color.LightGray);
          args.Player.SendMessage("  /refillchest -ot", Color.White);
          args.Player.SendMessage("Removes the 'ot' feature from the selected chest.", Color.LightGray);
          args.Player.SendMessage("To remove the timer, simply leave the time parameter away.", Color.LightGray);
          args.Player.SendMessage("Example #1: Make a chest refill its contents after one hour and 30 minutes:", Color.LightGray);
          break;
        case 4:
          args.Player.SendMessage("  /refillchest 1h30m", Color.White);
          args.Player.SendMessage("Example #2: Make a chest one time lootable per player without a refill timer:", Color.LightGray);
          args.Player.SendMessage("  /refillchest +ot", Color.White);
          args.Player.SendMessage("Example #3: Make a chest one time lootable per player with a 30 minutes refill timer:", Color.LightGray);
          args.Player.SendMessage("  /refillchest 30m +ot", Color.White);
          break;
        case 5:
          args.Player.SendMessage("Example #4: Make a chest one time lootable per player and 10 times lootable in total:", Color.LightGray);
          args.Player.SendMessage("  /refillchest +ot +ll 10", Color.White);
          break;
      }

      return true;
    }
    #endregion

    #region [Command Handling /refillchestmany]
    private void RefillChestManyCommand_Exec(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      if (!args.Player.Group.HasPermission(ProtectorPlugin.SetRefillChests_Permission)) {
        args.Player.SendErrorMessage("You do not have the permission to set up refill chests.");
        return;
      }

      bool? oneLootPerPlayer = null;
      int? lootLimit = null;
      bool? autoLock = null;
      TimeSpan? refillTime = null;
      bool? autoEmpty = null;
      string selector = null;
      bool fairLoot = false;
      bool invalidSyntax = (args.Parameters.Count == 0);
      if (!invalidSyntax) {
        selector = args.Parameters[0].ToLowerInvariant();

        int timeParameters = 0;
        for (int i = 1; i < args.Parameters.Count; i++) {
          string param = args.Parameters[i];
          if (param.Equals("+ot", StringComparison.InvariantCultureIgnoreCase))
            oneLootPerPlayer = true;
          else if (param.Equals("-ot", StringComparison.InvariantCultureIgnoreCase))
            oneLootPerPlayer = false;
          else if (param.Equals("-ll", StringComparison.InvariantCultureIgnoreCase))
            lootLimit = -1;
          else if (param.Equals("+ll", StringComparison.InvariantCultureIgnoreCase)) {
            if (args.Parameters.Count - 1 == i) {
              invalidSyntax = true;
              break;
            }

            int lootTimeAmount;
            if (!int.TryParse(args.Parameters[i + 1], out lootTimeAmount) || lootTimeAmount < 0) {
              invalidSyntax = true;
              break;
            }

            lootLimit = lootTimeAmount;
            i++;
          } else if (param.Equals("+al", StringComparison.InvariantCultureIgnoreCase))
            autoLock = true;
          else if (param.Equals("+fl", StringComparison.InvariantCultureIgnoreCase))
            fairLoot = true;
          else if (param.Equals("-al", StringComparison.InvariantCultureIgnoreCase))
            autoLock = false;
          else if (param.Equals("+ae", StringComparison.InvariantCultureIgnoreCase))
            autoEmpty = true;
          else if (param.Equals("-ae", StringComparison.InvariantCultureIgnoreCase))
            autoEmpty = false;
          else
            timeParameters++;
        }

        if (!invalidSyntax && timeParameters > 0) {
          if (!TimeSpanEx.TryParseShort(
            args.ParamsToSingleString(1, args.Parameters.Count - timeParameters - 1), out refillTime
          )) {
            invalidSyntax = true;
          }
        }
      }

      ChestKind chestKindToSelect = ChestKind.Unknown;
      switch (selector) {
        case "dungeon":
          chestKindToSelect = ChestKind.DungeonChest;
          break;
        case "sky":
          chestKindToSelect = ChestKind.SkyIslandChest;
          break;
        case "ocean":
          chestKindToSelect = ChestKind.OceanChest;
          break;
        case "shadow":
          chestKindToSelect = ChestKind.HellShadowChest;
          break;
        case "hardmodedungeon":
          chestKindToSelect = ChestKind.HardmodeDungeonChest;
          break;
        case "pyramid":
          chestKindToSelect = ChestKind.PyramidChest;
          break;
        default:
          invalidSyntax = true;
          break;
      }

      if (invalidSyntax) {
        args.Player.SendErrorMessage("Proper syntax: /refillchestmany <selector> [time] [+ot|-ot] [+ll amount|-ll] [+al|-al] [+ae|-ae] [+fl]");
        args.Player.SendErrorMessage("Type /refillchestmany help to get more help to this command.");
        return;
      }

      if (chestKindToSelect != ChestKind.Unknown) {
        int createdChestsCounter = 0;
        for (int i = 0; i < Main.chest.Length; i++) {
          Chest chest = Main.chest[i];
          if (chest == null)
            continue;

          DPoint chestLocation = new DPoint(chest.x, chest.y);
          ITile chestTile = TerrariaUtils.Tiles[chestLocation];
          if (!chestTile.active() || (chestTile.type != TileID.Containers && chestTile.type != TileID.Containers2))
            continue;

          if (TerrariaUtils.Tiles.GuessChestKind(chestLocation) != chestKindToSelect)
            continue;

          try {
            ProtectionEntry protection = this.ProtectionManager.CreateProtection(args.Player, chestLocation, false);
            protection.IsSharedWithEveryone = this.Config.AutoShareRefillChests;
          } catch (AlreadyProtectedException) {
            if (!this.ProtectionManager.CheckBlockAccess(args.Player, chestLocation, true) && !args.Player.Group.HasPermission(ProtectorPlugin.ProtectionMaster_Permission)) {
              args.Player.SendWarningMessage($"You did not have access to convert chest {TShock.Utils.ColorTag(chestLocation.ToString(), Color.Red)} into a refill chest.");
              continue;
            }
          } catch (Exception ex) {
            this.PluginTrace.WriteLineWarning($"Failed to create protection at {TShock.Utils.ColorTag(chestLocation.ToString(), Color.Red)}: \n{ex}");
          }
          
          try {
            this.ChestManager.SetUpRefillChest(
              args.Player, chestLocation, refillTime, oneLootPerPlayer, lootLimit, autoLock, autoEmpty, fairLoot
            );
            createdChestsCounter++;
          } catch (Exception ex) {
            this.PluginTrace.WriteLineWarning($"Failed to create / update refill chest at {TShock.Utils.ColorTag(chestLocation.ToString(), Color.Red)}: \n{ex}");
          }
        }

        args.Player.SendSuccessMessage($"{TShock.Utils.ColorTag(createdChestsCounter.ToString(), Color.Red)} refill chests were created / updated.");
      }
    }

    private bool RefillChestManyCommand_HelpCallback(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return true;

      int pageNumber;
      if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
        return false;

      switch (pageNumber) {
        default:
          args.Player.SendMessage("Command reference for /refillchestmany (Page 1 of 3)", Color.Lime);
          args.Player.SendMessage("/refillchestmany|/rchestmany <selector> [time] [+ot|-ot] [+ll amount|-ll] [+al|-al] [+ae|-ae] [+fl]", Color.White);
          args.Player.SendMessage("Converts all selected chests to refill chests or alters them.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("selector = dungeon, sky, ocean, shadow, hardmodedungeon or pyramid", Color.LightGray);
          args.Player.SendMessage("time = Examples: 2h, 2h30m, 2h30m10s, 1d6h etc.", Color.LightGray);
          break;
        case 2:
          args.Player.SendMessage("+ot = The chest can only be looted once per player.", Color.LightGray);
          args.Player.SendMessage("+ll = The chest can only be looted the given amount of times in total.", Color.LightGray);
          args.Player.SendMessage("+al = After being looted, the chest is automatically locked.", Color.LightGray);
          args.Player.SendMessage("+ae = After being looted, the chest is automatically emptied, regardless of contents.", Color.LightGray);
          args.Player.SendMessage("+fl = An item of the chest's own type will be placed inside the chest yielding in a fair loot.", Color.LightGray);
          args.Player.SendMessage("This command is expected to be used on a fresh world, the specified selector might", Color.LightGray);
          args.Player.SendMessage("also select player chests. This is how chest kinds are distinguished:", Color.LightGray);
          break;
        case 3:
          args.Player.SendMessage("Dungeon = Locked gold chest with natural dungeon walls behind.", Color.LightGray);
          args.Player.SendMessage("Sky = Locked gold chest above surface level.", Color.LightGray);
          args.Player.SendMessage("Ocean = Unlocked submerged gold chest in the ocean biome.", Color.LightGray);
          args.Player.SendMessage("Shadow = Locked shadow chest in the world's last seventh.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("For more information about refill chests and their parameters type /help rchest.", Color.LightGray);
          break;
      }

      return true;
    }
    #endregion

    #region [Command Handling /bankchest]
    private void BankChestCommand_Exec(CommandArgs args) {
      if (args.Parameters.Count < 1) {
        args.Player.SendErrorMessage("Proper syntax: /bankchest <number>");
        args.Player.SendErrorMessage("Type /bankchest help to get more help to this command.");
        return;
      }

      int chestIndex;
      if (!int.TryParse(args.Parameters[0], out chestIndex)) {
        args.Player.SendErrorMessage("The given prameter is not a valid number.");
        return;
      }

      bool hasNoBankChestLimits = args.Player.Group.HasPermission(ProtectorPlugin.NoBankChestLimits_Permission);
      if (
        chestIndex < 1 || (chestIndex > this.Config.MaxBankChestsPerPlayer && !hasNoBankChestLimits)
      ) {
        string messageFormat;
        if (!hasNoBankChestLimits)
          messageFormat = "The bank chest number must be between 1 to {0}.";
        else
          messageFormat = "The bank chest number must be greater than 1.";

        args.Player.SendErrorMessage(string.Format(messageFormat, this.Config.MaxBankChestsPerPlayer));
        return;
      }

      CommandInteraction interaction = this.StartOrResetCommandInteraction(args.Player);
      interaction.TileEditCallback += (playerLocal, editType, tileId, location, objectStyle) => {
        if (
          editType != TileEditType.PlaceTile || 
          editType != TileEditType.PlaceWall || 
          editType != TileEditType.DestroyWall || 
          editType != TileEditType.PlaceActuator
        ) {
          this.TrySetUpBankChest(playerLocal, location, chestIndex);

          playerLocal.SendTileSquare(location);
          return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
        } else if (editType == TileEditType.DestroyWall) {
          playerLocal.SendTileSquare(location);
          return new CommandInteractionResult { IsHandled = false, IsInteractionCompleted = false };
        }

        return new CommandInteractionResult { IsHandled = false, IsInteractionCompleted = false };
      };
      interaction.ChestOpenCallback += (playerLocal, location) => {
        this.TrySetUpBankChest(playerLocal, location, chestIndex);
        return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
      };
      interaction.TimeExpiredCallback += (playerLocal) => {
        playerLocal.SendMessage("Waited too long. No bank chest will be created.", Color.Red);
      };

      args.Player.SendInfoMessage("Open a chest to convert it into a bank chest.");
    }

    private bool BankChestCommand_HelpCallback(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return true;

      int pageNumber;
      if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
        return false;

      switch (pageNumber) {
        default:
          args.Player.SendMessage("Command reference for /bankchest (Page 1 of 5)", Color.Lime);
          args.Player.SendMessage("/bankchest|/bchest <number>", Color.White);
          args.Player.SendMessage("Converts a protected chest into a bank chest instance. Bank chests store their content in a separate", Color.LightGray);
          args.Player.SendMessage("non world related database - their content remains the same, no matter what world they are instanced in.", Color.LightGray);
          args.Player.SendMessage("They are basically like piggy banks, but server sided.", Color.LightGray);
          break;
        case 2:
          args.Player.SendMessage("number = A 1-based number to uniquely identify the bank chest.", Color.LightGray);
          args.Player.SendMessage("Usually, the number '1' is assigned to the first created bank chest, '2' for the next etc.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("In order to be converted to a bank chest, a chest must be protected and the player has to own it.", Color.LightGray);
          args.Player.SendMessage("Also, if this is the first instance of a bank chest ever created, the content of the chest will", Color.LightGray);
          break;
        case 3:
          args.Player.SendMessage("be considered as the new bank chest content. If the bank chest with that number was already instanced", Color.LightGray);
          args.Player.SendMessage("before though, then the chest has to be empty so that it can safely be overwritten by the bank chest's", Color.LightGray);
          args.Player.SendMessage("actual content.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("To remove a bank chest instance, simply /deprotect it.", Color.LightGray);
          break;
        case 4:
          args.Player.SendMessage("The amount of bank chests a player can own is usually limited by configuration, also an additional permission", Color.LightGray);
          args.Player.SendMessage("is required to share a bank chest with other players.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("Only one bank chest instance with the same number shall be present in one and the same world.", Color.White);
          break;
        case 5:
          args.Player.SendMessage("Example #1: Create a bank chest with the number 1:", Color.LightGray);
          args.Player.SendMessage("  /bankchest 1", Color.White);
          break;
      }

      return true;
    }
    #endregion

    #region [Command Handling /dumpbankchest]
    private void DumpBankChestCommand_Exec(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      bool persistentMode = false;
      if (args.Parameters.Count > 0) {
        if (args.ContainsParameter("-p", StringComparison.InvariantCultureIgnoreCase)) {
          persistentMode = true;
        } else {
          args.Player.SendErrorMessage("Proper syntax: /dumpbankchest [-p]");
          args.Player.SendInfoMessage("Type /dumpbankchest help to get more help to this command.");
          return;
        }
      }

      Action<TSPlayer,DPoint> dumpBankChest = (playerLocal, chestLocation) => {
        foreach (ProtectionEntry protection in this.ProtectionManager.EnumerateProtectionEntries(chestLocation)) {
          if (protection.BankChestKey == BankChestDataKey.Invalid) {
            args.Player.SendErrorMessage("This is not a bank chest.");
            return;
          }

          protection.BankChestKey = BankChestDataKey.Invalid;
          args.Player.SendSuccessMessage("The bank chest content was sucessfully dumped and the bank chest instance was removed.");
          return;
        }

        args.Player.SendErrorMessage("This chest is not protected by Protector at all.");
      };

      CommandInteraction interaction = base.StartOrResetCommandInteraction(args.Player);
      interaction.DoesNeverComplete = persistentMode;
      interaction.TileEditCallback += (playerLocal, editType, tileId, location, objectStyle) => {
        if (
          editType != TileEditType.PlaceTile || 
          editType != TileEditType.PlaceWall || 
          editType != TileEditType.DestroyWall || 
          editType != TileEditType.PlaceActuator
        ) {
          dumpBankChest(playerLocal, location);
          playerLocal.SendTileSquare(location);

          return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
        }

        playerLocal.SendTileSquare(location);
        return new CommandInteractionResult { IsHandled = false, IsInteractionCompleted = false };
      };
      interaction.ChestOpenCallback += (playerLocal, chestLocation) => {
        dumpBankChest(playerLocal, chestLocation);
        return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
      };
      interaction.TimeExpiredCallback += (player) => {
        player.SendErrorMessage("Waited too long, no bank chest will be dumped.");
      };
      args.Player.SendInfoMessage("Open a bank chest to dump its content.");
    }

    private bool DumpBankChestCommand_HelpCallback(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return true;

      int pageNumber;
      if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
        return false;

      switch (pageNumber) {
        default:
          args.Player.SendMessage("Command reference for /dumpbankchest (Page 1 of 2)", Color.Lime);
          args.Player.SendMessage("/dumpbankchest|dbchest [-p]", Color.White);
          args.Player.SendMessage("Removes a bank chest instance but keeps its content in place actually duplicating all items.", Color.LightGray);
          args.Player.SendMessage("This allows you to use bank chests like chest-templates.", Color.LightGray);
          break;
        case 2:
          args.Player.SendMessage("-p = Activates persistent mode. The command will stay persistent until it times", Color.LightGray);  
          args.Player.SendMessage("     out or any other protector command is entered.", Color.LightGray);
          break;
      }

      return true;
    }
    #endregion

    #region [Command Handling /tradechest]
    private void TradeChestCommand_Exec(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      if (args.Parameters.Count < 4) {
        args.Player.SendErrorMessage("Proper syntax: /tradechest <sell amount> <sell item> <pay amount> <pay item or group> [limit]");
        args.Player.SendErrorMessage("Example to sell 200 wood for 5 gold coins: /tradechest 200 Wood 5 \"Gold Coin\"");
        args.Player.SendErrorMessage("Type /tradechest help to get more help to this command.");
        return;
      }

      string sellAmountRaw = args.Parameters[0];
      string sellItemRaw = args.Parameters[1];
      string payAmountRaw = args.Parameters[2];
      string payItemRaw = args.Parameters[3];

      int sellAmount;
      Item sellItem;
      int payAmount;
      object payItemIdOrGroup;
      int lootLimit = 0;

      if (!int.TryParse(sellAmountRaw, out sellAmount) || sellAmount <= 0) {
        args.Player.SendErrorMessage($"Expected <sell amount> to be a postive number, but \"{sellAmountRaw}\" was given.");
        return;
      }
      if (!int.TryParse(payAmountRaw, out payAmount) || payAmount <= 0) {
        args.Player.SendErrorMessage($"Expected <sell amount> to be a postive number, but \"{payAmountRaw}\" was given.");
        return;
      }
      if (args.Parameters.Count > 4 && (!int.TryParse(args.Parameters[4], out lootLimit) || lootLimit <= 0)) {
        args.Player.SendErrorMessage($"Expected [limit] to be a postive number, but \"{args.Parameters[4]}\" was given.");
        return;
      }

      List<Item> itemsToLookup = TShock.Utils.GetItemByIdOrName(sellItemRaw);
      if (itemsToLookup.Count == 0) {
        args.Player.SendErrorMessage($"Unable to guess a valid item type from \"{sellItemRaw}\".");
        return;
      }
      if (itemsToLookup.Count > 1) {
        args.Player.SendErrorMessage("Found multiple matches for the given <sell item>: " + string.Join(", ", itemsToLookup));
        return;
      }
      sellItem = itemsToLookup[0];

      bool isItemGroup = this.Config.TradeChestItemGroups.ContainsKey(payItemRaw);
      if (!isItemGroup) {
        itemsToLookup = TShock.Utils.GetItemByIdOrName(payItemRaw);
        if (itemsToLookup.Count == 0) {
          args.Player.SendErrorMessage($"Unable to guess a valid item type from \"{payItemRaw}\".");
          return;
        }
        if (itemsToLookup.Count > 1) {
          args.Player.SendErrorMessage("Found multiple matches for the given <pay item>: " + string.Join(", ", itemsToLookup));
          return;
        }
        payItemIdOrGroup = itemsToLookup[0].netID;

        if (sellItem.netID == (int)payItemIdOrGroup || (TerrariaUtils.Items.IsCoinType(sellItem.netID) && TerrariaUtils.Items.IsCoinType((int)payItemIdOrGroup))) {
          args.Player.SendErrorMessage("The item to be sold should be different from the item to pay with.");
          return;
        }
      } else {
        payItemIdOrGroup = payItemRaw;
      }

      CommandInteraction interaction = this.StartOrResetCommandInteraction(args.Player);
      interaction.TileEditCallback += (playerLocal, editType, tileId, location, objectStyle) => {
        if (
          editType != TileEditType.PlaceTile || 
          editType != TileEditType.PlaceWall || 
          editType != TileEditType.DestroyWall || 
          editType != TileEditType.PlaceActuator
        ) {
          this.TrySetUpTradeChest(playerLocal, location, sellAmount, sellItem.netID, payAmount, payItemIdOrGroup, lootLimit);

          playerLocal.SendTileSquare(location);
          return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
        }

        playerLocal.SendTileSquare(location);
        return new CommandInteractionResult { IsHandled = false, IsInteractionCompleted = false };
      };
      interaction.ChestOpenCallback += (playerLocal, location) => {
        this.TrySetUpTradeChest(playerLocal, location, sellAmount, sellItem.netID, payAmount, payItemIdOrGroup, lootLimit);
        return new CommandInteractionResult { IsHandled = true, IsInteractionCompleted = true };
      };
      interaction.TimeExpiredCallback += (playerLocal) => {
        playerLocal.SendMessage("Waited too long. No trade chest will be created.", Color.Red);
      };

      string priceInfo = "";
      #if SEconomy
      if (this.PluginCooperationHandler.IsSeconomyAvailable && this.Config.TradeChestPayment > 0 && !args.Player.Group.HasPermission(ProtectorPlugin.FreeTradeChests_Permission))
        priceInfo = $" This will cost you {this.Config.TradeChestPayment} {this.PluginCooperationHandler.Seconomy_MoneyName()}";
      #endif

      args.Player.SendInfoMessage("Open a chest to convert it into a trade chest." + priceInfo);
    }

    private bool TradeChestCommand_HelpCallback(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return true;

      int pageNumber;
      if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
        return false;

      switch (pageNumber) {
        default:
          args.Player.SendMessage("Command reference for /tradechest (Page 1 of 3)", Color.Lime);
          args.Player.SendMessage("/tradechest|/tchest <sell amount> <sell item> <pay amount> <pay item or group> [limit]", Color.White);
          args.Player.SendMessage("sell amount = The amount of items to sell to the player per click on the chest.", Color.LightGray);
          args.Player.SendMessage("sell item = The type of item to sell.", Color.LightGray);
          args.Player.SendMessage("pay amount = The amount of <pay item> to take from the player's inventory when they buy.", Color.LightGray);
          args.Player.SendMessage("pay item or group = The item type to take from the player when they buy. This may also be an item group name.", Color.LightGray);
          args.Player.SendMessage("limit = Optional. Amount of times a single player is allowed to buy from this chest.", Color.LightGray);
          break;
        case 2:
          args.Player.SendMessage("Converts a chest to a special chest which can sell its content to other players.", Color.LightGray);
          args.Player.SendMessage("You may also use this command to alter an existing trade chest.", Color.LightGray);
          args.Player.SendMessage("Other players buy from trade chests by just clicking and only the owner, shared users or admins can view the content of the trade chest.", Color.LightGray);
          args.Player.SendMessage("The payment of the purchasers is also stored in the chest, so make sure there's always enough space available. Also make sure", Color.LightGray);
          args.Player.SendMessage("the chest is always filled with enough goods or players will not be able to buy from you.", Color.LightGray);
          break;
        case 3:
          args.Player.SendMessage("Note that prefixes are not regarded for the payment or for the item to be sold.", Color.LightGray);
          break;
      }

      return true;
    }
    #endregion

    private readonly ConditionalWeakTable<TSPlayer, DPoint[]> scanChestsResults = new ConditionalWeakTable<TSPlayer, DPoint[]>();
    #region [Command Handling /scanchests]
    private void ScanChestsCommand_Exec(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      if (args.Parameters.Count == 0) {
        args.Player.SendErrorMessage("Proper syntax: /scanchests <item name> [<page>]");
        args.Player.SendInfoMessage("Type /scanchests help to get more information about this command.");
        return;
      }
      
      string itemNamePart;
      int pageNumber = 1;
      if (args.Parameters.Count == 1) {
        itemNamePart = args.Parameters[0];
      } else {
        string lastParam = args.Parameters[args.Parameters.Count - 1];
        if (lastParam.Length <= 2 && int.TryParse(lastParam, out pageNumber))
          itemNamePart = args.ParamsToSingleString(0, 1);
        else
          itemNamePart = args.ParamsToSingleString();

        if (pageNumber < 1) {
          args.Player.SendErrorMessage($"\"{lastParam}\" is not a valid page number.");
          return;
        }
      }

      List<Item> itemsToLookup = TShock.Utils.GetItemByIdOrName(itemNamePart);
      if (itemsToLookup.Count == 0) {
        args.Player.SendErrorMessage($"Unable to guess a valid item type from \"{itemNamePart}\".");
        return;
      }

      // DPoint is the chest location.
      List<Tuple<ItemData[], DPoint>> results = new List<Tuple<ItemData[], DPoint>>();
      foreach (IChest chest in this.ChestManager.EnumerateAllChests()) {
        List<ItemData> matchingItems = new List<ItemData>(
          from item in chest.Items
          where itemsToLookup.Any(li => li.netID == item.Type)
          select item);

        if (matchingItems.Count > 0)
          results.Add(new Tuple<ItemData[], DPoint>(matchingItems.ToArray(), chest.Location));
      }

      DPoint[] resultsChestLocations = results.Select(r => r.Item2).ToArray();
      this.scanChestsResults.Remove(args.Player);
      this.scanChestsResults.Add(args.Player, resultsChestLocations);

      PaginationTools.SendPage(args.Player, pageNumber, results, new PaginationTools.Settings {
        HeaderFormat = $"The Following Chests Contain \"{itemNamePart}\" (Page {{0}} of {{1}})",
        NothingToDisplayString = $"No chest contains items matching \"{itemNamePart}\"",
        LineTextColor = Color.LightGray,
        MaxLinesPerPage = 10,
        LineFormatter = (lineData, dataIndex, pageNumberLocal) => {
          var result = (lineData as Tuple<ItemData[], DPoint>);
          if (result == null)
            return null;

          ItemData[] foundItems = result.Item1;
          DPoint chestLocation = result.Item2;

          string foundItemsString = string.Join(" ", foundItems.Select(i => TShock.Utils.ItemTag(i.ToItem())));

          string chestOwner = "{not protected}";
          ProtectionEntry protection = this.ProtectionManager.GetProtectionAt(chestLocation);
          if (protection != null) {
            UserAccount tsUser = TShock.UserAccounts.GetUserAccountByID(protection.Owner);
            chestOwner = tsUser?.Name ?? $"{{user id: {protection.Owner}}}";
          }

          return new Tuple<string,Color>($"{dataIndex}. Chest owned by {TShock.Utils.ColorTag(chestOwner, Color.Red)} contains {foundItemsString}", Color.LightGray);
        }
      });

      if (results.Count > 0)
        args.Player.SendSuccessMessage("Type /tpchest <result index> to teleport to the respective chest.");
    }

    private bool ScanChestsCommand_HelpCallback(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return false;

      int pageNumber;
      if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
        return false;

      switch (pageNumber) {
        default:
          args.Player.SendMessage("Command reference for /scanchests (Page 1 of 1)", Color.Lime);
          args.Player.SendMessage("/scanchests <item name> [page]", Color.White);
          args.Player.SendMessage("Searches all chests in the current world for items matching the given name. The user will be able to teleport to the chests found by this command using /tpchest.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("item name = Part of name of the item(s) to check for.", Color.LightGray);  
          break;
      }

      return true;
    }
    #endregion

    #region [Command Handling /tpchest]
    private void TpChestCommand_Exec(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return;

      if (args.Parameters.Count != 1) {
        args.Player.SendErrorMessage("Proper syntax: /tpchest <result index>");
        args.Player.SendInfoMessage("Type /tpchest help to get more information about this command.");
        return;
      }

      DPoint[] chestLocations;
      if (!this.scanChestsResults.TryGetValue(args.Player, out chestLocations)) {
        args.Player.SendErrorMessage("You have to use /scanchests before using this command.");
        return;
      }

      int chestIndex;
      if (!int.TryParse(args.Parameters[0], out chestIndex) || chestIndex < 1 || chestIndex > chestLocations.Length) {
        args.Player.SendErrorMessage($"\"{args.Parameters[0]}\" is not a valid result index.");
        return;
      }

      DPoint chestLocation = chestLocations[chestIndex - 1];
      args.Player.Teleport(chestLocation.X * 16, chestLocation.Y * 16);
    }

    private bool TpChestCommand_HelpCallback(CommandArgs args) {
      if (args == null || this.IsDisposed)
        return false;

      int pageNumber;
      if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
        return false;

      switch (pageNumber) {
        default:
          args.Player.SendMessage("Command reference for /tpchest (Page 1 of 1)", Color.Lime);
          args.Player.SendMessage("/tpchest <result index>", Color.White);
          args.Player.SendMessage("Teleports you to a chest that was found by the /scanchests command.", Color.LightGray);
          args.Player.SendMessage(string.Empty, Color.LightGray);
          args.Player.SendMessage("result index = The index of the search result.", Color.LightGray);  
          break;
      }

      return true;
    }
    #endregion

    #region [Hook Handlers]
    public override bool HandleTileEdit(TSPlayer player, TileEditType editType, int blockType, DPoint location, int objectStyle) {
      return this.HandleTileEdit(player, editType, blockType, location, objectStyle, false);
    }

    /// <param name="isLate">
    ///   if <c>true</c>, then this tile edit handler was invoked after all other plugins.
    /// </param>
    public bool HandleTileEdit(TSPlayer player, TileEditType editType, int blockType, DPoint location, int objectStyle, bool isLate) {
      if (this.IsDisposed)
        return false;
      if (base.HandleTileEdit(player, editType, blockType, location, objectStyle))
        return true;
      
      switch (editType) {
        case TileEditType.PlaceTile: {
          if (!isLate)
            break;

          WorldGen.PlaceTile(location.X, location.Y, blockType, false, true, -1, objectStyle);
          NetMessage.SendData((int)PacketTypes.Tile, -1, player.Index, NetworkText.Empty, 1, location.X, location.Y, blockType, objectStyle);
          
          if (this.Config.AutoProtectedTiles[blockType])
            this.TryCreateAutoProtection(player, location);

          return true;
        }
        case TileEditType.TileKill:
        case TileEditType.TileKillNoItem: {
          // Is the tile really going to be destroyed or just being hit?
          //if (blockType != 0)
          //  break;

          ITile tile = TerrariaUtils.Tiles[location];
          bool isChest = (tile.type == TileID.Containers || tile.type == TileID.Containers2 || tile.type == TileID.Dressers);
          foreach (ProtectionEntry protection in this.ProtectionManager.EnumerateProtectionEntries(location)) {
            // If the protection is invalid, just remove it.
            if (!TerrariaUtils.Tiles.IsValidCoord(protection.TileLocation)) {
              this.ProtectionManager.RemoveProtection(TSPlayer.Server, protection.TileLocation, false);
              continue;
            }

            ITile protectedTile = TerrariaUtils.Tiles[protection.TileLocation];
            // If the protection is invalid, just remove it.
            if (!protectedTile.active() || protectedTile.type != protection.BlockType) {
              this.ProtectionManager.RemoveProtection(TSPlayer.Server, protection.TileLocation, false);
              continue;
            }

            if (
              protection.Owner == player.Account.ID || (
                this.Config.AutoDeprotectEverythingOnDestruction &&
                player.Group.HasPermission(ProtectorPlugin.ProtectionMaster_Permission)
              )
            ) {
              if (isChest) {
                bool isBankChest = (protection.BankChestKey != BankChestDataKey.Invalid);
                ObjectMeasureData measureData = TerrariaUtils.Tiles.MeasureObject(protection.TileLocation);
                DPoint chestLocation = measureData.OriginTileLocation;
                IChest chest = this.ChestManager.ChestFromLocation(chestLocation);

                if (chest == null)
                  return true;

                if (isBankChest) {
                  this.DestroyBlockOrObject(chestLocation);
                } else {
                  for (int i = 0; i < Chest.maxItems; i++) {
                    if (chest.Items[i].StackSize > 0)
                      return true;
                  }
                }
              }
              this.ProtectionManager.RemoveProtection(player, protection.TileLocation, false);
          
              if (this.Config.NotifyAutoDeprotections)
                player.SendWarningMessage("The object is not protected anymore.");
            } else {
              player.SendErrorMessage("The object is protected.");

              if (protection.TradeChestData != null)
                player.SendWarningMessage("If you want to trade with this chest, right click it first.");

              player.SendTileSquare(location);
              return true;
            }
          }

          // note: if the chest was a bank chest, then it was already removed
          if (isChest && TerrariaUtils.Tiles[location].active()) {
            ObjectMeasureData measureData = TerrariaUtils.Tiles.MeasureObject(location);
            DPoint chestLocation = measureData.OriginTileLocation;
            IChest chest = this.ChestManager.ChestFromLocation(chestLocation);
            if (chest != null) {
              // Don't allow removing of non empty chests.
              for (int i = 0; i < Chest.maxItems; i++) {
                if (chest.Items[i].StackSize > 0)
                  return true;
              }

              this.DestroyBlockOrObject(chestLocation);
              return true;
            }
          }

          break;
        }
        case TileEditType.PlaceWire:
        case TileEditType.PlaceWireBlue:
        case TileEditType.PlaceWireGreen:
        case TileEditType.PlaceWireYellow:
        case TileEditType.PlaceActuator:
        case TileEditType.DestroyWire:
        case TileEditType.DestroyWireBlue:
        case TileEditType.DestroyWireGreen:
        case TileEditType.DestroyWireYellow:
        case TileEditType.DestroyActuator:
          if (this.Config.AllowWiringProtectedBlocks)
            break;

          if (this.CheckProtected(player, location, false)) {
            player.SendTileSquare(location);
            return true;
          }

          break;
        case TileEditType.PokeLogicGate:
        case TileEditType.Actuate:
          if (this.CheckProtected(player, location, false)) {
            player.SendTileSquare(location);
            return true;
          }

          break;
      }
      
      return false;
    }

    public virtual bool HandleObjectPlacement(TSPlayer player, DPoint location, int blockType, int objectStyle, int alternative, int random, bool direction) {
      if (this.IsDisposed)
        return false;
      
      int directionInt = direction ? 1 : -1;
      WorldGen.PlaceObject(location.X, location.Y, blockType, false, objectStyle, alternative, random, directionInt);
      NetMessage.SendObjectPlacment(player.Index, location.X, location.Y, blockType, objectStyle, alternative, random, directionInt);

      if (this.Config.AutoProtectedTiles[blockType])
        this.TryCreateAutoProtection(player, location);

      return true;
    }

    public virtual bool HandleChestPlace(TSPlayer player, DPoint location, int storageType, int storageStyle) {
      if (this.IsDisposed)
        return false;  
    
      ushort tileToPlace = TileID.Containers;
      if (storageType == 2)
        tileToPlace = TileID.Dressers;
      else if (storageType == 4)
        tileToPlace = TileID.Containers2;

      try {
        this.ChestManager.PlaceChest(tileToPlace, storageStyle, location);
      } catch (LimitEnforcementException ex) {
        player.SendTileSquare(location.X, location.Y, 2);
        player.SendErrorMessage("The limit of maximum possible chests has been reached. Please report this to a server administrator.");
        this.PluginTrace.WriteLineWarning($"Chest limit of {Main.chest.Length + this.Config.MaxProtectorChests - 1} has been reached!");
      }

      if (this.Config.AutoProtectedTiles[tileToPlace])
        this.TryCreateAutoProtection(player, location);

      return true;
    }

    private bool TryCreateAutoProtection(TSPlayer forPlayer, DPoint location) {
      try {
        this.ProtectionManager.CreateProtection(forPlayer, location, false);
        
        if (this.Config.NotifyAutoProtections)
          forPlayer.SendSuccessMessage("This object is now protected.");

        return true;
      } catch (PlayerNotLoggedInException) {
        forPlayer.SendWarningMessage("This object could'nt be protected because you're not logged in.");
      } catch (LimitEnforcementException) {
        forPlayer.SendWarningMessage("This object couldn't be protected because you've reached your protection capacity.");
      } catch (TileProtectedException) {
        this.PluginTrace.WriteLineError("Error: A block was tried to be auto protected where tile placement should not be possible.");
      } catch (AlreadyProtectedException) {
        this.PluginTrace.WriteLineError("Error: A block was tried to be auto protected on the same position of an existing protection.");
      } catch (Exception ex) {
        this.PluginTrace.WriteLineError("Unexpected exception was thrown during auto protection: \n" + ex);
      }

      return false;
    }

    public virtual bool HandleChestRename(TSPlayer player, int chestIndex, string newName) {
      if (this.IsDisposed)
        return false;

      IChest chest = this.LastOpenedChest(player);
      if (chest == null)
        return true;

      bool isAllowed = true;
      if (this.CheckProtected(player, chest.Location, true)) {
        player.SendErrorMessage("You have to be the owner of the chest in order to rename it.");
        isAllowed = false;
      }

      if (this.Config.LoginRequiredForChestUsage && !player.IsLoggedIn) {
        player.SendErrorMessage("You have to be logged in in order to rename chests.");
        isAllowed = false;
      }

      if (!isAllowed) {
        string originalName = string.Empty;
        if (chest.IsWorldChest)
          originalName = chest.Name;

        // The name change will already have happened locally for the player, so gotta send the original name back to them.
        player.SendData(PacketTypes.ChestName, originalName, chest.Index, chest.Location.X, chest.Location.Y);
        return true;
      } else {
        // Only world chests can have names, so attempt to convert it into one.
        if (!chest.IsWorldChest && !this.TrySwapChestData(null, chest.Location, out chest)) {
          player.SendErrorMessage("The maximum amount of named chests for this world has been reached.");
          return true;
        }
        
        chest.Name = newName;
        player.SendData(PacketTypes.ChestName, chest.Name, chest.Index, chest.Location.X, chest.Location.Y);

        return true;
      }
    }

    // Note: chestLocation is always {0, 0}. chestIndex == -1 chest, piggy, safe closed. chestIndex == -2 piggy bank opened, chestIndex == -3 safe opened.
    public virtual bool HandleChestOpen(TSPlayer player, int chestIndex, DPoint chestLocation) {
      if (this.IsDisposed)
        return false;
      bool isChestClosed = (chestIndex == -1);
      if (!isChestClosed)
        return false;

      IChest chest = this.LastOpenedChest(player);
      if (chest == null)
        return false;

      ITile chestTile = TerrariaUtils.Tiles[chest.Location];
      bool isLocked;
      ChestStyle chestStyle = TerrariaUtils.Tiles.GetChestStyle(chestTile, out isLocked);
      if (isLocked)
        return false;

      ProtectionEntry protection = null;
      foreach (ProtectionEntry enumProtection in this.ProtectionManager.EnumerateProtectionEntries(chest.Location)) {
        protection = enumProtection;
        break;
      }

      // Convert this chest to a world chest if it contains a key of night/light only, so that Terraria can do its
      // thing with it.
      if (!chest.IsWorldChest) {
        int containedLightNightKeys = 0;
        bool isOtherwiseEmpty = true;
        for (int i = 0; i < Chest.maxItems; i++) {
          ItemData chestItem = chest.Items[i];
          if (chestItem.StackSize == 1 && (chestItem.Type == ItemID.NightKey || chestItem.Type == ItemID.LightKey)) {
            containedLightNightKeys++;
          } else if (chestItem.StackSize > 0) {
            isOtherwiseEmpty = false;
            break;
          }
        }

        if (containedLightNightKeys == 1 && isOtherwiseEmpty) {
          this.TrySwapChestData(null, chest.Location, out chest);
          player.TPlayer.lastChest = chest.Index;
        }
      }

      if (protection == null)
        return false;

      if (protection.RefillChestData != null) {
        if (protection.RefillChestData.AutoEmpty && !player.Group.HasPermission(ProtectorPlugin.ProtectionMaster_Permission)) {
          for (int i = 0; i < Chest.maxItems; i++)
            chest.Items[i] = ItemData.None;
        }

        if (
          protection.RefillChestData.AutoLock && 
          TerrariaUtils.Tiles.IsChestStyleLockable(chestStyle) && 
          protection.RefillChestData.RefillTime == TimeSpan.Zero
        )
          TerrariaUtils.Tiles.LockChest(chest.Location);
      }

      return false;
    }

    public override bool HandleChestGetContents(TSPlayer player, DPoint location) {
      if (this.IsDisposed)
        return false;

      return this.HandleChestGetContents(player, location, skipInteractions: false);
    }

    public bool HandleChestGetContents(TSPlayer player, DPoint location, bool skipInteractions) {
      if (this.IsDisposed)
        return false;
      if (!skipInteractions && base.HandleChestGetContents(player, location))
        return true;
      bool isDummyChest = (location.X == 0);
      if (isDummyChest)
        return true;
      if (!TerrariaUtils.Tiles[location].active())
        return true;
      if (this.Config.LoginRequiredForChestUsage && !player.IsLoggedIn) {
        player.SendErrorMessage("You have to be logged in to make use of chests.");
        return true;
      }

      if (this.Config.DungeonChestProtection && !NPC.downedBoss3 && !player.Group.HasPermission(ProtectorPlugin.ProtectionMaster_Permission)) {
        ChestKind kind = TerrariaUtils.Tiles.GuessChestKind(location);
        if (kind == ChestKind.DungeonChest || kind == ChestKind.HardmodeDungeonChest) {
          player.SendErrorMessage("Skeletron has not been defeated yet.");
          return true;
        }
      }
      
      ProtectionEntry protection = null;
      // Only need the first enumerated entry as we don't need the protections of adjacent blocks.
      foreach (ProtectionEntry enumProtection in this.ProtectionManager.EnumerateProtectionEntries(location)) {
        protection = enumProtection;
        break;
      }
      
      DPoint chestLocation = TerrariaUtils.Tiles.MeasureObject(location).OriginTileLocation;

      IChest chest = this.ChestManager.ChestFromLocation(chestLocation, player);
      if (chest == null)
        return true;

      if (this.IsChestInUse(player, chest)) {
        player.SendErrorMessage("Another player is already viewing the content of this chest.");
        return true;
      }

      if (protection != null) {
        bool isTradeChest = (protection.TradeChestData != null);
        if (!this.ProtectionManager.CheckProtectionAccess(protection, player)) {
          if (isTradeChest)
            this.InitTrade(player, chest, protection);
          else
            player.SendErrorMessage("This chest is protected.");

          return true;
        }

        if (isTradeChest) {
          Item sellItem = new Item();
          sellItem.netDefaults(protection.TradeChestData.ItemToSellId);
          sellItem.stack = protection.TradeChestData.ItemToSellAmount;

          string paymentDescription = this.PaymentItemDescription(protection.TradeChestData);
          player.SendMessage($"This is a trade chest selling {TShock.Utils.ItemTag(sellItem)} for {paymentDescription}", Color.OrangeRed);
          player.SendMessage("You have access to it, so you can modify it any time.", Color.LightGray);
        }
      
        if (protection.RefillChestData != null) {
          RefillChestMetadata refillChest = protection.RefillChestData;
          if (this.CheckRefillChestLootability(refillChest, player)) {
            if (refillChest.OneLootPerPlayer)
              player.SendMessage("You can loot this chest only once.", Color.OrangeRed);
          } else {
            return true; 
          }

          if (refillChest.RefillTime != TimeSpan.Zero) {
            lock (this.ChestManager.RefillTimers) {
              if (this.ChestManager.RefillTimers.IsTimerRunning(refillChest.RefillTimer)) {
                TimeSpan timeLeft = (refillChest.RefillStartTime + refillChest.RefillTime) - DateTime.Now;
                player.SendMessage($"This chest will refill in {timeLeft.ToLongString()}.", Color.OrangeRed);
              } else {
                player.SendMessage("This chest will refill its content.", Color.OrangeRed);
              }
            }
          } else {
            player.SendMessage("This chest will refill its content.", Color.OrangeRed);
          }
        }
      }
  
      lock (ChestManager.DummyChest) {
        if (chest.IsWorldChest) {
          ChestManager.DummyChest.name = chest.Name;
          player.TPlayer.chest = chest.Index;
        } else {
          Main.chest[ChestManager.DummyChestIndex] = ChestManager.DummyChest;
          player.TPlayer.chest = -1;
        }

        for (int i = 0; i < Chest.maxItems; i++) {
          ChestManager.DummyChest.item[i] = chest.Items[i].ToItem();
          player.SendData(PacketTypes.ChestItem, string.Empty, player.TPlayer.chest, i);
        }

        ChestManager.DummyChest.x = chestLocation.X;
        ChestManager.DummyChest.y = chestLocation.Y;
        player.SendData(PacketTypes.ChestOpen, string.Empty, player.TPlayer.chest);
        player.SendData(PacketTypes.SyncPlayerChestIndex, string.Empty, player.Index, player.TPlayer.chest);

        ChestManager.DummyChest.x = 0;
      }

      DPoint oldChestLocation;
      if (this.PlayerIndexChestDictionary.TryGetValue(player.Index, out oldChestLocation)) {
        this.PlayerIndexChestDictionary.Remove(player.Index);
        this.ChestPlayerIndexDictionary.Remove(oldChestLocation);
      }

      if (!chest.IsWorldChest) {
        this.PlayerIndexChestDictionary[player.Index] = chestLocation;
        this.ChestPlayerIndexDictionary[chestLocation] = player.Index;
      }
      
      return false;
    }

    private string PaymentItemDescription(TradeChestMetadata tradeChestData) {
      bool isPayGroup = tradeChestData.ItemToPayGroup != null;
      if (!isPayGroup) {
        Item payItem = new Item();
        payItem.netDefaults(tradeChestData.ItemToPayId);
        payItem.stack = tradeChestData.ItemToPayAmount;

        return TShock.Utils.ItemTag(payItem);
      } else {
        string groupName = tradeChestData.ItemToPayGroup;
        HashSet<int> groupItemIds;
        if (this.Config.TradeChestItemGroups.TryGetValue(groupName.ToLowerInvariant(), out groupItemIds)) {
          StringBuilder builder = new StringBuilder();
          builder.Append(tradeChestData.ItemToPayGroup).Append(' ').Append('(');

          bool isFirst = true;
          foreach (int itemId in groupItemIds) {
            if (!isFirst)
              builder.Append(' ');

            Item item = new Item();
            item.netDefaults(itemId);
            item.stack = tradeChestData.ItemToPayAmount;

            builder.Append(TShock.Utils.ItemTag(item));
            isFirst = false;
          }

          builder.Append(')');
          return builder.ToString();
        } else {
          return $"{{non existing group: {groupName}}}";
        }
      }
    }

    private bool IsChestInUse(TSPlayer player, IChest chest) {
      int usingPlayerIndex = -1;
      if (chest.IsWorldChest)
        usingPlayerIndex = Chest.UsingChest(chest.Index);

      return
        (usingPlayerIndex != -1 && usingPlayerIndex != player.Index) ||
        (this.ChestPlayerIndexDictionary.TryGetValue(chest.Location, out usingPlayerIndex) && usingPlayerIndex != player.Index);
    }

    private void InitTrade(TSPlayer player, IChest chest, ProtectionEntry protection) {
      TradeChestMetadata tradeChestData = protection.TradeChestData;
      Item sellItem = new Item();
      sellItem.netDefaults(tradeChestData.ItemToSellId);
      sellItem.stack = tradeChestData.ItemToSellAmount;

      string paymentDescription = this.PaymentItemDescription(tradeChestData);

      player.SendMessage($"This is a trade chest owned by {TShock.Utils.ColorTag(GetUserName(protection.Owner), Color.Red)}.", Color.LightGray);

      Inventory chestInventory = new Inventory(chest.Items, specificPrefixes: false);
      int stock = chestInventory.Amount(sellItem.netID);
      if (stock < sellItem.stack) {
        player.SendMessage($"It was selling {TShock.Utils.ItemTag(sellItem)} for {paymentDescription} but it is out of stock.", Color.LightGray);
        return;
      }

      player.SendMessage($"Click again to buy {TShock.Utils.ItemTag(sellItem)} for {paymentDescription}", Color.LightGray);

      CommandInteraction interaction = this.StartOrResetCommandInteraction(player);
      interaction.ChestOpenCallback += (playerLocal, chestLocation) => {
        bool complete = false;

        bool wasThisChestHit = (chestLocation == chest.Location);
        if (wasThisChestHit) {
          Item payItem = new Item();
          // this is important to check, otherwise players could use trade chests to easily duplicate items
          if (!this.IsChestInUse(playerLocal, chest)) {
            if (tradeChestData.ItemToPayGroup == null) {
              
              payItem.netDefaults(tradeChestData.ItemToPayId);
              payItem.stack = tradeChestData.ItemToPayAmount;

              this.PerformTrade(player, protection, chestInventory, sellItem, payItem);
            } else {
              Inventory playerInventory = new Inventory(new PlayerItemsAdapter(player.Index, player.TPlayer.inventory, 0, 53), specificPrefixes: false);
              bool performedTrade = false;
              foreach (int payItemId in this.Config.TradeChestItemGroups[tradeChestData.ItemToPayGroup]) {
                int amountInInventory = playerInventory.Amount(payItemId);
                if (amountInInventory >= tradeChestData.ItemToPayAmount) {
                  payItem.netDefaults(payItemId);
                  payItem.stack = tradeChestData.ItemToPayAmount;

                  this.PerformTrade(player, protection, chestInventory, sellItem, payItem);
                  performedTrade = true;
                  break;
                }
              }

              if (!performedTrade)
                playerLocal.SendErrorMessage($"You don't have enought of any of the {paymentDescription}");
            }
          } else {
            player.SendErrorMessage("Another player is currently viewing the content of this chest.");
          }
        } else {
          this.HandleChestGetContents(playerLocal, chestLocation, skipInteractions: true);
          complete = true;
        }

        playerLocal.SendTileSquare(chest.Location);
        return new CommandInteractionResult {IsHandled = true, IsInteractionCompleted = complete};
      };
    }

    private void PerformTrade(TSPlayer player, ProtectionEntry protection, Inventory chestInventory, Item sellItem, Item payItem) {
      Inventory playerInventory = new Inventory(new PlayerItemsAdapter(player.Index, player.TPlayer.inventory, 0, 53), specificPrefixes: false);

      ItemData sellItemData = ItemData.FromItem(sellItem);
      ItemData payItemData = ItemData.FromItem(payItem);
      ItemData?[] playerInvUpdates;
      try {
        playerInvUpdates = playerInventory.Remove(payItemData);
        playerInventory.Add(playerInvUpdates, sellItemData);
      } catch (InvalidOperationException) {
        player.SendErrorMessage($"You either don't have the needed {TShock.Utils.ItemTag(payItem)} to trade {TShock.Utils.ItemTag(sellItem)} or your inventory is full.");
        return;
      }

      bool isRefillChest = (protection.RefillChestData != null);
      ItemData?[] chestInvUpdates = null;
      try {
        if (!isRefillChest) {
          chestInvUpdates = chestInventory.Remove(sellItemData);
          chestInventory.Add(chestInvUpdates, payItemData);
        }
      } catch (InvalidOperationException) {
        player.SendErrorMessage("The items in the trade chest are either sold out or there's no space in it to add your payment.");
        return;
      }

      try {
        protection.TradeChestData.AddOrUpdateLooter(player.Account.ID);
      } catch (InvalidOperationException) {
        player.SendErrorMessage($"The vendor doesn't allow more than {protection.TradeChestData.LootLimitPerPlayer} trades per player.");
        return;
      }

      playerInventory.ApplyUpdates(playerInvUpdates);
      if (!isRefillChest)
        chestInventory.ApplyUpdates(chestInvUpdates);

      protection.TradeChestData.AddJournalEntry(player.Name, sellItem, payItem);
      player.SendSuccessMessage($"You've just traded {TShock.Utils.ItemTag(sellItem)} for {TShock.Utils.ItemTag(payItem)} from {TShock.Utils.ColorTag(GetUserName(protection.Owner), Color.Red)}.");
    }

    public virtual bool HandleChestModifySlot(TSPlayer player, int chestIndex, int slotIndex, ItemData newItem) {
      if (this.IsDisposed)
        return false;

      // Get the chest location of the chest the player has last opened.
      IChest chest = this.LastOpenedChest(player);
      if (chest == null)
        return true;

      ProtectionEntry protection = null;
      // Only need the first enumerated entry as we don't need the protections of adjacent blocks.
      foreach (ProtectionEntry enumProtection in this.ProtectionManager.EnumerateProtectionEntries(chest.Location)) {
        protection = enumProtection;
        break;
      }

      bool playerHasAccess = true;
      if (protection != null)
        playerHasAccess = this.ProtectionManager.CheckProtectionAccess(protection, player, false);

      if (!playerHasAccess)
        return true;

      if (protection != null && protection.RefillChestData != null) {
        RefillChestMetadata refillChest = protection.RefillChestData;
        // The player who set up the refill chest or masters shall modify its contents.
        if (
          this.Config.AllowRefillChestContentChanges &&
          (refillChest.Owner == player.Account.ID || player.Group.HasPermission(ProtectorPlugin.ProtectionMaster_Permission))
        ) {
          refillChest.RefillItems[slotIndex] = newItem;

          this.ChestManager.TryRefillChest(chest, refillChest);

          if (refillChest.RefillTime == TimeSpan.Zero) {
            player.SendSuccessMessage("The content of this refill chest was updated.");
          } else {
            lock (this.ChestManager.RefillTimers) {
              if (this.ChestManager.RefillTimers.IsTimerRunning(refillChest.RefillTimer))
                this.ChestManager.RefillTimers.RemoveTimer(refillChest.RefillTimer);
            }

            player.SendSuccessMessage("The content of this refill chest was updated and the timer was reset.");
          }

          return false;
        }

        if (refillChest.OneLootPerPlayer || refillChest.RemainingLoots > 0) {
          //Contract.Assert(refillChest.Looters != null);
          if (!refillChest.Looters.Contains(player.Account.ID)) {
            refillChest.Looters.Add(player.Account.ID);

            if (refillChest.RemainingLoots > 0)
              refillChest.RemainingLoots--;
          }
        }

        // As the first item is taken out, we start the refill timer.
        ItemData oldItem = chest.Items[slotIndex];
        if (newItem.Type == 0 || (newItem.Type == oldItem.Type && newItem.StackSize <= oldItem.StackSize)) {
          // TODO: Bad code, refill timers shouldn't be public at all.
          lock (this.ChestManager.RefillTimers)
            this.ChestManager.RefillTimers.StartTimer(refillChest.RefillTimer);
        } else {
          player.SendErrorMessage("You can not put items into this chest.");
          return true;
        }
      } else if (protection != null && protection.BankChestKey != BankChestDataKey.Invalid) {
        BankChestDataKey bankChestKey = protection.BankChestKey;
        this.ServerMetadataHandler.EnqueueUpdateBankChestItem(bankChestKey, slotIndex, newItem);
      }

      chest.Items[slotIndex] = newItem;
      return true;
    }

    public virtual bool HandleChestUnlock(TSPlayer player, DPoint chestLocation) {
      if (this.IsDisposed)
        return false;
 
      ProtectionEntry protection = null;
      // Only need the first enumerated entry as we don't need the protections of adjacent blocks.
      foreach (ProtectionEntry enumProtection in this.ProtectionManager.EnumerateProtectionEntries(chestLocation)) {
        protection = enumProtection;
        break;
      }
      if (protection == null)
        return false;
      
      bool undoUnlock = false;
      if (!this.ProtectionManager.CheckProtectionAccess(protection, player, false)) {
        player.SendErrorMessage("This chest is protected, you can't unlock it.");
        undoUnlock = true;
      }
      if (protection.RefillChestData != null && !this.CheckRefillChestLootability(protection.RefillChestData, player))
        undoUnlock = true;

      if (undoUnlock) {
        bool dummy;
        ChestStyle style = TerrariaUtils.Tiles.GetChestStyle(TerrariaUtils.Tiles[chestLocation], out dummy);
        if (style != ChestStyle.ShadowChest) {
          int keyType = TerrariaUtils.Tiles.KeyItemTypeFromChestStyle(style);
          if (keyType != 0) {
            int itemIndex = Item.NewItem(chestLocation.X * TerrariaUtils.TileSize, chestLocation.Y * TerrariaUtils.TileSize, 0, 0, keyType);
            player.SendData(PacketTypes.ItemDrop, string.Empty, itemIndex);
          }
        }

        player.SendTileSquare(chestLocation, 3);
        return true;
      }

      return false;
    }

    public override bool HandleSignEdit(TSPlayer player, int signIndex, DPoint location, string newText) {
      if (this.IsDisposed)
        return false;
      if (base.HandleSignEdit(player, signIndex, location, newText))
        return true;

      return this.CheckProtected(player, location, false);
    }

    public override bool HandleHitSwitch(TSPlayer player, DPoint location) {
      if (this.IsDisposed)
        return false;
      if (base.HandleHitSwitch(player, location))
        return true;
      
      if (this.CheckProtected(player, location, false)) {
        player.SendTileSquare(location, 3);
        return true;
      }

      return false;
    }

    public virtual bool HandleDoorUse(TSPlayer player, DPoint location, bool isOpening, Direction direction) {
      if (this.IsDisposed)
        return false;
      if (this.CheckProtected(player, location, false)) {
        player.SendTileSquare(location, 5);
        return true;
      }

      return false;
    }

    public virtual bool HandlePlayerSpawn(TSPlayer player, DPoint spawnTileLocation) {
      if (this.IsDisposed)
        return false;

      bool isBedSpawn = (spawnTileLocation.X != -1 || spawnTileLocation.Y != -1);
      RemoteClient client = Netplay.Clients[player.Index];
      if (!isBedSpawn || client.State <= 3)
        return false;

      DPoint bedTileLocation = new DPoint(spawnTileLocation.X, spawnTileLocation.Y - 1);
      ITile spawnTile = TerrariaUtils.Tiles[bedTileLocation];
      bool isInvalidBedSpawn = (!spawnTile.active() || spawnTile.type != TileID.Beds);
      
      bool allowNewSpawnSet = true;
      if (isInvalidBedSpawn) {
        player.Teleport(Main.spawnTileX * TerrariaUtils.TileSize, (Main.spawnTileY - 3) * TerrariaUtils.TileSize);
        this.PluginTrace.WriteLineWarning($"Player \"{player.Name}\" tried to spawn on an invalid location.");

        allowNewSpawnSet = false;
      } else if (this.Config.EnableBedSpawnProtection) {
        if (this.CheckProtected(player, bedTileLocation, false)) {
          player.SendErrorMessage("The bed you have set spawn at is protected, you can not spawn there.");
          player.SendErrorMessage("You were transported to your last valid spawn location instead.");

          if (player.TPlayer.SpawnX == -1 && player.TPlayer.SpawnY == -1)
            player.Teleport(Main.spawnTileX * TerrariaUtils.TileSize, (Main.spawnTileY - 3) * TerrariaUtils.TileSize);
          else
            player.Teleport(player.TPlayer.SpawnX * TerrariaUtils.TileSize, (player.TPlayer.SpawnY - 3) * TerrariaUtils.TileSize);

          allowNewSpawnSet = false;
        }
      }

      if (allowNewSpawnSet) {
        player.TPlayer.SpawnX = spawnTileLocation.X;
        player.TPlayer.SpawnY = spawnTileLocation.Y;
        player.sX = spawnTileLocation.X;
        player.sY = spawnTileLocation.X;
      }

      player.TPlayer.Spawn(PlayerSpawnContext.ReviveFromDeath);
      NetMessage.SendData(12, -1, player.Index, NetworkText.Empty, player.Index);
      player.Dead = false;

      return true;
    }

    public virtual bool HandleQuickStackNearby(TSPlayer player, int playerSlotIndex) {
      if (this.IsDisposed)
        return false;

      Item item = player.TPlayer.inventory[playerSlotIndex];
      // TODO: fix this
      //this.PutItemInNearbyChest(player, item, player.TPlayer.Center);

      player.SendData(PacketTypes.PlayerSlot, string.Empty, player.Index, playerSlotIndex, item.prefix);
      return true;
    }

    // Modded version of Terraria's original method.
    private Item PutItemInNearbyChest(TSPlayer player, Item itemToStore, Vector2 position) {
      bool isStored = false;

      for (int i = 0; i < Main.chest.Length; i++) {
        if (i == ChestManager.DummyChestIndex)
          continue;
        Chest tChest = Main.chest[i];
        if (tChest == null || !Main.tile[tChest.x, tChest.y].active())
          continue;

        bool isPlayerInChest = Main.player.Any((p) => p.chest == i);
        if (!isPlayerInChest) {
          IChest chest = new ChestAdapter(i, tChest);
          isStored = this.TryToStoreItemInNearbyChest(player, position, itemToStore, chest);
          if (isStored)
            break;
        }
      }

      if (!isStored) {
        lock (this.WorldMetadata.ProtectorChests) {
          foreach (DPoint chestLocation in this.WorldMetadata.ProtectorChests.Keys) {
            if (!TerrariaUtils.Tiles[chestLocation].active())
              continue;

            bool isPlayerInChest = this.ChestPlayerIndexDictionary.ContainsKey(chestLocation);
            if (!isPlayerInChest) {
              IChest chest = this.WorldMetadata.ProtectorChests[chestLocation];
              isStored = this.TryToStoreItemInNearbyChest(player, position, itemToStore, chest);
              if (isStored)
                break;
            }
          }
        }
      }

      return itemToStore;
    }

    // Modded version of Terraria's Original
    private bool TryToStoreItemInNearbyChest(TSPlayer player, Vector2 playerPosition, Item itemToStore, IChest chest) {
      float quickStackRange = this.Config.QuickStackNearbyRange * 16;  
    
      if (Chest.IsLocked(chest.Location.X, chest.Location.Y))
        return false;

      Vector2 vector2 = new Vector2((chest.Location.X * 16 + 16), (chest.Location.Y * 16 + 16));
      if ((vector2 - playerPosition).Length() > quickStackRange)
        return false;

      ProtectionEntry protection;
      if (this.ProtectionManager.CheckBlockAccess(player, chest.Location, false, out protection)) {
        bool isRefillChest = (protection != null && protection.RefillChestData != null);
        bool isTradeChest = (protection != null && protection.TradeChestData != null);

        if (!isRefillChest && !isTradeChest) { 
          bool isBankChest = (protection != null && protection.BankChestKey != BankChestDataKey.Invalid);
          bool hasEmptySlot = false;
          bool containsSameItem = false;

          for (int i = 0; i < Chest.maxItems; i++) {
            ItemData chestItem = chest.Items[i];

            if (chestItem.Type <= 0 || chestItem.StackSize <= 0)
              hasEmptySlot = true;
            else if (itemToStore.netID == chestItem.Type) {
              int remainingStack = itemToStore.maxStack - chestItem.StackSize;

              if (remainingStack > 0) {
                if (remainingStack > itemToStore.stack)
                  remainingStack = itemToStore.stack;

                itemToStore.stack = itemToStore.stack - remainingStack;
                //chestItem.StackSize = chestItem.StackSize + remainingStack;
                if (isBankChest)
                  this.ServerMetadataHandler.EnqueueUpdateBankChestItem(protection.BankChestKey, i, chestItem);

                if (itemToStore.stack <= 0) {
                  itemToStore.SetDefaults();
                  return true;
                }
              }

              containsSameItem = true;
            }
          }
          if (containsSameItem && hasEmptySlot && itemToStore.stack > 0) {
            for (int i = 0; i < Chest.maxItems; i++) {
              ItemData chestItem = chest.Items[i];

              if (chestItem.Type == 0 || chestItem.StackSize == 0) {
                ItemData itemDataToStore = ItemData.FromItem(itemToStore);
                chest.Items[i] = itemDataToStore;

                if (isBankChest)
                  this.ServerMetadataHandler.EnqueueUpdateBankChestItem(protection.BankChestKey, i, itemDataToStore);

                itemToStore.SetDefaults();
                return true;
              }
            }
          }
        }
      }

      return false;
    }

    private IChest LastOpenedChest(TSPlayer player) {
      DPoint chestLocation;
      int chestIndex = player.TPlayer.chest;

      bool isWorldDataChest = (chestIndex != -1 && chestIndex != ChestManager.DummyChestIndex);
      if (isWorldDataChest) {
        bool isPiggyOrSafeOrForge = (chestIndex == -2 || chestIndex == -3 || chestIndex == -4);
        if (isPiggyOrSafeOrForge)
          return null;

        Chest chest = Main.chest[chestIndex];

        if (chest != null)
          return new ChestAdapter(chestIndex, chest);
        else
          return null;
      } else if (this.PlayerIndexChestDictionary.TryGetValue(player.Index, out chestLocation)) {
        lock (this.WorldMetadata.ProtectorChests)
          return this.WorldMetadata.ProtectorChests[chestLocation];
      } else {
        return null;
      }      
    }
    #endregion

    private bool TryCreateProtection(TSPlayer player, DPoint tileLocation, bool sendFailureMessages = true) {
      if (!player.IsLoggedIn) {
        if (sendFailureMessages)
          player.SendErrorMessage("You're not logged in.");

        return false;
      }

      try {
        this.ProtectionManager.CreateProtection(player, tileLocation);
        player.SendSuccessMessage("This object is now protected.");

        return true;
      } catch (ArgumentException ex) {
        if (ex.ParamName == "tileLocation" && sendFailureMessages)
          player.SendErrorMessage("Nothing to protect here.");

        throw;
      } catch (InvalidBlockTypeException ex) {
        if (sendFailureMessages) {
          string message;
          if (TerrariaUtils.Tiles.IsSolidBlockType(ex.BlockType, true))
            message = "Blocks of this type can not be protected.";
          else
            message = "Objects of this type can not be protected.";
        
          player.SendErrorMessage(message);
        }
      } catch (LimitEnforcementException) {
        if (sendFailureMessages) {
          player.SendErrorMessage(
            $"Protection capacity reached: {this.Config.MaxProtectionsPerPlayerPerWorld}.");
        }
      } catch (AlreadyProtectedException) {
        if (sendFailureMessages)
          player.SendErrorMessage("This object is already protected.");
      } catch (TileProtectedException) {
        if (sendFailureMessages)
          player.SendErrorMessage("This object is protected by someone else or is inside of a protected region.");
      } catch (Exception ex) {
        player.SendErrorMessage("An unexpected internal error occured.");
        this.PluginTrace.WriteLineError("Error on creating protection: ", ex.ToString());

      }

      return false;
    }

    private bool TryAlterProtectionShare(
      TSPlayer player, DPoint tileLocation, bool isShareOrUnshare, bool isGroup, bool isShareAll, 
      object shareTarget, string shareTargetName, bool sendFailureMessages = true
    ) {
      if (!player.IsLoggedIn) {
        if (sendFailureMessages)
          player.SendErrorMessage("You're not logged in.");

        return false;
      }

      try {
        if (isShareAll) {
          this.ProtectionManager.ProtectionShareAll(player, tileLocation, isShareOrUnshare, true);

          if (isShareOrUnshare) {
            player.SendSuccessMessage($"This object is now shared with everyone.");
          } else {
            player.SendSuccessMessage($"This object is not shared with everyone anymore.");
          }
        } else if (!isGroup) {
          this.ProtectionManager.ProtectionShareUser(player, tileLocation, (int)shareTarget, isShareOrUnshare, true);

          if (isShareOrUnshare) {
            player.SendSuccessMessage($"This object is now shared with player \"{shareTargetName}\".");
          } else {
            player.SendSuccessMessage($"This object is not shared with player \"{shareTargetName}\" anymore.");
          }
        } else {
          this.ProtectionManager.ProtectionShareGroup(player, tileLocation, (string)shareTarget, isShareOrUnshare, true);

          if (isShareOrUnshare) {
            player.SendSuccessMessage($"This object is now shared with group \"{shareTargetName}\".");
          } else {
            player.SendSuccessMessage($"This object is not shared with group \"{shareTargetName}\" anymore.");
          }
        }

        return true;
      } catch (ProtectionAlreadySharedException) {
        if (isShareAll) {
          player.SendErrorMessage($"This object is already shared with everyone.");
        } else if (!isGroup) {
          player.SendErrorMessage($"This object is already shared with {shareTargetName}.");
        } else {
          player.SendErrorMessage($"This object is already shared with group {shareTargetName}.");
        }

        return false;
      } catch (ProtectionNotSharedException) {
        if (isShareAll) {
          player.SendErrorMessage($"This object isn't shared with everyone.");
        } else if (!isGroup) {
          player.SendErrorMessage($"This object isn't shared with {shareTargetName}.");
        } else {
          player.SendErrorMessage($"This object isn't shared with group {shareTargetName}.");
        }

        return false;
      } catch (InvalidBlockTypeException ex) {
        if (sendFailureMessages)
          player.SendErrorMessage("Objects of this type can not be shared with others.");

        return false;
      } catch (MissingPermissionException ex) {
        if (sendFailureMessages) {
          if (ex.Permission == ProtectorPlugin.BankChestShare_Permission) {
            player.SendErrorMessage("You're not allowed to share bank chests.");
          } else {
            player.SendErrorMessage("You're not allowed to share objects of this type.");
          }
        }
        
        return false;
      } catch (NoProtectionException) {
        if (sendFailureMessages)
          player.SendErrorMessage("This object is not protected and thus can't be shared with others.");

        return false;
      } catch (TileProtectedException) {
        if (sendFailureMessages)
          player.SendErrorMessage("You have to be the owner this object to share it.");

        return false;
      }
    }

    private bool TryRemoveProtection(TSPlayer player, DPoint tileLocation, bool sendFailureMessages = true) {
      if (!player.IsLoggedIn) {
        if (sendFailureMessages)
          player.SendErrorMessage("You're not logged in.");

        return false;
      }

      try {
        this.ProtectionManager.RemoveProtection(player, tileLocation);
        player.SendSuccessMessage("Object is not protected anymore.");

        return true;
      } catch (InvalidBlockTypeException ex) {
        if (sendFailureMessages) {
          string message;
          if (TerrariaUtils.Tiles.IsSolidBlockType(ex.BlockType, true))
            message = "Deprotecting this type of blocks is not allowed.";
          else
            message = "Deprotecting this type objects is not allowed.";
        
          player.SendErrorMessage(message);
        }

        return false;
      } catch (NoProtectionException) {
        if (sendFailureMessages)
          player.SendErrorMessage("Object is not protected.");
        
        return false;
      } catch (TileProtectedException) {
        player.SendErrorMessage("You're not the owner of this object.");

        return false;
      }
    }

    private bool TryGetProtectionInfo(TSPlayer player, DPoint tileLocation, bool sendFailureMessages = true) {
      ITile tile = TerrariaUtils.Tiles[tileLocation];
      if (!tile.active())
        return false;

      ProtectionEntry protection = null;
      // Only need the first enumerated entry as we don't need the protections of adjacent blocks.
      foreach (ProtectionEntry enumProtection in this.ProtectionManager.EnumerateProtectionEntries(tileLocation)) {
        protection = enumProtection;
        break;
      }

      if (protection == null) {
        if (sendFailureMessages)
          player.SendErrorMessage($"This object is not protected.");
        
        return false;
      }

      bool canViewExtendedInfo = (
        player.Group.HasPermission(ProtectorPlugin.ViewAllProtections_Permission) ||
        protection.Owner == player.Account.ID ||
        protection.IsSharedWithPlayer(player)
      );
      
      if (!canViewExtendedInfo) {
        player.SendMessage($"This object is protected and not shared with you.", Color.LightGray);

        player.SendWarningMessage("You are not permitted to get more information about this protection.");
        return true;
      }

      string ownerName;
      if (protection.Owner == -1)
        ownerName = "{Server}";
      else
        ownerName = GetUserName(protection.Owner);
      

      player.SendMessage($"This object is protected. The owner is {TShock.Utils.ColorTag(ownerName, Color.Red)}.", Color.LightGray);
      
      string creationTimeFormat = "unknown";
      if (protection.TimeOfCreation != DateTime.MinValue)
        creationTimeFormat = "{0:MM/dd/yy, h:mm tt} UTC ({1} ago)";

      player.SendMessage(
        string.Format(
          CultureInfo.InvariantCulture, "Protection created On: " + creationTimeFormat, protection.TimeOfCreation, 
          (DateTime.UtcNow - protection.TimeOfCreation).ToLongString()
        ), 
        Color.LightGray
      );
      
      int blockType = TerrariaUtils.Tiles[tileLocation].type;
      if (blockType == TileID.Containers || blockType == TileID.Containers2 || blockType == TileID.Dressers) {
        if (protection.RefillChestData != null) {
          RefillChestMetadata refillChest = protection.RefillChestData;
          if (refillChest.RefillTime != TimeSpan.Zero)
            player.SendMessage($"This is a refill chest with a timer set to {TShock.Utils.ColorTag(refillChest.RefillTime.ToLongString(), Color.Red)}.", Color.LightGray);
          else
            player.SendMessage("This is a refill chest without a timer.", Color.LightGray);

          StringBuilder messageBuilder = new StringBuilder();
          if (refillChest.OneLootPerPlayer || refillChest.RemainingLoots != -1) {
            messageBuilder.Append("It can be looted ");
            if (refillChest.OneLootPerPlayer)
              messageBuilder.Append("once per player only");
            if (refillChest.RemainingLoots != -1) {
              if (messageBuilder.Length > 0)
                messageBuilder.Append(" and ");

              messageBuilder.Append(TShock.Utils.ColorTag(refillChest.RemainingLoots.ToString(), Color.Red));
              messageBuilder.Append(" more times in total");
            }
            messageBuilder.Append('.');
          }

          if (refillChest.Looters != null) {
            messageBuilder.Append("It was looted ");
            messageBuilder.Append(TShock.Utils.ColorTag(refillChest.Looters.Count.ToString(), Color.Red));
            messageBuilder.Append(" times yet.");
          }

          if (messageBuilder.Length > 0)
            player.SendMessage(messageBuilder.ToString(), Color.LightGray);
        } else if (protection.BankChestKey != BankChestDataKey.Invalid) {
          BankChestDataKey bankChestKey = protection.BankChestKey;
          player.SendMessage($"This is a bank chest instance with the number {bankChestKey.BankChestIndex}.", Color.LightGray);
        } else if (protection.TradeChestData != null) {
          Item sellItem = new Item();
          sellItem.netDefaults(protection.TradeChestData.ItemToSellId);
          sellItem.stack = protection.TradeChestData.ItemToSellAmount;
          Item payItem = new Item();
          payItem.netDefaults(protection.TradeChestData.ItemToPayId);
          payItem.stack = protection.TradeChestData.ItemToPayAmount;

          player.SendMessage($"This is a trade chest. It's selling {TShock.Utils.ItemTag(sellItem)} for {TShock.Utils.ItemTag(payItem)}", Color.LightGray);
        }

        IChest chest = this.ChestManager.ChestFromLocation(protection.TileLocation);
        if (chest.IsWorldChest)
          player.SendMessage($"It is stored as part of the world data (id: {TShock.Utils.ColorTag(chest.Index.ToString(), Color.Red)}).", Color.LightGray);
        else
          player.SendMessage($"It is {TShock.Utils.ColorTag("not", Color.Red)} stored as part of the world data.", Color.LightGray);
      }
      
      if (ProtectionManager.IsShareableBlockType(blockType)) {
        if (protection.IsSharedWithEveryone) {
          player.SendMessage("Protection is shared with everyone.", Color.LightGray);
        } else {
          StringBuilder sharedListBuilder = new StringBuilder();
          if (protection.SharedUsers != null) {
            for (int i = 0; i < protection.SharedUsers.Count; i++) {
              if (i > 0)
                sharedListBuilder.Append(", ");

              TShockAPI.DB.UserAccount tsUser = TShock.UserAccounts.GetUserAccountByID(protection.SharedUsers[i]);
              if (tsUser != null)
                sharedListBuilder.Append(tsUser.Name);
            }
          }

          if (sharedListBuilder.Length == 0 && protection.SharedGroups == null) {
            player.SendMessage($"Protection is {TShock.Utils.ColorTag("not", Color.Red)} shared with users or groups.", Color.LightGray);
          } else {
            if (sharedListBuilder.Length > 0)
              player.SendMessage($"Shared with users: {TShock.Utils.ColorTag(sharedListBuilder.ToString(), Color.Red)}", Color.LightGray);
            else
              player.SendMessage($"Protection is {TShock.Utils.ColorTag("not", Color.Red)} shared with users.", Color.LightGray);

            if (protection.SharedGroups != null)
              player.SendMessage($"Shared with groups: {TShock.Utils.ColorTag(protection.SharedGroups.ToString(), Color.Red)}", Color.LightGray);
            else
              player.SendMessage($"Protection is {TShock.Utils.ColorTag("not", Color.Red)} shared with groups.", Color.LightGray);
          }
        }
      }

      if (protection.TradeChestData != null && protection.TradeChestData.TransactionJournal.Count > 0) {
        player.SendMessage($"Trade Chest Journal (Last {protection.TradeChestData.TransactionJournal.Count} Transactions)", Color.LightYellow);
        protection.TradeChestData.TransactionJournal.ForEach(entry => {
          string entryText = entry.Item1;
          DateTime entryTime = entry.Item2;
          TimeSpan timeSpan = DateTime.UtcNow - entryTime;

          player.SendMessage($"{entryText} {timeSpan.ToLongString()} ago.", Color.LightGray);
        });
      }

      return true;
    }

    private static string GetUserName(int userId) {
      TShockAPI.DB.UserAccount tsUser = TShock.UserAccounts.GetUserAccountByID(userId);
      if (tsUser != null)
        return tsUser.Name;
      else 
        return string.Concat("{deleted user id: ", userId, "}");
    }

    private bool CheckProtected(TSPlayer player, DPoint tileLocation, bool fullAccessRequired) {
      if (!TerrariaUtils.Tiles[tileLocation].active())
        return false;

      ProtectionEntry protection;
      if (this.ProtectionManager.CheckBlockAccess(player, tileLocation, fullAccessRequired, out protection))
        return false;

      player.SendErrorMessage("This object is protected.");
      return true;
    }

    public bool TryLockChest(TSPlayer player, DPoint anyChestTileLocation, bool sendMessages = true) {
      try {
        TerrariaUtils.Tiles.LockChest(anyChestTileLocation);
        return true;
      } catch (ArgumentException) {
        player.SendErrorMessage("There is no chest here.");
        return false;
      } catch (InvalidChestStyleException) {
        player.SendErrorMessage("The chest must be an unlocked lockable chest.");
        return false;
      }
    }

    public bool TrySwapChestData(TSPlayer player, DPoint anyChestTileLocation, out IChest newChest) {
      newChest = null;

      int tileID = TerrariaUtils.Tiles[anyChestTileLocation].type;
      if (tileID != TileID.Containers && tileID != TileID.Containers2 && tileID != TileID.Dressers) {
        player?.SendErrorMessage("The selected tile is not a chest or dresser.");
        return false;
      }

      DPoint chestLocation = TerrariaUtils.Tiles.MeasureObject(anyChestTileLocation).OriginTileLocation;
      IChest chest = this.ChestManager.ChestFromLocation(chestLocation, player);
      if (chest == null)
        return false;

      ItemData[] content = new ItemData[Chest.maxItems];
      for (int i = 0; i < Chest.maxItems; i++)
        content[i] = chest.Items[i];
      
      if (chest.IsWorldChest) {
        lock (this.WorldMetadata.ProtectorChests) {
          bool isChestAvailable = this.WorldMetadata.ProtectorChests.Count < this.Config.MaxProtectorChests;
          if (!isChestAvailable) {
            player?.SendErrorMessage("The maximum of possible Protector chests has been reached.");
            return false;
          }

          int playerUsingChestIndex = Chest.UsingChest(chest.Index);
          if (playerUsingChestIndex != -1)
            Main.player[playerUsingChestIndex].chest = -1;

          Main.chest[chest.Index] = null;
          newChest = new ProtectorChestData(chestLocation, content);

          this.WorldMetadata.ProtectorChests.Add(chestLocation, (ProtectorChestData)newChest);

          //TSPlayer.All.SendData(PacketTypes.ChestName, string.Empty, chest.Index, chestLocation.X, chestLocation.Y);

          // Tell the client to remove the chest with the given index from its own chest array.
          TSPlayer.All.SendData(PacketTypes.PlaceChest, string.Empty, 1, chestLocation.X, chestLocation.Y, 0, chest.Index);
          TSPlayer.All.SendTileSquare(chestLocation.X, chestLocation.Y, 2);
          player?.SendWarningMessage("This chest is now a Protector chest.");
        }
      } else {
        int availableUnnamedChestIndex = -1;
        int availableEmptyChestIndex = -1;
        for (int i = 0; i < Main.chest.Length; i++) {
          if (i == ChestManager.DummyChestIndex)
            continue;

          Chest tChest = Main.chest[i];
          if (tChest == null) {
            availableEmptyChestIndex = i;
            break;
          } else if (availableUnnamedChestIndex == -1 && string.IsNullOrWhiteSpace(tChest.name)) {
            availableUnnamedChestIndex = i;
          }
        }

        // Prefer unset chests over unnamed chests.
        int availableChestIndex = availableEmptyChestIndex;
        if (availableChestIndex == -1)
          availableChestIndex = availableUnnamedChestIndex;

        bool isChestAvailable = (availableChestIndex != -1);
        if (!isChestAvailable) {
          player?.SendErrorMessage("The maximum of possible world chests has been reached.");
          return false;
        }

        lock (this.WorldMetadata.ProtectorChests)
          this.WorldMetadata.ProtectorChests.Remove(chestLocation);

        Chest availableChest = Main.chest[availableChestIndex];
        bool isExistingButUnnamedChest = (availableChest != null);
        if (isExistingButUnnamedChest) {
          if (!this.TrySwapChestData(null, new DPoint(availableChest.x, availableChest.y), out newChest))
            return false;
        }

        availableChest = Main.chest[availableChestIndex] = new Chest();
        availableChest.x = chestLocation.X;
        availableChest.y = chestLocation.Y;
        availableChest.item = content.Select(i => i.ToItem()).ToArray();

        newChest = new ChestAdapter(availableChestIndex, availableChest);
        player?.SendWarningMessage("This chest is now a world chest.");
      }

      return true;
    }

    public void RemoveChestData(IChest chest) {
      ItemData[] content = new ItemData[Chest.maxItems];
      for (int i = 0; i < Chest.maxItems; i++)
        content[i] = chest.Items[i];

      if (chest.IsWorldChest) {
        int playerUsingChestIndex = Chest.UsingChest(chest.Index);
        if (playerUsingChestIndex != -1)
          Main.player[playerUsingChestIndex].chest = -1;

        Main.chest[chest.Index] = null;
      } else {
        lock (this.WorldMetadata.ProtectorChests)
          this.WorldMetadata.ProtectorChests.Remove(chest.Location);
      }
    }

    /// <exception cref="FormatException">The format item in <paramref name="format" /> is invalid.-or- The index of a format item is not zero. </exception>
    public bool TrySetUpRefillChest(
      TSPlayer player, DPoint tileLocation, TimeSpan? refillTime, bool? oneLootPerPlayer, int? lootLimit, bool? autoLock, 
      bool? autoEmpty, bool sendMessages = true
    ) {
      if (!player.IsLoggedIn) {
        if (sendMessages)
          player.SendErrorMessage("You have to be logged in in order to set up refill chests.");

        return false;
      }

      if (!this.ProtectionManager.CheckBlockAccess(player, tileLocation, true) && !player.Group.HasPermission(ProtectorPlugin.ProtectionMaster_Permission)) {
        player.SendErrorMessage("You don't own the protection of this chest.");
        return false;
      }

      try {
        if (this.ChestManager.SetUpRefillChest(
          player, tileLocation, refillTime, oneLootPerPlayer, lootLimit, autoLock, autoEmpty, false, true
        )) {
          if (sendMessages) {
            player.SendSuccessMessage("Refill chest successfully set up.");

            if (this.Config.AllowRefillChestContentChanges)
              player.SendSuccessMessage("As you are the owner of it you may still freely modify its contents.");
          }
        } else {
          if (sendMessages) {
            if (refillTime != null) {
              if (refillTime != TimeSpan.Zero)
                player.SendSuccessMessage($"Set the refill timer of this chest to {refillTime.Value.ToLongString()}.");
              else
                player.SendSuccessMessage("This chest will now refill instantly.");
            }
            if (oneLootPerPlayer != null) {
              if (oneLootPerPlayer.Value)
                player.SendSuccessMessage("This chest can now be looted once per player only.");
              else
                player.SendSuccessMessage("This chest can now be looted freely.");
            }
            if (lootLimit != null) {
              if (lootLimit.Value != -1)
                player.SendSuccessMessage($"This chest can now be looted {lootLimit} more times.");
              else
                player.SendSuccessMessage("This chest can now be looted endlessly.");
            }
            if (autoLock != null) {
              if (autoLock.Value)
                player.SendSuccessMessage("This chest locks itself automatically when it gets looted.");
              else
                player.SendSuccessMessage("This chest will not lock itself automatically anymore.");
            }
            if (autoEmpty != null) {
              if (autoEmpty.Value)
                player.SendSuccessMessage("This chest empties itself automatically when it gets looted.");
              else
                player.SendSuccessMessage("This chest will not empty itself automatically anymore.");
            }
          }
        }

        if (this.Config.AutoShareRefillChests) {
          foreach (ProtectionEntry protection in this.ProtectionManager.EnumerateProtectionEntries(tileLocation)) {
            protection.IsSharedWithEveryone = true;
            break;
          }
        }

        return true;
      } catch (ArgumentException ex) {
        if (ex.ParamName == "tileLocation") {
          if (sendMessages)
            player.SendErrorMessage("There is no chest here.");

          return false;
        }

        throw;
      } catch (MissingPermissionException) {
        if (sendMessages)
          player.SendErrorMessage("You are not allowed to define refill chests.");

        return false;
      } catch (NoProtectionException) {
        if (sendMessages)
          player.SendErrorMessage("The chest must be protected first.");

        return false;
      } catch (ChestIncompatibilityException) {
        if (sendMessages)
          player.SendErrorMessage("A chest can not be a refill- and bank chest at the same time.");

        return false;
      } catch (NoChestDataException) {
        if (sendMessages) {
          player.SendErrorMessage("Error: There are no chest data for this chest available. This world's data might be");
          player.SendErrorMessage("corrupted.");
        }

        return false;
      }
    }

    public bool TrySetUpBankChest(TSPlayer player, DPoint tileLocation, int bankChestIndex, bool sendMessages = true) {
      if (!player.IsLoggedIn) {
        if (sendMessages)
          player.SendErrorMessage("You have to be logged in in order to set up bank chests.");
        
        return false;
      }

      if (!this.ProtectionManager.CheckBlockAccess(player, tileLocation, true) && !player.Group.HasPermission(ProtectorPlugin.ProtectionMaster_Permission)) {
        player.SendErrorMessage("You don't own the protection of this chest.");
        return false;
      }
      
      try {
        this.ChestManager.SetUpBankChest(player, tileLocation, bankChestIndex, true);

        player.SendSuccessMessage(string.Format(
          $"This chest is now an instance of your bank chest with the number {TShock.Utils.ColorTag(bankChestIndex.ToString(), Color.Red)}."
        ));

        return true;
      } catch (ArgumentException ex) {
        if (ex.ParamName == "tileLocation") {
          if (sendMessages)
            player.SendErrorMessage("There is no chest here.");

          return false;
        } else if (ex.ParamName == "bankChestIndex") {
          ArgumentOutOfRangeException actualEx = (ArgumentOutOfRangeException)ex;
          if (sendMessages) {
            string messageFormat;
            if (!player.Group.HasPermission(ProtectorPlugin.NoBankChestLimits_Permission))
              messageFormat = "The bank chest number must be between 1 and {0}.";
            else
              messageFormat = "The bank chest number must be greater than 1.";

            player.SendErrorMessage(string.Format(messageFormat, actualEx.ActualValue));
          }

          return false;
        }

        throw;
      } catch (MissingPermissionException) {
        if (sendMessages)
          player.SendErrorMessage("You are not allowed to define bank chests.");

        return false;
      } catch (InvalidBlockTypeException) {
        if (sendMessages)
          player.SendErrorMessage("Only chests can be converted to bank chests.");

        return false;
      } catch (NoProtectionException) {
        if (sendMessages)
          player.SendErrorMessage("The chest must be protected first.");

        return false;
      } catch (ChestNotEmptyException) {
        if (sendMessages)
          player.SendErrorMessage("The chest has to be empty in order to restore a bank chest here.");

        return false;
      } catch (ChestTypeAlreadyDefinedException) {
        if (sendMessages)
          player.SendErrorMessage("The chest is already a bank chest.");

        return false;
      } catch (ChestIncompatibilityException) {
        if (sendMessages)
          player.SendErrorMessage("A bank chest can not be a refill- or trade chest at the same time.");

        return false;
      } catch (NoChestDataException) {
        if (sendMessages) {
          player.SendErrorMessage("Error: There are no chest data for this chest available. This world's data might be");
          player.SendErrorMessage("corrupted.");
        }

        return false;
      } catch (BankChestAlreadyInstancedException) {
        if (sendMessages) {
          player.SendErrorMessage($"There is already an instance of your bank chest with the index {bankChestIndex} in");
          player.SendErrorMessage("this world.");
        }

        return false;
      }
    }

    public bool TrySetUpTradeChest(TSPlayer player, DPoint tileLocation, int sellAmount, int sellItemId, int payAmount, object payItemIdOrGroup, int lootLimit = 0, bool sendMessages = true) {
      if (!player.IsLoggedIn) {
        if (sendMessages)
          player.SendErrorMessage("You have to be logged in in order to set up trade chests.");
        
        return false;
      }

      if (!this.ProtectionManager.CheckBlockAccess(player, tileLocation, true) && !player.Group.HasPermission(ProtectorPlugin.ProtectionMaster_Permission)) {
        player.SendErrorMessage("You don't own the protection of this chest.");
        return false;
      }
      
      try {
        this.ChestManager.SetUpTradeChest(player, tileLocation, sellAmount, sellItemId, payAmount, payItemIdOrGroup, lootLimit, true);

        player.SendSuccessMessage("Trade chest was successfully created / updated.");
        return true;
      } catch (ArgumentOutOfRangeException ex) {
        if (sendMessages)
          player.SendErrorMessage("Invalid item amount given.");

        return false;
      } catch (ArgumentException ex) {
        if (ex.ParamName == "tileLocation") {
          if (sendMessages)
            player.SendErrorMessage("There is no chest here.");

          return false;
        }

        throw;
      } catch (MissingPermissionException) {
        if (sendMessages)
          player.SendErrorMessage("You are not allowed to define trade chests.");
      #if SEconomy
      } catch (PaymentException ex) {
        if (sendMessages)
          player.SendErrorMessage("You don't have the necessary amount of {0} {1} to set up a trade chest!", ex.PaymentAmount, this.PluginCooperationHandler.Seconomy_MoneyName());
      #endif
      } catch (InvalidBlockTypeException) {
        if (sendMessages)
          player.SendErrorMessage("Only chests can be converted to trade chests.");
      } catch (NoProtectionException) {
        if (sendMessages)
          player.SendErrorMessage("The chest must be protected first.");
      } catch (ChestTypeAlreadyDefinedException) {
        if (sendMessages)
          player.SendErrorMessage("The chest is already a trade chest.");
      } catch (ChestIncompatibilityException) {
        if (sendMessages)
          player.SendErrorMessage("A trade chest can not be a bank chest at the same time.");
      } catch (NoChestDataException) {
        if (sendMessages)
          player.SendErrorMessage("Error: There are no chest data for this chest available. This world's data might be corrupted.");
      }

      return false;
    }

    public void EnsureProtectionData(TSPlayer player, bool resetBankChestContent) {
      int invalidProtectionsCount;
      int invalidRefillChestCount;
      int invalidBankChestCount;

      this.ProtectionManager.EnsureProtectionData(
        resetBankChestContent, out invalidProtectionsCount, out invalidRefillChestCount, out invalidBankChestCount);

      if (player != TSPlayer.Server) {
        if (invalidProtectionsCount > 0)
          player.SendWarningMessage("{0} invalid protections removed.", invalidProtectionsCount);
        if (invalidRefillChestCount > 0)
          player.SendWarningMessage("{0} invalid refill chests removed.", invalidRefillChestCount);
        if (invalidBankChestCount > 0)
          player.SendWarningMessage("{0} invalid bank chest instances removed.", invalidBankChestCount);

        player.SendInfoMessage("Finished ensuring protection data.");
      }

      if (invalidProtectionsCount > 0)
        this.PluginTrace.WriteLineWarning("{0} invalid protections removed.", invalidProtectionsCount);
      if (invalidRefillChestCount > 0)
        this.PluginTrace.WriteLineWarning("{0} invalid refill chests removed.", invalidRefillChestCount);
      if (invalidBankChestCount > 0)
        this.PluginTrace.WriteLineWarning("{0} invalid bank chest instances removed.", invalidBankChestCount);

      this.PluginTrace.WriteLineInfo("Finished ensuring protection data.");
    }

    private void DestroyBlockOrObject(DPoint tileLocation) {
      ITile tile = TerrariaUtils.Tiles[tileLocation];
      if (!tile.active())
        return;

      if (tile.type == TileID.Containers || tile.type == TileID.Containers2 || tile.type == TileID.Dressers) {
        this.ChestManager.DestroyChest(tileLocation);
      } else {
        WorldGen.KillTile(tileLocation.X, tileLocation.Y, false, false, true);
        TSPlayer.All.SendData(PacketTypes.PlaceChest, string.Empty, 0, tileLocation.X, tileLocation.Y, 0, -1);
      }
    }

    public bool CheckRefillChestLootability(RefillChestMetadata refillChest, TSPlayer player, bool sendReasonMessages = true) {
      if (!player.IsLoggedIn && (refillChest.OneLootPerPlayer || refillChest.RemainingLoots != -1)) {
        if (sendReasonMessages)
          player.SendErrorMessage("You have to be logged in in order to use this chest.");

        return false;
      }

      if (
        !this.Config.AllowRefillChestContentChanges || 
        (player.Account.ID != refillChest.Owner && !player.Group.HasPermission(ProtectorPlugin.ProtectionMaster_Permission))
      ) {
        if (refillChest.RemainingLoots == 0) {
          if (sendReasonMessages)
            player.SendErrorMessage("This chest has a loot limit attached to it and can't be looted anymore.");

          return false;
        }

        if (refillChest.OneLootPerPlayer) {
          //Contract.Assert(refillChest.Looters != null);
          if (refillChest.Looters == null)
            refillChest.Looters = new Collection<int>();

          if (refillChest.Looters.Contains(player.Account.ID)) {
            if (sendReasonMessages)
              player.SendErrorMessage("This chest can be looted only once per player.");

            return false;
          }
        }
      }

      return true;
    }

    #region [IDisposable Implementation]
    protected override void Dispose(bool isDisposing) {
      if (this.IsDisposed)
        return;
      
      if (isDisposing)
        this.ReloadConfigurationCallback = null;

      base.Dispose(isDisposing);
    }
    #endregion
  }
}
