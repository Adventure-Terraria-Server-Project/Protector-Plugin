using System;

using Terraria.Plugins.Common;

namespace Terraria.Plugins.CoderCow.Protector {
  public class BankChestMetadata {
    #region [Property: Items]
    private ItemData[] items;

    public ItemData[] Items {
      get { return this.items; }
      set { this.items = value; }
    }
    #endregion


    #region [Method: Constructor]
    public BankChestMetadata(): base() {
      this.items = new ItemData[20];
    }
    #endregion
  }
}
