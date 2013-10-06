using System;

using Terraria.Plugins.Common;

namespace Terraria.Plugins.CoderCow.Protector {
  public class BankChestMetadata {
    public ItemData[] Items { get; set; }


    public BankChestMetadata(): base() {
      this.Items = new ItemData[Chest.maxItems];
    }
  }
}
