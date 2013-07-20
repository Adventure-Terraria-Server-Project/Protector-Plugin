using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DPoint = System.Drawing.Point;

using Terraria.Plugins.Common;
using Terraria.Plugins.Common.Hooks;

using Hooks;
using TShockAPI;

namespace Terraria.Plugins.CoderCow.Protector {
  [APIVersion(1, 12)]
  public class ProtectorPlugin: TerrariaPlugin, IDisposable {
    #region [Constants]
    private const string TracePrefix = @"[Protector] ";

    public const string ManualProtect_Permission        = "prot_manualprotect";
    public const string ManualDeprotect_Permission      = "prot_manualdeprotect";
    public const string ViewAllProtections_Permission   = "prot_viewall";
    public const string NoProtectionLimits_Permission   = "prot_nolimits";
    public const string ChestSharing_Permission         = "prot_chestshare";
    public const string SwitchSharing_Permission        = "prot_switchshare";
    public const string OtherSharing_Permission         = "prot_othershare";
    public const string ShareWithGroups_Permission      = "prot_sharewithgroups";
    public const string ProtectionMaster_Permission     = "prot_protectionmaster";
    public const string UseEverything_Permision         = "prot_useeverything";
    public const string SetRefillChests_Permission      = "prot_setrefillchest";
    public const string SetBankChests_Permission        = "prot_setbankchest";
    public const string DumpBankChests_Permission       = "prot_dumpbankchest";
    public const string BankChestShare_Permission       = "prot_bankchestshare";
    public const string NoBankChestLimits_Permision     = "prot_nobankchestlimits";
    public const string Utility_Permission              = "prot_utility";
    public const string Cfg_Permission                  = "prot_cfg";
    #endregion

    #region [Properties: Static DataDirectory, Static ConfigFilePath, Static SqlLiteDatabaseFile, Static WorldMetadataDirectory]
    public static string DataDirectory {
      get {
        return Path.Combine(TShock.SavePath, "Protector");
      }
    }

    public static string ConfigFilePath {
      get {
        return Path.Combine(ProtectorPlugin.DataDirectory, "Config.xml");
      }
    }

    public static string SqlLiteDatabaseFile {
      get {
        return Path.Combine(ProtectorPlugin.DataDirectory, "Protector.sqlite");
      }
    }

    public static string WorldMetadataDirectory {
      get {
        return Path.Combine(ProtectorPlugin.DataDirectory, "World Data");
      }
    }
    #endregion

    #region [Property: Static LatestInstance]
    private static ProtectorPlugin latestInstance;

    public static ProtectorPlugin LatestInstance {
      get { return ProtectorPlugin.latestInstance; }
    }
    #endregion

    #region [Property: Trace]
    private PluginTrace trace;

    public PluginTrace Trace {
      get { return this.trace; }
    }
    #endregion

    #region [Property: PluginInfo]
    private readonly PluginInfo pluginInfo;

    protected PluginInfo PluginInfo {
      get { return this.pluginInfo; }
    }
    #endregion

    #region [Property: Config]
    private Configuration config;

    protected Configuration Config {
      get { return this.config; }
    }
    #endregion

    #region [Property: GetDataHookHandler]
    private GetDataHookHandler getDataHookHandler;

    protected GetDataHookHandler GetDataHookHandler {
      get { return this.getDataHookHandler; }
    }
    #endregion

    #region [Property: PostGetDataHookHandler]
    private GetDataHookHandler postGetDataHookHandler;

    public GetDataHookHandler PostGetDataHookHandler {
      get { return this.postGetDataHookHandler; }
    }
    #endregion

    #region [Property: ProtectionManager]
    private ProtectionManager protectionManager;

    public ProtectionManager ProtectionManager {
      get { return this.protectionManager; }
    }
    #endregion

    #region [Property: UserInteractionHandler]
    private UserInteractionHandler userInteractionHandler;

    protected UserInteractionHandler UserInteractionHandler {
      get { return this.userInteractionHandler; }
    }
    #endregion

    #region [Property: ServerMetadataHandler]
    private ServerMetadataHandler serverMetadataHandler;

    protected ServerMetadataHandler ServerMetadataHandler {
      get { return this.serverMetadataHandler; }
    }
    #endregion

    #region [Property: WorldMetadataHandler, WorldMetadata]
    private WorldMetadataHandler worldMetadataHandler;

    protected WorldMetadataHandler WorldMetadataHandler {
      get { return this.worldMetadataHandler; }
    }

    public WorldMetadata WorldMetadata { 
      get { return this.WorldMetadataHandler.Metadata; }
    }
    #endregion

    #region [Property: PluginCooperationHandler]
    private PluginCooperationHandler pluginCooperationHandler;

    protected PluginCooperationHandler PluginCooperationHandler {
      get { return this.pluginCooperationHandler; }
    }
    #endregion

    private bool hooksEnabled;


