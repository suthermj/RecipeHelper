using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace RecipeHelper.ViewModels;

public class ImportRecipePageVM
{
    public string? Url { get; set; }              // bound from ?Url=...
    public string? Error { get; set; }            // optional error message
    public ImportRecipeVM? Preview { get; set; } // filled after fetch
}

public class ImportRecipeVM
{
    public string Title { get; set; } = "";
    public string? Image { get; set; }
    public string SourceUrl { get; set; } = "";
    public int? ReadyInMinutes { get; set; }
    public int? Servings { get; set; }

    // badges
    public bool Vegetarian { get; set; }
    public bool Vegan { get; set; }
    public bool GlutenFree { get; set; }
    public bool DairyFree { get; set; }

    public string? SummaryText { get; set; }
    public List<ImportIngredientVM> Ingredients { get; set; } = new();
    public List<string> Steps { get; set; } = new();

    // -------- mapping helper from your Spoonacular DTO --------
    public static ImportRecipeVM FromSpoonacular(Models.Spoonacular.Recipe dto)
    {
        string? PickAmount(RecipeHelper.Models.Spoonacular.Extendedingredient ei)
        {
            // prefer US measure if present, fall back to metric, or the generic amount+unit
            if (ei?.measures?.us is { } us && us.amount > 0)
                return $"{TrimZeros(us.amount)} {us.unitShort}".Trim();
            if (ei?.measures?.metric is { } m && m.amount > 0)
                return $"{TrimZeros(m.amount)} {m.unitShort}".Trim();
            if (ei is { amount: > 0 })
                return $"{TrimZeros(ei.amount)} {ei.unit}".Trim();
            return ei?.original; // last resort: the raw line
        }

        static string TrimZeros(float n)
        {
            var s = n.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return s.Contains('.') ? s.TrimEnd('0').TrimEnd('.') : s;
        }

        static string StripHtml(string? html)
        {
            if (string.IsNullOrWhiteSpace(html)) return null;
            // spoonacular summary can include HTML; keep it safe
            return Regex.Replace(html, "<.*?>", string.Empty).Trim();
        }

        static string FirstNonEmpty(params string[] values)
        {
            return values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "UNKNOWN";
        }

        var vm = new ImportRecipeVM
        {
            Title = dto.title ?? "Untitled",
            Image = dto.image,
            SourceUrl = dto.sourceUrl ?? "",
            ReadyInMinutes = dto.readyInMinutes == 0 ? null : dto.readyInMinutes,
            Servings = dto.servings == 0 ? null : dto.servings,
            Vegetarian = dto.vegetarian,
            Vegan = dto.vegan,
            GlutenFree = dto.glutenFree,
            DairyFree = dto.dairyFree,
            SummaryText = StripHtml(dto.summary)
        };

        // ingredients
        if (dto.extendedIngredients != null)
        {
            foreach (var ei in dto.extendedIngredients)
            {
                vm.Ingredients.Add(new ImportIngredientVM
                {
                    Name = FirstNonEmpty(ei.name, ei.originalName, ei.original, "UNKNOWN"),
                    DisplayAmount = PickAmount(ei),
                    Amount = ei.amount,
                    Unit = ei.unit

                }); 
            }
        }

        // steps (flatten analyzedInstructions)
        if (dto.analyzedInstructions != null)
        {
            vm.Steps = dto.analyzedInstructions
                .Where(ai => ai?.steps != null)
                .SelectMany(ai => ai.steps)
                .OrderBy(s => s.number)
                .Select(s => s.step)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
        }
        // fallback: single instructions string split by periods
        if (vm.Steps.Count == 0 && !string.IsNullOrWhiteSpace(dto.instructions))
        {
            vm.Steps = dto.instructions
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();
        }

        return vm;
    }
}

public class ImportIngredientVM
{
    public string Name { get; set; } = "";
    public string? DisplayAmount { get; set; }  // e.g. "1 cup" or "200 g"
    public float? Amount { get; set; }          // e.g. 1 or 200
    public string? Unit { get; set; }        // e.g. "cup" or "g"
}

public class ImportRecipeQueryVM { public string? Uri { get; set; } }

// ViewModels/SaveImportedRecipeVM.cs
public class PreviewImportedRecipeVM
{
    public string Title { get; set; } = "";
    public string? Image { get; set; }

    public List<PreviewImportedIngredientVM> Ingredients { get; set; } = new();

}

public class PreviewImportedIngredientVM
{
    public string Name { get; set; } = "";   // editable
    public decimal? Amount { get; set; }       // editable
    public string? Unit { get; set; }        // editable
    //public int? ProductId { get; set; }      // optional mapping to your Product
}

public class MappedImportedRecipeVM
{
    [Required]
    public string Title { get; set; } = "";

    public string? Image { get; set; }

    // Each imported ingredient with suggestions + Kroger fallback
    [MinLength(1)]
    public List<IngredientPreviewVM> Ingredients { get; set; } = new();
}

public class IngredientPreviewVM
{
    // Source (from Spoonacular parsing)
    [Required]
    public string Name { get; set; } = "";

    public decimal? Amount { get; set; }   // e.g., 2
    public string? Unit { get; set; }    // e.g., "cloves", "tsp", "g"
    public bool Include { get; set; }
    public int? SuggestedProductId { get; set; }
    public string? SuggestedProductUpc { get; set; }
    public string? SuggestedProductName { get; set; }
    public string? SuggestionKind { get; set; }
    public int? ProductId { get; set; }
    public string? SelectedProductLabel { get; set; } // what to show in the search box on reload
    //public string Upc { get; set; } = "";
    public string? KrogerUpc { get; set; }            // chosen Kroger UPC (suggested or search)
    public string? KrogerName { get; set; }           // optional (for display/rehydrate)
    public string? KrogerImage { get; set; }          // optional
    // If true, prefer Kroger item even if ProductId is null/0
    public bool UseKroger { get; set; }

    public KrogerPreviewVM? Kroger { get; set; }
}

public class KrogerPreviewVM
{
    public string? Name { get; set; }
    public string? ImageUrl { get; set; }
    public string? Upc { get; set; }
}