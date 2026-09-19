namespace HyperVToolsX.Infrastructure.Collection;

public class CollectionSectionResult<T>
{
    public bool Success { get; set; }

    public T? Data { get; set; }

    public string ErrorMessage { get; set; } = string.Empty;

    public TimeSpan Duration { get; set; }

    public static CollectionSectionResult<T> Successful(
        T data,
        TimeSpan duration)
    {
        return new CollectionSectionResult<T>
        {
            Success = true,
            Data = data,
            Duration = duration
        };
    }

    public static CollectionSectionResult<T> Failed(
        string errorMessage,
        TimeSpan duration)
    {
        return new CollectionSectionResult<T>
        {
            Success = false,
            ErrorMessage = errorMessage,
            Duration = duration
        };
    }
}