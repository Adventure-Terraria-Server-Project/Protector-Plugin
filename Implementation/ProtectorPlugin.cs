using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Terraria.ID;
using DPoint = System.Drawing.Point;

using TerrariaApi.Server;
using TShockAPI;

using Terraria.Plugins.Common;
using Terraria.Plugins.Common.Hooks;

namespace Terraria.Plugins.CoderCow.Protector {
  [ApiVersion(2, 1)]
  public class ProtectorPlugin: TerrariaPlugin, IDisposable {
    private const string TracePrefix = @"[Protector] ";

    public const string ManualProtect_Permission        = "prot.manualprotect";
    public const string ManualDeprotect_Permission      = "prot.manualdeprotect";
    public const string ViewAllProtections_Permission   = "prot.viewall";
    public const string NoProtectionLimits_Permission   = "prot.nolimits";
    public const string ChestSharing_Permission         = "prot.chestshare";
    public const string SwitchSharing_Permission        = "prot.switchshare";
    public const string OtherSharing_Permission         = "prot.othershare";
    public const string ShareWithGroups_Permission      = "prot.sharewithgroups";
    public const string ProtectionMaster_Permission     = "prot.protectionmaster";
    public const string UseEverything_Permission        = "prot.useeverything";
    public const string SetRefillChests_Permission      = "prot.setrefillchest";
    public const string SetBankChests_Permission        = "prot.setbankchest";
    public const string SetTradeChests_Permission       = "prot.settradechest";
    public const string DumpBankChests_Permission       = "prot.dumpbankchest";
    public const string BankChestShare_Permission       = "prot.bankchestshare";
    public const string NoBankChestLimits_Permission    = "prot.nobankchestlimits";
    public const string FreeTradeChests_Permission      = "prot.freetradechests";
    public const string ScanChests_Permission           = "prot.scanchests";
    public const string Utility_Permission              = "prot.utility";
    public const string Cfg_Permission                  = "prot.cfg";

    public static string DataDirectory => Path.Combine(TShock.SavePath, "Protector");
    public static string ConfigFilePath => Path.Combine(ProtectorPlugin.DataDirectory, "Config.xml");
    public static string SqlLiteDatabaseFile => Path.Combine(ProtectorPlugin.DataDirectory, "Protector.sqlite");
    public static string WorldMetadataDirectory => Path.Combine(ProtectorPlugin.DataDirectory, "World Data");

    public static ProtectorPlugin LatestInstance { get; private set; }

    public PluginTrace Trace { get; private set; }
    protected PluginInfo PluginInfo { get; private set; }
    protected Configuration Config { get; private set; }
    protected GetDataHookHandler GetDataHookHandler { get; private set; }
    protected GetDataHookHandler GetDataHookHandlerLate { get; private set; }
    public ChestManager ChestManager { get; private set; }
    public ProtectionManager ProtectionManager { get; private set; }
    protected UserInteractionHandler UserInteractionHandler { get; private set; }
    protected ServerMetadataHandler ServerMetadataHandler { get; private set; }
    protected WorldMetadataHandler WorldMetadataHandler { get; private set; }
    public WorldMetadata WorldMetadata => this.WorldMetadataHandler.Metadata;
    protected PluginCooperationHandler PluginCooperationHandler { get; private set; }

    private bool hooksEnabled;

    public ProtectorPlugin(Main game): base(game) {
      this.PluginInfo = new PluginInfo(
        "Protector",
        Assembly.GetAssembly(typeof(ProtectorPlugin)).GetName().Version,
        "",
        "CoderCow",
        "Protects single blocks and objects from being changed."
      );

      this.Order = 1;
      #if DEBUG
      if (Debug.Listeners.Count == 0)
        Debug.Listeners.Add(new ConsoleTraceListener());
      #endif

      this.Trace = new PluginTrace(ProtectorPlugin.TracePrefix);
      ProtectorPlugin.LatestInstance = this;
    }

    public override void Initialize() {
      ServerApi.Hooks.GamePostInitialize.Register(this, this.Game_PostInitialize);

      this.AddHooks();
    }

