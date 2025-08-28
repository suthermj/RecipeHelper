namespace RecipeHelper.Models.Spoonacular
{

    public class Recipe
    {
        public string image { get; set; }
        public string title { get; set; }
        public int? readyInMinutes { get; set; }
        public int? servings { get; set; }
        public string sourceUrl { get; set; }
        public bool vegetarian { get; set; }
        public bool vegan { get; set; }
        public bool glutenFree { get; set; }
        public bool dairyFree { get; set; }
        public Extendedingredient[] extendedIngredients { get; set; }
        public string summary { get; set; }
        public string instructions { get; set; }
        public Analyzedinstruction[] analyzedInstructions { get; set; }
    }

    public class Extendedingredient
    {
        public string name { get; set; }
        public string original { get; set; }
        public string originalName { get; set; }
        public float amount { get; set; }
        public string unit { get; set; }
        public Measures measures { get; set; }
    }

    public class Measures
    {
        public Us us { get; set; }
        public Metric metric { get; set; }
    }

    public class Us
    {
        public float amount { get; set; }
        public string unitShort { get; set; }
    }

    public class Metric
    {
        public float amount { get; set; }
        public string unitShort { get; set; }
    }

    public class Analyzedinstruction
    {
        public string name { get; set; }
        public Step[] steps { get; set; }
    }

    public class Step
    {
        public int number { get; set; }
        public string step { get; set; }
    }
}