using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using Terraria.Plugins.Common;
using DPoint = System.Drawing.Point;

namespace Terraria.Plugins.CoderCow.Protector {
  public class ChestAdapter: IChest {
    private readonly Chest tChest;

    public bool IsWorldChest => true;

    public string Name {
      get { return this.tChest.name; }
      set { this.tChest.name = value; }
    }

    public DPoint Location => new DPoint(this.tChest.x, this.tChest.y);
    public int Index { get; }
    public ItemData this[int slotIndex] => ItemData.FromItem(this.tChest.item[slotIndex]);

    public ChestAdapter(int chestIndex, Chest tChest) {
      Contract.Requires<ArgumentNullException>(tChest != null);

      this.Index = chestIndex;
      this.tChest = tChest;
    }

    public void SetItem(int slot, ItemData item) {
      this.tChest.item[slot] = item.ToItem();
    }
    
    public ItemData[] ContentAsArray() {
      ItemData[] array = new ItemData[Chest.maxItems];
      for (int i = 0; i < Chest.maxItems; i++)
        array[i] = ItemData.FromItem(this.tChest.item[i]);
      
      return array;
    }
  }
}
