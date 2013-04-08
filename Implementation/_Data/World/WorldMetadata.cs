using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using DPoint = System.Drawing.Point;

using Terraria.Plugins.Common;

using Newtonsoft.Json;
using Formatting = Newtonsoft.Json.Formatting;

namespace Terraria.Plugins.CoderCow.Protector {
  public class WorldMetadata: IMetadataFile {
    #region [Constants]
    protected const string CurrentVersion = "1.0";
    #endregion

    #region [Property: Version]
    private readonly string version;

    public string Version {
      get { return this.version; }
    }
    #endregion

    #region [Property: Protections]
    private Dictionary<DPoint,ProtectionEntry> protections;

    public Dictionary<DPoint,ProtectionEntry> Protections {
      get { return this.protections; }
      set { this.protections = value; }
    }
    #endregion


    #region [Methods: Static Read, Write]
    public static WorldMetadata Read(string filePath) {
      using (StreamReader fileReader = new StreamReader(filePath)) {
        return JsonConvert.DeserializeObject<WorldMetadata>(fileReader.ReadToEnd());
      }
    }

    public void Write(string filePath) {
      using (StreamWriter fileWriter = new StreamWriter(filePath)) {
        fileWriter.Write(JsonConvert.SerializeObject(this, Formatting.Indented));
      }
    }
    #endregion

    #region [Method: Constructor]
    public WorldMetadata() {
      this.version = WorldMetadata.CurrentVersion;
      this.protections = new Dictionary<DPoint,ProtectionEntry>(40);
    }
    #endregion

    #region [Method: CountUserProtections]
    public int CountUserProtections(int userId) {
      lock (this.Protections) {
        return this.Protections.Values.Count(p => p.Owner == userId);
      }
    }
    #endregion
  }
}