    private void Game_PostInitialize(EventArgs e) {
      ServerApi.Hooks.GamePostInitialize.Deregister(this, this.Game_PostInitialize);

      if (!Directory.Exists(ProtectorPlugin.DataDirectory))
        Directory.CreateDirectory(ProtectorPlugin.DataDirectory);
      
      if (!this.InitConfig())
        return;
      if (!this.InitServerMetdataHandler())
        return;
      if (!this.InitWorldMetdataHandler())
        return;

      this.PluginCooperationHandler = new PluginCooperationHandler(this.Trace, this.Config, this.ChestManager);
      this.ChestManager = new ChestManager(
        this.Trace, this.Config, this.ServerMetadataHandler, this.WorldMetadata, this.PluginCooperationHandler);
      this.ProtectionManager = new ProtectionManager(
        this.Trace, this.Config, this.ChestManager, this.ServerMetadataHandler, this.WorldMetadata);

      this.PluginCooperationHandler.ChestManager = this.ChestManager;

      this.InitUserInteractionHandler();
      this.UserInteractionHandler.EnsureProtectionData(TSPlayer.Server, true);

      this.hooksEnabled = true;
    }

    private bool InitConfig() {
      if (File.Exists(ProtectorPlugin.ConfigFilePath)) {
        try {
          this.Config = Configuration.Read(ProtectorPlugin.ConfigFilePath);
        } catch (Exception ex) {
          this.Trace.WriteLineError(
            "Reading the configuration file failed. This plugin will be disabled. Exception details:\n{0}", ex
          );
          this.Trace.WriteLineError("THIS PLUGIN IS DISABLED, EVERYTHING IS UNPROTECTED!");

          this.Dispose();
          return false;
        }
      } else {
        this.Config = new Configuration();
      }

      // Warn about possible unwanted configuration settings
      if (this.Config.ManuallyProtectableTiles[TileID.Sand] || this.Config.AutoProtectedTiles[TileID.Sand])
        this.Trace.WriteLineWarning("Protector is configured to protect sand blocks, this is generally not recommended as protections will not move with falling sand and thus cause invalid protections.");
      if (this.Config.ManuallyProtectableTiles[TileID.Silt] || this.Config.AutoProtectedTiles[TileID.Silt])
        this.Trace.WriteLineWarning("Protector is configured to protect silt blocks, this is generally not recommended as protections will not move with falling silt and thus cause invalid protections.");
      if (this.Config.ManuallyProtectableTiles[TileID.MagicalIceBlock] || this.Config.AutoProtectedTiles[TileID.MagicalIceBlock])
        this.Trace.WriteLineWarning("Protector is configured to protect ice blocks, this is generally not recommended as protections will not be automatically removed when the ice block disappears.");

      return true;
    }

    private bool InitServerMetdataHandler() {
      this.ServerMetadataHandler = new ServerMetadataHandler(ProtectorPlugin.SqlLiteDatabaseFile);

      try {
        this.ServerMetadataHandler.EstablishConnection();
      } catch (Exception ex) {
        this.Trace.WriteLineError(
          "An error occured while opening the database connection. This plugin will be disabled. Exception details: \n" + ex
        );
        this.Trace.WriteLineError("THIS PLUGIN IS DISABLED, EVERYTHING IS UNPROTECTED!");

        this.Dispose();
        return false;
      }

      try {
        this.ServerMetadataHandler.EnsureDataStructure();
      } catch (Exception ex) {
        this.Trace.WriteLineError(
          "An error occured while ensuring the database structure. This plugin will be disabled. Exception details: \n" + ex
        );
        this.Trace.WriteLineError("THIS PLUGIN IS DISABLED, EVERYTHING IS UNPROTECTED!");

        this.Dispose();
        return false;
      }

      return true;
    }

    private bool InitWorldMetdataHandler() {
      this.WorldMetadataHandler = new WorldMetadataHandler(this.Trace, ProtectorPlugin.WorldMetadataDirectory);

      try {
        this.WorldMetadataHandler.InitOrReadMetdata();
      } catch (Exception ex) {
        this.Trace.WriteLineError("Failed initializing or reading metdata or its backup. This plugin will be disabled. Exception details:\n" + ex);
        this.Trace.WriteLineError("THIS PLUGIN IS DISABLED, EVERYTHING IS UNPROTECTED!");

        this.Dispose();
        return false;
      }

      return true;
    }

    private void InitUserInteractionHandler() {
      Func<Configuration> reloadConfiguration = () => {
        if (this.isDisposed)
          return null;

        this.Config = Configuration.Read(ProtectorPlugin.ConfigFilePath);
        this.ProtectionManager.Config = this.Config;

        return this.Config;
      };
      this.UserInteractionHandler = new UserInteractionHandler(
        this.Trace, this.PluginInfo, this.Config, this.ServerMetadataHandler, this.WorldMetadata, 
        this.ProtectionManager, this.ChestManager, this.PluginCooperationHandler, reloadConfiguration
      );
    }

