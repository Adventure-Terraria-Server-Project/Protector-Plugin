using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Xml;
using System.Xml.Schema;

using Terraria.Plugins.Common;

namespace Terraria.Plugins.CoderCow.Protector {
  public class Configuration {
    #region [Constants]
    public const string CurrentVersion = "1.0";
    #endregion

    #region [Property: ManuallyProtectableTiles]
    private bool[] manuallyProtectableTiles;

    public bool[] ManuallyProtectableTiles {
      get { return this.manuallyProtectableTiles; }
      set { this.manuallyProtectableTiles = value; }
    }
    #endregion

    #region [Property: AutoProtectedTiles]
    private bool[] autoProtectedTiles;

    public bool[] AutoProtectedTiles {
      get { return this.autoProtectedTiles; }
      set { this.autoProtectedTiles = value; }
    }
    #endregion

    #region [Property: NotDeprotectableTiles]
    private bool[] notDeprotectableTiles;

    public bool[] NotDeprotectableTiles {
      get { return this.notDeprotectableTiles; }
      set { this.notDeprotectableTiles = value; }
    }
    #endregion

    #region [Property: MaxProtectionsPerPlayerPerWorld]
    private int maxProtectionsPerPlayerPerWorld;

    public int MaxProtectionsPerPlayerPerWorld {
      get { return this.maxProtectionsPerPlayerPerWorld; }
      set { this.maxProtectionsPerPlayerPerWorld = value; }
    }
    #endregion

    #region [Property: MaxBankChestsPerPlayer]
    private int maxBankChestsPerPlayer;

    public int MaxBankChestsPerPlayer {
      get { return this.maxBankChestsPerPlayer; }
      set { this.maxBankChestsPerPlayer = value; }
    }
    #endregion

    #region [Property: AllowRefillChestContentChanges]
    private bool allowRefillChestContentChanges;

    public bool AllowRefillChestContentChanges {
      get { return this.allowRefillChestContentChanges; }
      set { this.allowRefillChestContentChanges = value; }
    }
    #endregion

    #region [Property: EnableBedSpawnProtection]
    private bool enableBedSpawnProtection;

    public bool EnableBedSpawnProtection {
      get { return this.enableBedSpawnProtection; }
      set { this.enableBedSpawnProtection = value; }
    }
    #endregion

    #region [Property: LoginRequiredForChestUsage]
    private bool loginRequiredForChestUsage;

    public bool LoginRequiredForChestUsage {
      get { return this.loginRequiredForChestUsage; }
      set { this.loginRequiredForChestUsage = value; }
    }
    #endregion

    #region [Property: AutoShareRefillChests]
    private bool autoShareRefillChests;

    public bool AutoShareRefillChests {
      get { return this.autoShareRefillChests; }
      set { this.autoShareRefillChests = value; }
    }
    #endregion

    #region [Property: AutoDeprotectEverythingOnDestruction]
    private bool autoDeprotectEverythingOnDestruction;

    public bool AutoDeprotectEverythingOnDestruction {
      get { return this.autoDeprotectEverythingOnDestruction; }
      set { this.autoDeprotectEverythingOnDestruction = value; }
    }
    #endregion

    #region [Property: AllowChainedSharing]
    private bool allowChainedSharing;

    public bool AllowChainedSharing {
      get { return this.allowChainedSharing; }
      set { this.allowChainedSharing = value; }
    }
    #endregion

    #region [Property: AllowChainedShareAltering]
    private bool allowChainedShareAltering;

    public bool AllowChainedShareAltering {
      get { return this.allowChainedShareAltering; }
      set { this.allowChainedShareAltering = value; }
    }
    #endregion

    #region [Property: AllowWiringProtectedBlocks]
    private bool allowWiringProtectedBlocks;

    public bool AllowWiringProtectedBlocks {
      get { return this.allowWiringProtectedBlocks; }
      set { this.allowWiringProtectedBlocks = value; }
    }
    #endregion

    #region [Property: NotifyAutoProtections]
    private bool notifyAutoProtections;

    public bool NotifyAutoProtections {
      get { return this.notifyAutoProtections; }
      set { this.notifyAutoProtections = value; }
    }
    #endregion

    #region [Property: NotifyAutoDeprotections]
    private bool notifyAutoDeprotections;

    public bool NotifyAutoDeprotections {
      get { return this.notifyAutoDeprotections; }
      set { this.notifyAutoDeprotections = value; }
    }
    #endregion


