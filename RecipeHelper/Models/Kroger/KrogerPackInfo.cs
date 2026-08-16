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

        // True when PrimaryQty/PrimaryUnit/Dimension came from Kroger's own nutrition-
        // panel serving data (servingSize x servingsPerPackage) rather than parsing the
        // free-text size string. Exposed mainly so callers/tests can tell which source
        // produced a given pack figure.
        public bool FromServingData { get; set; }

        public static KrogerPackInfo BuildPackInfo(KrogerProductDto product)
        {
            var pack = new KrogerPackInfo
            {
                Upc = product.upc,
                SoldByRaw = product.soldBy,
                ParsedOk = false
            };

            // 1️⃣ Prefer Kroger's own nutrition-panel serving data when present:
            // servingSize x servingsPerPackage is the manufacturer-reported total pack
            // content in a real cooking/weight unit (e.g. "1 tsp" x 45 servings = 45 tsp
            // of minced garlic; "240 ml" x 4 = 960 ml of broth). A free-text size string
            // like "8 oz" or "32 oz" is ambiguous (fluid vs weight ounces isn't stated)
            // and, when it doesn't share a dimension with the ingredient, forces a guess
            // at the product's density -- serving data sidesteps both problems entirely
            // when its unit is one we recognize.
            var servingUnit = string.IsNullOrWhiteSpace(product.servingSizeUnitAbbreviation)
                ? MeasureUnit.Unknown
                : UnitConverter.Parse(product.servingSizeUnitAbbreviation);

            if (product.servingSizeQty is > 0 && product.servingsPerPackage is > 0 && servingUnit != MeasureUnit.Unknown)
            {
                pack.PrimaryQty = product.servingSizeQty * product.servingsPerPackage;
                pack.PrimaryUnit = product.servingSizeUnitAbbreviation;
                pack.Dimension = ToPackDimension(UnitConverter.GetDimension(servingUnit));
                pack.IsComposite = false;
                pack.CountEach = null;
                pack.ParsedOk = true;
                pack.FromServingData = true;
            }
            else
            {
                // 2️⃣ Fall back to parsing the size string. TryParse always returns an
                // instance (never null) -- it signals failure via parsed.ParsedOk (e.g.
                // "Varies", "N/A", "1 lb 4 oz" all fail to match either regex). Must
                // propagate that flag rather than assuming success just because an object
                // came back, or every unparseable size silently skips the "could not
                // parse product size" fallback in KrogerService.ConvertIngredientsToCartItems.
                var parsed = KrogerSizeParser.TryParse(product.size);

                pack.PrimaryQty = parsed.PrimaryQty;
                pack.PrimaryUnit = parsed.PrimaryUnit;
                pack.CountEach = parsed.CountEach;
                pack.IsComposite = parsed.IsComposite;
                pack.Dimension = parsed.Dimension;
                pack.ParsedOk = parsed.ParsedOk;
            }

            // 3️⃣ Infer soldBy if missing
            pack.SoldByEffective = SoldByInference.InferSoldBy(
                product.soldBy,
                product.size,
                product.categories
            );

            return pack;
        }

        private static PackDimension ToPackDimension(MeasureDimension dim) => dim switch
        {
            MeasureDimension.Volume => PackDimension.Volume,
            MeasureDimension.Weight => PackDimension.Weight,
            MeasureDimension.Count => PackDimension.Unit,
            _ => PackDimension.Unknown,
        };
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



