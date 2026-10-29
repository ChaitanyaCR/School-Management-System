using Microsoft.AspNetCore.Http;
using SchoolMs.SharedKernel;

namespace SchoolMs.Infrastructure.Http;

/// <summary>Maps <see cref="Result"/> failures to RFC 9457 problem responses with consistent status codes.</summary>
public static class ResultHttpExtensions
{
    public static IResult ToProblem(this Error error) =>
        Results.Problem(
            statusCode: error.Type switch
            {
                ErrorType.Validation => StatusCodes.Status400BadRequest,
                ErrorType.NotFound => StatusCodes.Status404NotFound,
                ErrorType.Conflict => StatusCodes.Status409Conflict,
                ErrorType.Forbidden => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status422UnprocessableEntity,
            },
            title: error.Message,
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });

    public static IResult ToHttpResult<TValue>(this Result<TValue> result) =>
        result.IsSuccess ? Results.Ok(result.Value) : result.Error.ToProblem();

    public static IResult ToHttpResult(this Result result) =>
        result.IsSuccess ? Results.NoContent() : result.Error.ToProblem();
}
