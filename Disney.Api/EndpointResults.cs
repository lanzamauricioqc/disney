namespace Disney.Api;

/// <summary>
/// The single place where application-layer failures become HTTP responses,
/// so every endpoint reports validation and conflict errors the same way.
/// </summary>
internal static class EndpointResults
{
    public static IResult ValidationProblem(string field, string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [field] = [message]
        });

    public static async Task<IResult> ExecuteAsync(
        Func<Task<IResult>> execute,
        string fallbackField)
    {
        try
        {
            return await execute();
        }
        catch (ArgumentException exception)
        {
            return ValidationProblem(
                exception.ParamName ?? fallbackField,
                exception.Message);
        }
    }

    public static Task<IResult> ExecuteOkAsync<TResult>(
        Func<Task<TResult>> execute,
        string fallbackField) =>
        ExecuteAsync(
            async () => Results.Ok(await execute()),
            fallbackField);

    public static Task<IResult> ExecuteFoundAsync<TResult>(
        Func<Task<TResult?>> execute,
        string fallbackField)
        where TResult : class =>
        ExecuteAsync(
            async () => await execute() is { } result
                ? Results.Ok(result)
                : Results.NotFound(),
            fallbackField);

    public static async Task<IResult> ExecuteWithConflictAsync(
        Func<Task<IResult>> execute,
        string fallbackField)
    {
        try
        {
            return await ExecuteAsync(execute, fallbackField);
        }
        catch (InvalidOperationException exception)
        {
            return Results.Conflict(new { detail = exception.Message });
        }
    }
}