    #region [Method: Constructor]
    public ProtectorPlugin(Main game): base(game) {
      this.pluginInfo = new PluginInfo(
        "Protector",
        Assembly.GetAssembly(typeof(ProtectorPlugin)).GetName().Version,
        "",
        "CoderCow",
        "Protects blocks and objects from being changed."
      );

      this.Order = 1;
      #if DEBUG
      if (Debug.Listeners.Count == 0)
        Debug.Listeners.Add(new ConsoleTraceListener());
      #endif

      this.trace = new PluginTrace(ProtectorPlugin.TracePrefix);
      ProtectorPlugin.latestInstance = this;
    }
    #endregion

    #region [Methods: Initialize, Game_PostInitialize]
    public override void Initialize() {
      GameHooks.PostInitialize += this.Game_PostInitialize;

      this.AddHooks();
    }

    private void Game_PostInitialize() {
      GameHooks.PostInitialize -= this.Game_PostInitialize;

      if (!Directory.Exists(ProtectorPlugin.DataDirectory))
        Directory.CreateDirectory(ProtectorPlugin.DataDirectory);
      
      if (!this.InitConfig())
        return;
      if (!this.InitServerMetdataHandler())
        return;
      if (!this.InitWorldMetdataHandler())
        return;

      this.pluginCooperationHandler = new PluginCooperationHandler(this.Trace);
      this.protectionManager = new ProtectionManager(
        this.Trace, this.Config, this.ServerMetadataHandler, this.WorldMetadataHandler.Metadata
      );

      this.InitUserInteractionHandler();
      this.UserInteractionHandler.EnsureProtectionData(TSPlayer.Server);

      this.hooksEnabled = true;

      Task.Factory.StartNew(() => {
        // Wait a bit until other plugins might have registered their hooks in PostInitialize.
        Thread.Sleep(1000);

        this.AddPostHooks();
      });
    }

    private bool InitConfig() {
      if (File.Exists(ProtectorPlugin.ConfigFilePath)) {
        try {
          this.config = Configuration.Read(ProtectorPlugin.ConfigFilePath);
        } catch (Exception ex) {
          this.Trace.WriteLineError(
            "Reading the configuration file failed. This plugin will be disabled. Exception details:\n{0}", ex
          );
          this.Trace.WriteLineError("THIS PLUGIN IS DISABLED, EVERYTHING IS UNPROTECTED!");

          this.Dispose();
          return false;
        }
      } else {
        this.config = new Configuration();
      }

      // Invalidate Configuration
      if (this.Config.ManuallyProtectableTiles[(int)BlockType.SandBlock] || this.Config.AutoProtectedTiles[(int)BlockType.SandBlock])
        this.Trace.WriteLineWarning("Protector is configured to protect sand blocks, this is generally not recommended as protections will not move with falling sand and thus cause invalid protections.");
      if (this.Config.ManuallyProtectableTiles[(int)BlockType.SiltBlock] || this.Config.AutoProtectedTiles[(int)BlockType.SiltBlock])
        this.Trace.WriteLineWarning("Protector is configured to protect silt blocks, this is generally not recommended as protections will not move with falling silt and thus cause invalid protections.");
      if (this.Config.ManuallyProtectableTiles[(int)BlockType.IceBlock] || this.Config.AutoProtectedTiles[(int)BlockType.IceBlock])
        this.Trace.WriteLineWarning("Protector is configured to protect ice blocks, this is generally not recommended as protections will not be automatically removed when the ice block disappears.");

      return true;
    }

    private bool InitServerMetdataHandler() {
      this.serverMetadataHandler = new ServerMetadataHandler(ProtectorPlugin.SqlLiteDatabaseFile);

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
        this.serverMetadataHandler.EnsureDataStructure();
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
      this.worldMetadataHandler = new WorldMetadataHandler(this.Trace, ProtectorPlugin.WorldMetadataDirectory);

      try {
        this.WorldMetadataHandler.InitOrReadMetdata();
      } catch (Exception ex) {
        this.Trace.WriteLineError("Failed initializing or reading metdata or its backup. This plugin will be disabled. Exception details:\n" + ex);
        this.Trace.WriteLineError("THIS PLUGIN IS DISABLED, EVERYTHING IS UNPROTECTED!");

        this.Dispose();
        return false;
      }

      if (this.WorldMetadataHandler.IsWorldOlderThanLastWrittenMetadata()) {
        try {
          this.WorldMetadataHandler.CreateMetadataSnapshot();
        } catch (InvalidOperationException ex) {
          this.Trace.WriteLineError(ex.ToString());
        }

        this.Trace.WriteLineWarning(string.Format(
          "You might have loaded an outdated version of the current world, so a snapshot of the current metadata was created. Simply ignore this message if you've intentionally loaded the outdated world file, however, if not then you should restore the most recent version of the world and restore the metadata snapshot found at \"{0}\" otherwise some protections might become invalid",
          ProtectorPlugin.WorldMetadataDirectory
        ));
      }

      return true;
    }

