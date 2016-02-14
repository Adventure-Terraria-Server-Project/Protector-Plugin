using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.Plugins.Common;
using TShockAPI;

namespace Terraria.Plugins.CoderCow.Protector {
  public static class PacketUtils {
    public static void SendChestItem(TSPlayer player, int chestIndex, IList<ItemData> items) {
      const int TerrariaPacketHeaderSize = 3;
      const int ChestItemPacketSizeNoHeader = 8;
      const short PacketSize = TerrariaPacketHeaderSize + ChestItemPacketSizeNoHeader;

      using (MemoryStream packetData = new MemoryStream(new byte[PacketSize])) {
        BinaryWriter writer = new BinaryWriter(packetData);

        // Header
        writer.Write(PacketSize); // Packet size
        writer.Write((byte)PacketTypes.ChestItem);

        writer.Write((short)chestIndex);

        // Re-write item data for each item and send the packet
        for (int i = 0; i < items.Count; i++) {
          ItemData item = items[i];

          writer.Write((byte)i);
          writer.Write((short)item.StackSize);
          writer.Write((byte)item.Prefix);
          writer.Write((short)item.Type);

          player.SendRawData(packetData.ToArray());

          // Rewind to write the item data of another item
          packetData.Position -= ChestItemPacketSizeNoHeader;
        }
      }
    }

    public static void SendChestName(TSPlayer player, int chestIndex, string name) {
      
    }
  }
}
