using System.Text.Json;
using Endatix.Core.Models.Themes;
using FastEndpoints;
using FluentValidation;

namespace Endatix.Api.Endpoints.Themes.Update;

public class UpdateValidator : Validator<UpdateRequest>
{
    public UpdateValidator()
    {
        RuleFor(x => x.ThemeId)
            .GreaterThan(0);

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required.")
            .MaximumLength(100)
            .WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .WithMessage("Description must not exceed 500 characters.");

        RuleFor(x => x.JsonData)
            .Must((x, jsonData) => BeValidJson(jsonData ?? string.Empty))
            .WithMessage("Invalid JSON data provided.");
    }

    private bool BeValidJson(string jsonData)
    {
        if (string.IsNullOrEmpty(jsonData))
        {
            return true; // Allow empty JSON data
        }

        try
        {
            JsonSerializer.Deserialize<ThemeData>(jsonData);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}