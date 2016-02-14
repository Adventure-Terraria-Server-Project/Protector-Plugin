using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.Plugins.Common;
using DPoint = System.Drawing.Point;

namespace Terraria.Plugins.CoderCow.Protector {
  public interface IChest {
    bool IsWorldChest { get; }
    string Name { get; set; }
    DPoint Location { get; }
    int Index { get; }
    ItemData this[int slotIndex] { get; }

    void SetItem(int slot, ItemData item);
    ItemData[] ContentAsArray();
  }
}
