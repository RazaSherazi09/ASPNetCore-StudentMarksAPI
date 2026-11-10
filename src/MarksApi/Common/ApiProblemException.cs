namespace MarksApi.Common;

public class ApiProblemException : Exception
{
    public int StatusCode { get; }
    public string Title { get; }
    public string Detail { get; }

    public ApiProblemException(int statusCode, string title, string detail)
        : base(detail)
    {
        StatusCode = statusCode;
        Title = title;
        Detail = detail;
    }
}

