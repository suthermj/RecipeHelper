using RecipeHelper.Services;
using Xunit;

namespace RecipeHelper.Tests
{
    // A photo-imported Cinnamon Roll Bread showed "Ingredients / Filling / Icing" twice.
    // GetImportedRecipePreview resolves exact-match ingredients and fuzzy-searched (new)
    // ingredients separately, then appended the fuzzy ones after all the exact ones, and
    // SaveImportedRecipe stores that order as SortOrder -- so the yeast, cinnamon, powdered
    // sugar, vanilla paste and heavy cream (none of them known ingredients yet) landed at
    // the end and repeated their section headers.
    public class ImportPreviewOrderTests
    {
        [Fact]
        public void FuzzyMatchedIngredients_KeepTheirOriginalPosition()
        {
            // The recipe as printed: (name, section, already a known ingredient?)
            var lines = new (string Name, string Section, bool Known)[]
            {
                ("warm milk", "Ingredients", true),
                ("active dry yeast", "Ingredients", false),
                ("granulated sugar", "Ingredients", true),
                ("all-purpose flour", "Ingredients", true),
                ("butter", "Filling", true),
                ("brown sugar", "Filling", true),
                ("cinnamon", "Filling", false),
                ("powdered sugar", "Icing", false),
                ("cream cheese", "Icing", true),
                ("heavy cream", "Icing", false),
            };

            var slots = new string?[lines.Length];
            var gapIndices = new List<int>();
            var gapItems = new List<string>();
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Known) slots[i] = lines[i].Name;
                else { gapIndices.Add(i); gapItems.Add(lines[i].Name); }
            }

            var result = ImportService.FillInOriginalOrder(slots, gapIndices, gapItems);

            Assert.Equal(lines.Select(l => l.Name), result);

            // One header per section, i.e. each section's rows are contiguous.
            var sections = result.Select(n => lines.First(l => l.Name == n).Section).ToList();
            var sectionRuns = sections.Where((s, i) => i == 0 || sections[i - 1] != s).ToList();
            Assert.Equal(new[] { "Ingredients", "Filling", "Icing" }, sectionRuns);
        }
    }
}