    #region [Methods: Static Read]
    public static Configuration Read(string filePath) {
      XmlReaderSettings configReaderSettings = new XmlReaderSettings {
        ValidationType = ValidationType.Schema,
        ValidationFlags = XmlSchemaValidationFlags.ProcessIdentityConstraints | XmlSchemaValidationFlags.ReportValidationWarnings
      };

      string configSchemaPath = Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath) + ".xsd");
      configReaderSettings.Schemas.Add(null, configSchemaPath);
      
      XmlDocument document = new XmlDocument();
      using (XmlReader configReader = XmlReader.Create(filePath, configReaderSettings))
        document.Load(configReader);

      // Before validating using the schema, first check if the configuration file's version matches with the supported version.
      XmlElement rootElement = document.DocumentElement;
      string fileVersionRaw;
      if (rootElement.HasAttribute("Version"))
        fileVersionRaw = rootElement.GetAttribute("Version");
      else
        fileVersionRaw = "1.0";
      
      if (fileVersionRaw != Configuration.CurrentVersion) {
        throw new FormatException(string.Format(
          "The configuration file is either outdated or too new. Expected version was: {0}. File version is: {1}", 
          Configuration.CurrentVersion, fileVersionRaw
        ));
      }
      
      Configuration resultingConfig = new Configuration();
      Configuration.UpdateTileIdArrayByString(resultingConfig.manuallyProtectableTiles, rootElement["ManuallyProtectableTiles"].InnerXml);
      Configuration.UpdateTileIdArrayByString(resultingConfig.autoProtectedTiles, rootElement["AutoProtectedTiles"].InnerXml);
      Configuration.UpdateTileIdArrayByString(resultingConfig.notDeprotectableTiles, rootElement["NotDeprotectableTiles"].InnerXml);
      resultingConfig.maxProtectionsPerPlayerPerWorld = int.Parse(rootElement["MaxProtectionsPerPlayerPerWorld"].InnerText);
      resultingConfig.maxBankChestsPerPlayer = int.Parse(rootElement["MaxBankChestsPerPlayer"].InnerXml);

      XmlElement subElement = rootElement["AllowRefillChestContentChanges"];
      if (subElement == null)
        resultingConfig.AllowRefillChestContentChanges = true;
      else
        resultingConfig.AllowRefillChestContentChanges = BoolEx.ParseEx(subElement.InnerXml);

      resultingConfig.enableBedSpawnProtection = BoolEx.ParseEx(rootElement["EnableBedSpawnProtection"].InnerXml);
      resultingConfig.loginRequiredForChestUsage = BoolEx.ParseEx(rootElement["LoginRequiredForChestUsage"].InnerXml);
      resultingConfig.autoShareRefillChests = BoolEx.ParseEx(rootElement["AutoShareRefillChests"].InnerXml);
      resultingConfig.allowChainedSharing = BoolEx.ParseEx(rootElement["AllowChainedSharing"].InnerXml);
      resultingConfig.allowChainedShareAltering = BoolEx.ParseEx(rootElement["AllowChainedShareAltering"].InnerXml);
      resultingConfig.allowWiringProtectedBlocks = BoolEx.ParseEx(rootElement["AllowWiringProtectedBlocks"].InnerXml);
      resultingConfig.autoDeprotectEverythingOnDestruction = BoolEx.ParseEx(rootElement["AutoDeprotectEverythingOnDestruction"].InnerXml);
      resultingConfig.notifyAutoProtections = BoolEx.ParseEx(rootElement["NotifyAutoProtection"].InnerXml);
      resultingConfig.notifyAutoDeprotections = BoolEx.ParseEx(rootElement["NotifyAutoDeprotection"].InnerXml);

      return resultingConfig;
    }

    private static void UpdateTileIdArrayByString(bool[] idArray, string tileIds) {
      if (string.IsNullOrWhiteSpace(tileIds))
        return;

      foreach (string tileId in tileIds.Split(','))
        idArray[int.Parse(tileId)] = true;
    }
    #endregion

    #region [Method: Constructor]
    public Configuration() {
      this.manuallyProtectableTiles = new bool[TerrariaUtils.BlockType_Max];
      this.autoProtectedTiles = new bool[TerrariaUtils.BlockType_Max];
      this.notDeprotectableTiles = new bool[TerrariaUtils.BlockType_Max];
    }
    #endregion
  }
}
