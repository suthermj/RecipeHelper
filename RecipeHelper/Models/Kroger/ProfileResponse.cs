namespace RecipeHelper.Models.Kroger
{
    public class ProfileResponse
    {

        public Data data { get; set; }

        public class Data
        {
            public string id { get; set; }
        }
    }
}