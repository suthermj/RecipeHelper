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
    public static ImportRecipeVM FromSpoonacular(RecipeHelper.Models.Spoonacular.Recipe dto)
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
                    Name = ei.name ?? ei.originalName ?? "ingredient",
                    DisplayAmount = PickAmount(ei)
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
}

public class ImportRecipeQueryVM { public string? Uri { get; set; } }
