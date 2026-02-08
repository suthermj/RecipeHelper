using System.Net.ServerSentEvents;
using RecipeHelper.Models.Kroger;
using RecipeHelper.Utility;

namespace RecipeHelper.Models.Kroger
{
    public class KrogerPackInfo
    {
        public string Upc { get; set; }

        public string SoldByRaw { get; set; }        

        // how the item is actually sold (inferred from SoldByRaw and Size)
        public string SoldByEffective { get; set; }  

        public decimal? PrimaryQty { get; set; }     // 15, 16.9, 1, 0.5
        public string? PrimaryUnit { get; set; }     // oz, fl oz, lb, gal, ct

        // only populated for composite packs (e.g., "8 ct / 22 oz")
        // defines how many pieces are in one pack
        public decimal? CountEach { get; set; }      

        // Indicates if the pack info represents a composite pack (e.g., "8 ct / 22 oz")
        public bool IsComposite { get; set; }

        // What unit is the pack dimension measured in
        public PackDimension Dimension { get; set; } // Unit / Weight / Volume / Unknown
        public bool ParsedOk { get; set; }

        public static KrogerPackInfo BuildPackInfo(KrogerProductDto product)
        {
            var pack = new KrogerPackInfo
            {
                Upc = product.upc,
                SoldByRaw = product.soldBy,
                ParsedOk = false
            };

            // 1️⃣ Parse size string
            var parsed = KrogerSizeParser.TryParse(product.size); // your new parser

            if (parsed != null)
            {
                pack.PrimaryQty = parsed.PrimaryQty;
                pack.PrimaryUnit = parsed.PrimaryUnit;
                pack.CountEach = parsed.CountEach;
                pack.IsComposite = parsed.IsComposite;
                pack.Dimension = parsed.Dimension;
                pack.ParsedOk = true;
            }

            // 2️⃣ Infer soldBy if missing
            pack.SoldByEffective = SoldByInference.InferSoldBy(
                product.soldBy,
                product.size,
                product.categories
            );

            return pack;
        }
    }

    public enum PackDimension
    {
        Unknown = 0,
        Unit = 1,     // ct, each, bunch
        Weight = 2,   // oz, lb, g
        Volume = 3,   // fl oz, cup, qt
        Composite = 4 // exposes multiple (ct + oz)
    }
}



