namespace Reviews.Common.Events;

public static class CacheTags
{
    public const string Reviews = "Reviews";
    /// <summary>Category cache is also invalidated when reviews change (category listings show aggregate ratings).</summary>
    public const string Categories = "Categories";
}




