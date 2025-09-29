using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Soft1_To_Atum.Data.Models
{
    public class AtumModels
    {
        public class SnakeCaseNamingPolicy : JsonNamingPolicy
        {
            public override string ConvertName(string name)
            {
                if (string.IsNullOrEmpty(name)) return name;
                var sb = new StringBuilder();
                for (int i = 0; i < name.Length; i++)
                {
                    char c = name[i];
                    if (char.IsUpper(c))
                    {
                        if (sb.Length > 0) sb.Append('_');
                        sb.Append(char.ToLowerInvariant(c));
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                return sb.ToString();
            }
        }

        public class FlexibleStringConverter : System.Text.Json.Serialization.JsonConverter<string>
        {
            public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.String)
                    return reader.GetString();
                if (reader.TokenType == JsonTokenType.Number)
                    return reader.GetInt32().ToString(); // ή GetInt64() αν έχεις μεγάλα ids
                if (reader.TokenType == JsonTokenType.Null)
                    return null;

                throw new JsonException($"Unexpected token {reader.TokenType}");
            }

            public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value);
            }
        }


        public class Inventory
        {
            public int Id { get; set; }
            public int ProductId { get; set; }
            public string Name { get; set; }
            public int Priority { get; set; }
            public bool IsMain { get; set; }
            public DateTime InventoryDate { get; set; }
            public string Lot { get; set; }
            public bool WriteOff { get; set; }
            public List<object> Region { get; set; }
            public Dictionary<string, string> Location { get; set; }

            public DateTime? BbeDate { get; set; }
            public int ExpiryThreshold { get; set; }
            public string InboundStock { get; set; }
            public int StockOnHold { get; set; }
            public int SoldToday { get; set; }
            public int SalesLastDays { get; set; }
            public int ReservedStock { get; set; }
            public int CustomerReturns { get; set; }
            public int WarehouseDamage { get; set; }
            public int LostInPost { get; set; }
            public int OtherLogs { get; set; }
            public int OutStockDays { get; set; }
            public int LostSales { get; set; }
            public DateTime UpdateDate { get; set; }

            public MetaData MetaData { get; set; }
            [JsonPropertyName("_links")]
            public Links Links { get; set; }  // _links έχει πρόθεμα underscore — attribute για να είσαι safe
        }

        public class MetaData
        {
            public string Sku { get; set; }
            public bool ManageStock { get; set; }
            public int? StockQuantity { get; set; }     // nullable γιατί μπορεί να είναι null
            public string Backorders { get; set; }
            public string StockStatus { get; set; }

            [JsonConverter(typeof(FlexibleStringConverter))]
            public string SupplierId { get; set; }
            public string SupplierSku { get; set; }
            public string Barcode { get; set; }
            public bool SoldIndividually { get; set; }
            public string OutStockThreshold { get; set; }
            public string LowStockThreshold { get; set; }
            public string PurchasePrice { get; set; }
            public string Price { get; set; }
            public string RegularPrice { get; set; }
            public string SalePrice { get; set; }
            public DateTime? DateOnSaleFrom { get; set; }
            public DateTime? DateOnSaleTo { get; set; }
            public DateTime? OutStockDate { get; set; }
            public string ExpiredStock { get; set; }
            public bool IsExpired { get; set; }
        }

        public class Links
        {
            public List<LinkItem> Self { get; set; }
            public List<LinkItem> Collection { get; set; }
            public List<LinkItem> Up { get; set; }
        }
        public class LinkItem { public string Href { get; set; } }

    }
}
