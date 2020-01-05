using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using Terraria.Plugins.Common;
using Terraria.Plugins.Common.Collections;
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
    public IList<ItemData> Items { get; }

    public ChestAdapter(int chestIndex, Chest tChest) {
      if (tChest == null) throw new ArgumentNullException();

      this.Index = chestIndex;
      this.tChest = tChest;
      this.Items = new ItemsAdapter(tChest.item);
    }
  }
}
