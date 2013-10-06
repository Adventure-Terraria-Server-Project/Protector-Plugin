using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using DPoint = System.Drawing.Point;

using Terraria.Plugins.Common;

namespace Terraria.Plugins.CoderCow.Protector {
  public class WorldMetadataHandler: WorldMetadataHandlerBase {
    public new WorldMetadata Metadata {
      get { return (WorldMetadata)base.Metadata; }
    }


    public WorldMetadataHandler(PluginTrace pluginTrace, string metadataDirectoryPath): 
      base(pluginTrace, metadataDirectoryPath) {}

    protected override IMetadataFile InitMetadata() {
      return new WorldMetadata();
    }

    protected override IMetadataFile ReadMetadataFromFile(string filePath) {
      WorldMetadata result = WorldMetadata.Read(filePath);
      if (result == null)
        throw new FormatException();

      Version fileVersion = new Version(result.Version);

      // Ensure compatibility with older versions
      if (fileVersion < new Version(1, 2)) {
        foreach (KeyValuePair<DPoint,ProtectionEntry> protectionPair in result.Protections) {
          DPoint location = protectionPair.Key;
          ProtectionEntry protection = protectionPair.Value;

          if (protection.BlockType == BlockType.Invalid) {
            Tile tile = TerrariaUtils.Tiles[location];
            if (tile.active())
              protection.BlockType = (BlockType)tile.type;
          }
        }
      }

      return result;
    }
  }
}
