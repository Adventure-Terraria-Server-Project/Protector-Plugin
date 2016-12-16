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
    protected const string CurrentVersion = "1.4";

    public string Version { get; set; }
    public Dictionary<DPoint,ProtectionEntry> Protections { get; set; }
    public Dictionary<DPoint,ProtectorChestData> ProtectorChests { get; set; }

    public static WorldMetadata Read(string filePath) {
      using (StreamReader fileReader = new StreamReader(filePath)) {
        WorldMetadata data = JsonConvert.DeserializeObject<WorldMetadata>(fileReader.ReadToEnd(), new JsonUnixTimestampConverter());

        if (data.ProtectorChests == null)
          data.ProtectorChests = new Dictionary<DPoint,ProtectorChestData>(20);

        return data;
      }
    }

    public void Write(string filePath) {
      this.Version = WorldMetadata.CurrentVersion;

      using (StreamWriter fileWriter = new StreamWriter(filePath)) {
        fileWriter.Write(JsonConvert.SerializeObject(this, Formatting.Indented, new JsonUnixTimestampConverter()));
      }
    }


    public WorldMetadata() {
      this.Version = WorldMetadata.CurrentVersion;
      this.Protections = new Dictionary<DPoint,ProtectionEntry>(40);
      this.ProtectorChests = new Dictionary<DPoint,ProtectorChestData>(20);
    }

    public int CountUserProtections(int userId) {
      lock (this.Protections) {
        return this.Protections.Values.Count(p => p.Owner == userId);
      }
    }
  }
}