    private void InitUserInteractionHandler() {
      Func<Configuration> reloadConfiguration = () => {
        if (this.isDisposed)
          return null;

        this.config = Configuration.Read(ProtectorPlugin.ConfigFilePath);

        this.protectionManager.Config = this.Config;

        return this.config;
      };
      this.userInteractionHandler = new UserInteractionHandler(
        this.Trace, this.PluginInfo, this.Config, this.ServerMetadataHandler, 
        this.WorldMetadataHandler.Metadata, this.ProtectionManager, this.PluginCooperationHandler, reloadConfiguration
      );
    }
    #endregion

    #region [Methods: Server Hook Handling]
    private void AddHooks() {
      if (this.getDataHookHandler != null)
        throw new InvalidOperationException("Hooks already registered.");
      
      this.getDataHookHandler = new GetDataHookHandler(this.Trace, true);
      this.GetDataHookHandler.TileEdit += this.Net_TileEdit;
      this.GetDataHookHandler.SignEdit += this.Net_SignEdit;
      this.GetDataHookHandler.SignRead += this.Net_SignRead;
      this.GetDataHookHandler.ChestOpen += Net_ChestOpen;
      this.GetDataHookHandler.ChestGetContents += this.Net_ChestGetContents;
      this.GetDataHookHandler.ChestModifySlot += this.Net_ChestModifySlot;
      this.GetDataHookHandler.ChestUnlock += this.Net_ChestUnlock;
      this.GetDataHookHandler.HitSwitch += this.Net_HitSwitch;
      this.GetDataHookHandler.DoorUse += this.Net_DoorUse;
      this.GetDataHookHandler.PlayerSpawn += this.Net_PlayerSpawn;

      GameHooks.Update += this.Game_Update;
      WorldHooks.SaveWorld += this.World_SaveWorld;
    }

    private void AddPostHooks() {
      if (this.postGetDataHookHandler != null)
        throw new InvalidOperationException("Post hooks already registered.");

      this.postGetDataHookHandler = new GetDataHookHandler(this.Trace, true);
      this.PostGetDataHookHandler.TileEdit += this.Net_PostTileEdit;
    }

    private void RemoveHooks() {
      if (this.getDataHookHandler != null) 
        this.getDataHookHandler.Dispose();

      GameHooks.Update -= this.Game_Update;
      WorldHooks.SaveWorld -= this.World_SaveWorld;
      GameHooks.PostInitialize -= this.Game_PostInitialize;
    }

    private void RemovePostHooks() {
      if (this.postGetDataHookHandler != null)
        this.postGetDataHookHandler.Dispose();
    }

    private void Net_TileEdit(object sender, TileEditEventArgs e) {
      if (this.isDisposed || !this.hooksEnabled || e.Handled)
        return;

      e.Handled = this.UserInteractionHandler.HandleTileEdit(e.Player, e.EditType, e.BlockType, e.Location, e.ObjectStyle);
    }

    private void Net_PostTileEdit(object sender, TileEditEventArgs e) {
      if (this.isDisposed || !this.hooksEnabled || e.Handled)
        return;

      e.Handled = this.UserInteractionHandler.HandlePostTileEdit(e.Player, e.EditType, e.BlockType, e.Location, e.ObjectStyle);
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

    private void Net_ChestOpen(object sender, ChestOpenEventArgs e) {
      if (this.isDisposed || !this.hooksEnabled || e.Handled)
        return;

      e.Handled = this.UserInteractionHandler.HandleChestOpen(e.Player, e.ChestIndex, e.Location);
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

      e.Handled = this.UserInteractionHandler.HandleDoorUse(e.Player, e.Location, e.IsOpening, e.Direction);
    }

    private void Net_PlayerSpawn(object sender, PlayerSpawnEventArgs e) {
      if (this.isDisposed || !this.hooksEnabled || e.Handled)
        return;

      e.Handled = this.UserInteractionHandler.HandlePlayerSpawn(e.Player, e.SpawnTileLocation);
    }

    private void Game_Update() {
      if (this.isDisposed || !this.hooksEnabled)
        return;

      try {
        this.ProtectionManager.HandleGameUpdate();
      } catch (Exception ex) {
        this.Trace.WriteLineError("Unhandled exception in GameUpdate handler:\n" + ex);
      }
    }

    private void World_SaveWorld(bool resettime, HandledEventArgs e) {
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
    #endregion

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
        this.RemovePostHooks();
        
        if (this.userInteractionHandler != null)
          this.userInteractionHandler.Dispose();
        if (this.serverMetadataHandler != null) 
          this.serverMetadataHandler.Dispose();
      }

      base.Dispose(isDisposing);
      this.isDisposed = true;
    }
    #endregion
  }
}
