using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;
using ValidationException = SmartGarage.Domain.Exceptions.ValidationException;

namespace SmartGarage.API.Filters;

/// <summary>
/// Runs the FluentValidation validator for every action argument that has one, before the
/// action executes. Controllers therefore never validate and never forget to.
/// <para>
/// Failures are keyed by camelCase property name, which is what both Flutter clients use to
/// put each message under the control it belongs to.
/// </para>
/// </summary>
public sealed class ValidationFilter : IAsyncActionFilter
{
    private readonly IServiceProvider _services;

    public ValidationFilter(IServiceProvider services)
    {
        _services = services;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());

            if (_services.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var result = await validator.ValidateAsync(
                new ValidationContext<object>(argument), context.HttpContext.RequestAborted);

            if (!result.IsValid)
            {
                throw new ValidationException(
                    result.Errors
                        .GroupBy(e => ToCamelCasePath(e.PropertyName), StringComparer.Ordinal)
                        .ToDictionary(
                            g => g.Key,
                            g => g.Select(e => e.ErrorMessage).ToArray(),
                            StringComparer.Ordinal));
            }
        }

        await next();
    }

    private static string ToCamelCasePath(string propertyPath) =>
        string.Join('.', propertyPath.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
}