    private void AddHooks() {
      if (this.GetDataHookHandler != null)
        throw new InvalidOperationException("Hooks already registered.");
      
      // this handler should ideally be registered BEFORE all other plugins
      this.GetDataHookHandler = new GetDataHookHandler(this, true);
      this.GetDataHookHandler.TileEdit += this.Net_TileEdit;
      this.GetDataHookHandler.SignEdit += this.Net_SignEdit;
      this.GetDataHookHandler.SignRead += this.Net_SignRead;
      this.GetDataHookHandler.ChestPlace += this.Net_ChestPlace;
      this.GetDataHookHandler.ChestOpen += this.Net_ChestOpen;
      this.GetDataHookHandler.ChestRename += this.Net_ChestRename;
      this.GetDataHookHandler.ChestGetContents += this.Net_ChestGetContents;
      this.GetDataHookHandler.ChestModifySlot += this.Net_ChestModifySlot;
      this.GetDataHookHandler.ChestUnlock += this.Net_ChestUnlock;
      this.GetDataHookHandler.HitSwitch += this.Net_HitSwitch;
      this.GetDataHookHandler.DoorUse += this.Net_DoorUse;
      this.GetDataHookHandler.QuickStackNearby += this.Net_QuickStackNearby;

      // this handler should ideally be registered AFTER all other plugins
      this.GetDataHookHandlerLate = new GetDataHookHandler(this, true, -100);
      this.GetDataHookHandlerLate.TileEdit += this.Net_TileEditLate;
      this.GetDataHookHandlerLate.ObjectPlacement += this.Net_ObjectPlacement;

      ServerApi.Hooks.GameUpdate.Register(this, this.Game_Update);
      ServerApi.Hooks.WorldSave.Register(this, this.World_SaveWorld);
      GetDataHandlers.PlayerSpawn += this.TShock_PlayerSpawn;
    }

    private void RemoveHooks() {
      this.GetDataHookHandler?.Dispose();

      ServerApi.Hooks.GameUpdate.Deregister(this, this.Game_Update);
      ServerApi.Hooks.WorldSave.Deregister(this, this.World_SaveWorld);
      ServerApi.Hooks.GamePostInitialize.Deregister(this, this.Game_PostInitialize);
      GetDataHandlers.PlayerSpawn -= this.TShock_PlayerSpawn;
    }

    private void Net_TileEdit(object sender, TileEditEventArgs e) {
      if (this.isDisposed || !this.hooksEnabled || e.Handled)
        return;

      e.Handled = this.UserInteractionHandler.HandleTileEdit(e.Player, e.EditType, e.BlockType, e.Location, e.ObjectStyle);
    }

    private void Net_TileEditLate(object sender, TileEditEventArgs e) {
      if (this.isDisposed || !this.hooksEnabled || e.Handled)
        return;

      e.Handled = this.UserInteractionHandler.HandleTileEdit(e.Player, e.EditType, e.BlockType, e.Location, e.ObjectStyle, true);
    }

    private void Net_ObjectPlacement(object sender, ObjectPlacementEventArgs e) {
      if (this.isDisposed || !this.hooksEnabled || e.Handled)
        return;

      e.Handled = this.UserInteractionHandler.HandleObjectPlacement(e.Player, e.Location, e.BlockType, e.ObjectStyle, e.Alternative, e.Random, e.Direction);
    }

    private void Net_SignEdit(object sender, SignEditEventArgs e) {
      if (this.isDisposed || !this.hooksEnabled || e.Handled)
        return;

      e.Handled = this.UserInteractionHandler.HandleSignEdit(e.Player, e.SignIndex, e.Location, e.NewText);
    }

    private void Net_SignRead(object sender, TileLocationEventArgs e) {
      if (this.isDisposed || !this.hooksEnabled || e.Handled)
        return;

      e.Handled = this.UserInteractionHandler.HandleSignRead(e.Player, e.Location);
    }

    private void Net_ChestPlace(object sender, ChestPlaceEventArgs e) {
      if (this.isDisposed || !this.hooksEnabled || e.Handled)
        return;

      e.Handled = this.UserInteractionHandler.HandleChestPlace(e.Player, e.Location, e.StorageType, e.StorageStyle);
    }

    private void Net_ChestOpen(object sender, ChestOpenEventArgs e) {
      if (this.isDisposed || !this.hooksEnabled || e.Handled)
        return;

      e.Handled = this.UserInteractionHandler.HandleChestOpen(e.Player, e.ChestIndex, e.Location);
    }

    private void Net_ChestRename(object sender, ChestRenameEventArgs e) {
      if (this.isDisposed || !this.hooksEnabled || e.Handled)
        return;

      e.Handled = this.UserInteractionHandler.HandleChestRename(e.Player, e.ChestIndex, e.NewName);
    }

