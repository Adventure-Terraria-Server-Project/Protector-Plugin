using System;

using Terraria.Plugins.Common;

namespace Terraria.Plugins.CoderCow.Protector {
  public class BankChestMetadata {
    #region [Property: Items]
    private ItemMetadata[] items;

    public ItemMetadata[] Items {
      get { return this.items; }
      set { this.items = value; }
    }
    #endregion


    #region [Method: Constructor]
    public BankChestMetadata(): base() {
      this.items = new ItemMetadata[20];
    }
    #endregion
  }
}
