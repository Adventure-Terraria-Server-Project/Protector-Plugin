using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using DPoint = System.Drawing.Point;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using TShockAPI;

using Terraria.Plugins.Common;
using Terraria.Plugins.Common.Collections;

namespace Terraria.Plugins.CoderCow.Protector {
  [JsonConverter(typeof(ProtectionEntry.ProtectionEntryConverter))]
  public class ProtectionEntry {
    #region [Nested: ProtectionEntryConverter]
    internal class ProtectionEntryConverter: CustomCreationConverter<ProtectionEntry> {
      public override ProtectionEntry Create(Type objectType) {
        return ProtectionEntry.DeserializationCreate();
      }
    }
    #endregion

    public int Owner { get; set; }
    public DPoint TileLocation { get; set; }
    public int BlockType { get; set; }
    public DateTime TimeOfCreation { get; set; }

    [JsonIgnore]
    public bool IsShared => (
      this.IsSharedWithEveryone ||
      this.SharedUsers != null ||
      this.SharedGroups != null
    );

    public bool IsSharedWithEveryone { get; set; }
    public Collection<int> SharedUsers { get; set; }
    public StringCollection SharedGroups { get; set; }
    public BankChestDataKey BankChestKey { get; set; }
    public RefillChestMetadata RefillChestData { get; set; }
    public TradeChestMetadata TradeChestData { get; set; }


    public ProtectionEntry(int owner, DPoint tileLocation, int blockType, DateTime timeOfCreation = default(DateTime)) {
      this.Owner = owner;
      this.TileLocation = tileLocation;
      this.BlockType = blockType;

      if (timeOfCreation == default(DateTime))
        timeOfCreation = DateTime.UtcNow;
      this.TimeOfCreation = timeOfCreation;
    }

    private ProtectionEntry() {}

    private static ProtectionEntry DeserializationCreate() {
      return new ProtectionEntry { BlockType = -1 };
    }

    public bool IsSharedWithPlayer(TSPlayer player) {
      return (
        player.IsLoggedIn && (
          this.IsSharedWithEveryone ||
          (this.SharedUsers != null && this.SharedUsers.Contains(player.User.ID)) || 
          (this.SharedGroups != null && this.SharedGroups.Contains(player.Group.Name))
        )
      );
    }

    public void Unshare() {
      this.SharedUsers = null;
      this.SharedGroups = null;
      this.IsSharedWithEveryone = false;
    }

    public override string ToString() {
      return string.Format("{{Owner={0} TileLocation={1}}}", this.Owner, this.TileLocation);
    }
  }
}
