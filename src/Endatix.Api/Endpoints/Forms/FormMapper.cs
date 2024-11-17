﻿using Endatix.Core.Entities;
using Endatix.Core.UseCases.Forms;

namespace Endatix.Api.Endpoints.Forms;

/// <summary>
/// Mapper from a form entity to a form API model.
/// </summary>
public class FormMapper
{
    /// <summary>
    /// Maps a form entity to a form API model.
    /// </summary>
    /// <typeparam name="T">The type of the form API model, which inherits FormModel.</typeparam>
    /// <param name="form">The form entity.</param>
    /// <returns>The mapped form API model.</returns>
    public static T Map<T>(Form form) where T : FormModel, new() => new T
    {
        Id = form.Id.ToString(),
        Name = form.Name,
        Description = form.Description,
        IsEnabled = form.IsEnabled,
        CreatedAt = form.CreatedAt,
        ModifiedAt = form.ModifiedAt
    };

    /// <summary>
    /// Maps a collection of form entities to a collection of form API models.
    /// </summary>
    /// <typeparam name="T">The type of the form API model, which inherits FormModel.</typeparam>
    /// <param name="forms">The collection of form entities.</param>
    /// <returns>A collection of mapped form API models.</returns>
    public static IEnumerable<T> Map<T>(IEnumerable<Form> forms) where T : FormModel, new() =>
        forms.Select(Map<T>).ToList();
}

public static class FormMapperExtensions
{
    /// <summary>
    /// Extension method to expose the consumption of the <see cref="FormMapper.Map{T}(Form)"/> method.
    /// </summary>
    /// <param name="forms">The collection of form entities.</param>
    /// <returns>A collection of mapped form API models.</returns>
    public static IEnumerable<FormModel> ToFormModelList(this IEnumerable<Form> forms)
    {
        return forms.Select(FormMapper.Map<FormModel>);
    }

    public static IEnumerable<FormModel> ToFormModelList(this IEnumerable<FormDto> forms)
    {
        return forms.Select(formDto => formDto.ToFormModel());
    }

    public static FormModel ToFormModel(this FormDto formDto) => new FormModel()
    {
        Id = formDto.Id,
        Name = formDto.Name,
        Description = formDto.Description,
        IsEnabled = formDto.IsEnabled,
        CreatedAt = formDto.CreatedAt,
        ModifiedAt = formDto.ModifiedAt,
        SubmissionsCount = formDto.SubmissionsCount
    };
}
