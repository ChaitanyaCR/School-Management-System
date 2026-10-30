using Microsoft.AspNetCore.Http;
using SchoolMs.SharedKernel;

namespace SchoolMs.Infrastructure.Http;

/// <summary>Maps <see cref="Result"/> failures to RFC 9457 problem responses with consistent status codes.</summary>
public static class ResultHttpExtensions
{
    public static IResult ToProblem(this Error error) =>
        error.Fields is { } fields
            ? ApiProblems.Validation(error.Code, error.Message, fields.ToDictionary())
            : ApiProblems.Create(StatusCode(error.Type), error.Code, error.Message);

    public static IResult ToHttpResult<TValue>(this Result<TValue> result) =>
        result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();

    public static IResult ToHttpResult(this Result result) =>
        result.IsSuccess ? Results.NoContent() : result.Error.ToProblem();

    private static int StatusCode(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        _ => StatusCodes.Status422UnprocessableEntity,
    };
}

/// <summary>Problem responses carrying the stable machine-readable <c>code</c> extension the UI relies on.</summary>
public static class ApiProblems
{
    public static IResult Create(int status, string code, string title) =>
        Results.Problem(statusCode: status, title: title, extensions: new Dictionary<string, object?> { ["code"] = code });

    /// <summary>A 400 response listing failures per field, e.g. <c>{ "password": ["Too short."] }</c>.</summary>
    public static IResult Validation(string code, string title, IDictionary<string, string[]> errors) =>
        Results.ValidationProblem(errors, title: title, extensions: new Dictionary<string, object?> { ["code"] = code });
}
