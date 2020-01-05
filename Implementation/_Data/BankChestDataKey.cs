using System;

using Newtonsoft.Json;

namespace Terraria.Plugins.CoderCow.Protector {
  [JsonConverter(typeof(BankChestDataKey.CJsonConverter))]
  public struct BankChestDataKey {
    #region [Nested: CJsonConverter Class]
    public class CJsonConverter: JsonConverter {
      public override bool CanConvert(Type objectType) {
        return (objectType == typeof(BankChestDataKey));
      }

      public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
        if (reader.TokenType != JsonToken.String)
          return BankChestDataKey.Invalid;

        string[] rawData = ((string)reader.Value).Split(',');
        return new BankChestDataKey(
          int.Parse(rawData[0]), int.Parse(rawData[1])
        );
      }

      public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
        BankChestDataKey key = (BankChestDataKey)value;
        writer.WriteValue(string.Format("{0}, {1}", key.UserId, key.BankChestIndex));
      }
    }
    #endregion

    public static readonly BankChestDataKey Invalid = default(BankChestDataKey);

    public int UserId { get; private set; }
    public int BankChestIndex { get; private set; }


    public BankChestDataKey(int userId, int bankChestIndex): this() {
      this.UserId = userId;
      this.BankChestIndex = bankChestIndex;
    }

    public override int GetHashCode() {
      return this.UserId ^ this.BankChestIndex;
    }

    public bool Equals(BankChestDataKey other) {
      return (
        this.UserId == other.UserId &&
        this.BankChestIndex == other.BankChestIndex
      );
    }

    public override bool Equals(object obj) {
      if (!(obj is BankChestDataKey))
        return false;

      return this.Equals((BankChestDataKey)obj);
    }

    public static bool operator ==(BankChestDataKey a, BankChestDataKey b) {
      return a.Equals(b);
    }

    public static bool operator !=(BankChestDataKey a, BankChestDataKey b) {
      return !a.Equals(b);
    }

    public override string ToString() {
      return string.Format("{{UserId = {0}, BankChestIndex = {1}}}", this.UserId, this.BankChestIndex);
    }
  }
}
