// Placeholder for the generated Step 3 Swagger client's API exception shape.
namespace DataRetriever.Infrastructure.Step3Load;

public class ApiException(
    int statusCode,
    string? message = null,
    Exception? innerException = null) : Exception(message, innerException)
{
    public int StatusCode { get; } = statusCode;
}