    private void Net_ChestGetContents(object sender, TileLocationEventArgs e) {
      if (this.isDisposed || !this.hooksEnabled || e.Handled)
        return;

      e.Handled = this.UserInteractionHandler.HandleChestGetContents(e.Player, e.Location);
    }

    private void Net_ChestModifySlot(object sender, ChestModifySlotEventArgs e) {
      if (this.isDisposed || !this.hooksEnabled || e.Handled)
        return;

      e.Handled = this.UserInteractionHandler.HandleChestModifySlot(e.Player, e.ChestIndex, e.SlotIndex, e.NewItem);
    }

    private void Net_ChestUnlock(object sender, TileLocationEventArgs e) {
      if (this.isDisposed || !this.hooksEnabled || e.Handled)
        return;

      e.Handled = this.UserInteractionHandler.HandleChestUnlock(e.Player, e.Location);
    }
    
    private void Net_HitSwitch(object sender, TileLocationEventArgs e) {
      if (this.isDisposed || !this.hooksEnabled || e.Handled)
        return;

      e.Handled = this.UserInteractionHandler.HandleHitSwitch(e.Player, e.Location);
    }

    private void Net_DoorUse(object sender, DoorUseEventArgs e) {
      if (this.isDisposed || !this.hooksEnabled || e.Handled)
        return;

      bool isOpening = (e.action == DoorAction.OpenDoor || e.action == DoorAction.OpenTallGate || e.action == DoorAction.OpenTrapdoor);
      e.Handled = this.UserInteractionHandler.HandleDoorUse(e.Player, e.Location, isOpening, e.Direction);
    }

    private void TShock_PlayerSpawn(object sender, GetDataHandlers.SpawnEventArgs e) {
      if (this.isDisposed || !this.hooksEnabled || e.Handled)
        return;
      
      TSPlayer player = e.Player;
      if (player == null || !player.IsLoggedIn)
        return;

      e.Handled = this.UserInteractionHandler.HandlePlayerSpawn(player, new DPoint(e.SpawnX, e.SpawnY));
    }

    private void Net_QuickStackNearby(object sender, PlayerSlotEventArgs e) {
      if (this.isDisposed || !this.hooksEnabled || e.Handled)
        return;

      e.Handled = this.UserInteractionHandler.HandleQuickStackNearby(e.Player, e.SlotIndex);
    }

    private void Game_Update(EventArgs e) {
      if (this.isDisposed || !this.hooksEnabled)
        return;

      try {
        this.ChestManager.HandleGameSecondUpdate();
      } catch (Exception ex) {
        this.Trace.WriteLineError("Unhandled exception in GameUpdate handler:\n" + ex);
      }
    }

    private void World_SaveWorld(WorldSaveEventArgs e) {
      if (this.isDisposed || !this.hooksEnabled || e.Handled)
        return;

      try {
        lock (this.WorldMetadataHandler.Metadata.Protections) {
          Stopwatch watch = new Stopwatch();
          watch.Start();
          this.WorldMetadataHandler.WriteMetadata();
          Console.WriteLine(File.GetLastWriteTime(Main.worldPathName));
          watch.Stop();

          string format = "Serializing the protection data took {0}ms.";
          if (watch.ElapsedMilliseconds == 0)
            format = "Serializing the protection data took less than 1ms.";

          this.Trace.WriteLineInfo(format, watch.ElapsedMilliseconds);
        }
      } catch (Exception ex) {
        this.Trace.WriteLineError("Unhandled exception in SaveWorld handler:\n" + ex);
      }
    }

    #region [TerrariaPlugin Overrides]
    public override string Name {
      get { return this.PluginInfo.PluginName; }
    }

    public override Version Version {
      get { return this.PluginInfo.VersionNumber; }
    }

    public override string Author {
      get { return this.PluginInfo.Author; }
    }

    public override string Description {
      get { return this.PluginInfo.Description; }
    }
    #endregion

    #region [IDisposable Implementation]
    private bool isDisposed;

    public bool IsDisposed {
      get { return this.isDisposed; } 
    }

    protected override void Dispose(bool isDisposing) {
      if (this.IsDisposed)
        return;
    
      if (isDisposing) {
        this.hooksEnabled = false;
        this.RemoveHooks();

        this.UserInteractionHandler?.Dispose();
        this.ServerMetadataHandler?.Dispose();
      }

      base.Dispose(isDisposing);
      this.isDisposed = true;
    }
    #endregion
  }
}
