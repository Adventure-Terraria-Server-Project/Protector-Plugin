using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Terraria.Plugins.Common;
using DPoint = System.Drawing.Point;

namespace Terraria.Plugins.CoderCow.Protector {
  [JsonObject(MemberSerialization.OptIn)]
  public class ProtectorChestData: IChest {
    public bool IsWorldChest => false;
    public DPoint Location { get; }
    [JsonProperty]
    public ItemData[] Content => this.Items.ToArray();
    public IList<ItemData> Items { get; }

    public int Index {
      get { throw new NotSupportedException(); }
    }

    public string Name {
      get { return null; }
      set { throw new NotSupportedException(); }
    }

    public ProtectorChestData(DPoint location, ItemData[] content = null) {
      this.Location = location;

      if (content != null)
        this.Items = content.Clone() as ItemData[];
      else
        this.Items = new ItemData[Chest.maxItems];
    }

    public ItemData[] ContentAsArray() {
      return (ItemData[])this.Content.Clone();
    }
  }
}