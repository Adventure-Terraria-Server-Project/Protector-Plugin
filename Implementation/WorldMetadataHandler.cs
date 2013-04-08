using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;

using Terraria.Plugins.Common;

namespace Terraria.Plugins.CoderCow.Protector {
  public class WorldMetadataHandler: WorldMetadataHandlerBase {
    #region [Property: WorldMetadata]
    public new WorldMetadata Metadata {
      get { return (WorldMetadata)base.Metadata; }
    }
    #endregion


    #region [Methods: Constructor, InitMetadata, ReadMetadataFromFile]
    public WorldMetadataHandler(PluginTrace pluginTrace, string metadataDirectoryPath): 
      base(pluginTrace, metadataDirectoryPath) {}

    protected override IMetadataFile InitMetadata() {
      return new WorldMetadata();
    }

    protected override IMetadataFile ReadMetadataFromFile(string filePath) {
      WorldMetadata result = WorldMetadata.Read(filePath);
      if (result == null)
        throw new FormatException();

      return result;
    }
    #endregion
  }
}
