using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Schema;

using Terraria.Plugins.Common;

namespace Terraria.Plugins.CoderCow.Protector {
  public class Configuration {
    public const string CurrentVersion = "1.4";

    public bool[] ManuallyProtectableTiles { get; set; }
    public bool[] AutoProtectedTiles { get; set; }
    public bool[] NotDeprotectableTiles { get; set; }
    public int MaxProtectionsPerPlayerPerWorld { get; set; }
    public int MaxBankChestsPerPlayer { get; set; }
    public bool AllowRefillChestContentChanges { get; set; }
    public bool EnableBedSpawnProtection { get; set; }
    public bool LoginRequiredForChestUsage { get; set; }
    public bool AutoShareRefillChests { get; set; }
    public bool AutoDeprotectEverythingOnDestruction { get; set; }
    public bool AllowChainedSharing { get; set; }
    public bool AllowChainedShareAltering { get; set; }
    public bool AllowWiringProtectedBlocks { get; set; }
    public bool NotifyAutoProtections { get; set; }
    public bool NotifyAutoDeprotections { get; set; }
    public float QuickStackNearbyRange { get; set; }
    public bool DungeonChestProtection { get; set; }
    public Dictionary<string,int> MaxBankChests { get; set; }
    public int MaxProtectorChests { get; set; }
    public int TradeChestPayment { get; set; }
    public Dictionary<string,HashSet<int>> TradeChestItemGroups { get; set; }

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
      Configuration.UpdateTileIdArrayByString(resultingConfig.ManuallyProtectableTiles, rootElement["ManuallyProtectableTiles"].InnerXml);
      Configuration.UpdateTileIdArrayByString(resultingConfig.AutoProtectedTiles, rootElement["AutoProtectedTiles"].InnerXml);
      Configuration.UpdateTileIdArrayByString(resultingConfig.NotDeprotectableTiles, rootElement["NotDeprotectableTiles"].InnerXml);
      resultingConfig.MaxProtectionsPerPlayerPerWorld = int.Parse(rootElement["MaxProtectionsPerPlayerPerWorld"].InnerText);
      resultingConfig.MaxBankChestsPerPlayer = int.Parse(rootElement["MaxBankChestsPerPlayer"].InnerXml);

      XmlElement subElement = rootElement["AllowRefillChestContentChanges"];
      if (subElement == null)
        resultingConfig.AllowRefillChestContentChanges = true;
      else
        resultingConfig.AllowRefillChestContentChanges = BoolEx.ParseEx(subElement.InnerXml);

      resultingConfig.EnableBedSpawnProtection = BoolEx.ParseEx(rootElement["EnableBedSpawnProtection"].InnerXml);
      resultingConfig.LoginRequiredForChestUsage = BoolEx.ParseEx(rootElement["LoginRequiredForChestUsage"].InnerXml);
      resultingConfig.AutoShareRefillChests = BoolEx.ParseEx(rootElement["AutoShareRefillChests"].InnerXml);
      resultingConfig.AllowChainedSharing = BoolEx.ParseEx(rootElement["AllowChainedSharing"].InnerXml);
      resultingConfig.AllowChainedShareAltering = BoolEx.ParseEx(rootElement["AllowChainedShareAltering"].InnerXml);
      resultingConfig.AllowWiringProtectedBlocks = BoolEx.ParseEx(rootElement["AllowWiringProtectedBlocks"].InnerXml);
      resultingConfig.AutoDeprotectEverythingOnDestruction = BoolEx.ParseEx(rootElement["AutoDeprotectEverythingOnDestruction"].InnerXml);
      resultingConfig.NotifyAutoProtections = BoolEx.ParseEx(rootElement["NotifyAutoProtection"].InnerXml);
      resultingConfig.NotifyAutoDeprotections = BoolEx.ParseEx(rootElement["NotifyAutoDeprotection"].InnerXml);
      resultingConfig.DungeonChestProtection = BoolEx.ParseEx(rootElement["DungeonChestProtection"].InnerXml);
      resultingConfig.QuickStackNearbyRange = float.Parse(rootElement["QuickStackNearbyRange"].InnerXml);
      resultingConfig.MaxProtectorChests = int.Parse(rootElement["MaxProtectorChests"].InnerXml);
      resultingConfig.TradeChestPayment = int.Parse(rootElement["TradeChestPayment"].InnerXml);

      XmlElement maxBankChestsElement = rootElement["MaxBankChests"];
      resultingConfig.MaxBankChests = new Dictionary<string,int>();
      foreach (XmlNode node in maxBankChestsElement) {
        XmlElement limitElement = node as XmlElement;
        if (limitElement != null)
          resultingConfig.MaxBankChests.Add(limitElement.GetAttribute("Group"), int.Parse(limitElement.InnerXml));
      }

      XmlElement tradeChestItemGroupsElement = rootElement["TradeChestItemGroups"];
      resultingConfig.TradeChestItemGroups = new Dictionary<string,HashSet<int>>();
      foreach (XmlNode node in tradeChestItemGroupsElement) {
        XmlElement itemGroupElement = node as XmlElement;
        if (itemGroupElement != null) {
          string groupName = itemGroupElement.GetAttribute("Name").ToLowerInvariant();
          var itemIds = new HashSet<int>(itemGroupElement.InnerText.Split(',').Select(idRaw => int.Parse(idRaw)));
          resultingConfig.TradeChestItemGroups.Add(groupName, itemIds);
        }
      }

      return resultingConfig;
    }

    private static void UpdateTileIdArrayByString(bool[] idArray, string tileIds) {
      if (string.IsNullOrWhiteSpace(tileIds))
        return;

      foreach (string tileId in tileIds.Split(','))
        idArray[int.Parse(tileId)] = true;
    }

    public Configuration() {
      this.ManuallyProtectableTiles = new bool[TerrariaUtils.BlockType_Max + 50];
      this.AutoProtectedTiles = new bool[TerrariaUtils.BlockType_Max + 50];
      this.NotDeprotectableTiles = new bool[TerrariaUtils.BlockType_Max + 50];
      this.MaxBankChests = new Dictionary<string,int>();
    }
  }
}
