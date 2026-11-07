using System.Text.Json;
using Endatix.Api.Endpoints.Forms;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Forms.Create;
using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Endatix.Api.Tests.Endpoints.Forms;

public class CreateTests
{
    private readonly IMediator _mediator;
    private readonly Create _endpoint;

    public CreateTests()
    {
        _mediator = Substitute.For<IMediator>();
        _endpoint = Factory.Create<Create>(_mediator);
    }

    private static JsonElement Schema(string json) =>
        JsonSerializer.Deserialize<JsonElement>(json);

    [Fact]
    public async Task ExecuteAsync_InvalidRequest_ReturnsProblemRequest()
    {
        // Arrange
        var request = new CreateFormRequest
        {
            Name = "Test Form",
            IsEnabled = true,
            FormDefinitionSchema = Schema("""{ "type": "object" }""")
        };

        var result = Result.Invalid();

        _mediator.Send(Arg.Any<CreateFormCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var problemResult = response.Result as ProblemHttpResult;
        problemResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsCreatedWithForm()
    {
        // Arrange
        var request = new CreateFormRequest
        {
            Name = "Test Form",
            Description = "Test Description",
            IsEnabled = true,
            FormDefinitionSchema = Schema("""{ "type": "object" }""")
        };

        FormCreateArgs args = new(TenantId: SampleData.TENANT_ID, Name: request.Name);
        var form = Form.Create(args);
        form.Id = 1;
        var result = Result<Form>.Created(form);

        _mediator.Send(Arg.Any<CreateFormCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, TestContext.Current.CancellationToken);

        // Assert
        var createdResult = response.Result as Created<CreateFormResponse>;
        createdResult.Should().NotBeNull();
        createdResult!.Value.Should().NotBeNull();
        createdResult!.Value!.Id.Should().Be(form.Id.ToString());
        createdResult!.Value!.Name.Should().Be(form.Name);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMapRequestToCommandCorrectly()
    {
        // Arrange
        var schema = Schema("""{ "type": "object" }""");
        var request = new CreateFormRequest
        {
            Name = "Test Form",
            Description = "Test Description",
            IsEnabled = true,
            FormDefinitionSchema = schema
        };
        var result = Result<Form>.Created(Form.Create(new FormCreateArgs(TenantId: SampleData.TENANT_ID, Name: "Test Form")));

        _mediator.Send(Arg.Any<CreateFormCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        var expectedJson = JsonSerializer.Serialize(schema);
        await _mediator.Received(1).Send(
            Arg.Is<CreateFormCommand>(cmd =>
                cmd.Name == request.Name &&
                cmd.Description == request.Description &&
                cmd.IsEnabled == request.IsEnabled &&
                cmd.FormDefinitionJsonData == expectedJson
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task ExecuteAsync_WithFormDefinitionSchema_ShouldSerializeToJsonString()
    {
        // Arrange
        var jsonSchema = Schema("""
            {
                "pages": [
                    {
                        "elements": [
                            {
                                "type": "text",
                                "name": "question1"
                            }
                        ]
                    }
                ]
            }
            """);

        var request = new CreateFormRequest
        {
            Name = "Test Form",
            Description = "Test Description",
            IsEnabled = true,
            FormDefinitionSchema = jsonSchema
        };
        var result = Result<Form>.Created(Form.Create(new FormCreateArgs(TenantId: SampleData.TENANT_ID, Name: "Test Form")));

        _mediator.Send(Arg.Any<CreateFormCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<CreateFormCommand>(cmd =>
                cmd.Name == request.Name &&
                cmd.Description == request.Description &&
                cmd.IsEnabled == request.IsEnabled &&
                cmd.FormDefinitionJsonData != null &&
                cmd.FormDefinitionJsonData.Contains("pages") &&
                cmd.FormDefinitionJsonData.Contains("question1")
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task ExecuteAsync_WithWebHookSettings_ShouldSerializeToJsonString()
    {
        // Arrange
        var webhookSettings = Schema("""
            {
                "events": {
                    "SubmissionCreated": {
                        "isEnabled": true
                    }
                }
            }
            """);

        var request = new CreateFormRequest
        {
            Name = "Test Form",
            Description = "Test Description",
            IsEnabled = true,
            FormDefinitionSchema = Schema("""{ "type": "object" }"""),
            WebHookSettings = webhookSettings
        };
        var result = Result<Form>.Created(Form.Create(new FormCreateArgs(TenantId: SampleData.TENANT_ID, Name: "Test Form")));

        _mediator.Send(Arg.Any<CreateFormCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<CreateFormCommand>(cmd =>
                cmd.WebHookSettingsJson != null &&
                cmd.WebHookSettingsJson.Contains("events") &&
                cmd.WebHookSettingsJson.Contains("SubmissionCreated")
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task ExecuteAsync_WithBothFormDefinitionFormats_PrefersNewJsonObject()
    {
        // Arrange
        var newJsonSchema = Schema("""
            {
                "pages": [
                    {
                        "elements": [
                            {
                                "type": "text",
                                "name": "newQuestion"
                            }
                        ]
                    }
                ]
            }
            """);

        var request = new CreateFormRequest
        {
            Name = "Test Form",
            IsEnabled = true,
#pragma warning disable CS0618 // Subject under test: obsolete string field loses to FormDefinitionSchema
            FormDefinitionJsonData = """{ "type": "old" }""",
#pragma warning restore CS0618
            FormDefinitionSchema = newJsonSchema
        };
        var result = Result<Form>.Created(Form.Create(new FormCreateArgs(TenantId: SampleData.TENANT_ID, Name: "Test Form")));

        _mediator.Send(Arg.Any<CreateFormCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<CreateFormCommand>(cmd =>
                cmd.FormDefinitionJsonData.Contains("newQuestion") &&
                !cmd.FormDefinitionJsonData.Contains("old")
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task ExecuteAsync_WithBothWebHookFormats_PrefersNewJsonObject()
    {
        // Arrange
        var newWebhookSettings = Schema("""
            {
                "events": {
                    "SubmissionCreated": {
                        "isEnabled": true
                    }
                }
            }
            """);

        var request = new CreateFormRequest
        {
            Name = "Test Form",
            IsEnabled = true,
            FormDefinitionSchema = Schema("""{ "type": "object" }"""),
#pragma warning disable CS0618 // Subject under test: obsolete string field loses to WebHookSettings
            WebHookSettingsJson = """{ "events": {} }""",
#pragma warning restore CS0618
            WebHookSettings = newWebhookSettings
        };
        var result = Result<Form>.Created(Form.Create(new FormCreateArgs(TenantId: SampleData.TENANT_ID, Name: "Test Form")));

        _mediator.Send(Arg.Any<CreateFormCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<CreateFormCommand>(cmd =>
                cmd.WebHookSettingsJson != null &&
                cmd.WebHookSettingsJson.Contains("SubmissionCreated")
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task ExecuteAsync_WithOnlyOldStringFormat_StillWorks()
    {
        // Arrange - Backward compatibility for the obsolete HTTP string fields
#pragma warning disable CS0618
        var request = new CreateFormRequest
        {
            Name = "Test Form",
            Description = "Test Description",
            IsEnabled = true,
            FormDefinitionJsonData = """{ "type": "object" }""",
            WebHookSettingsJson = """{ "events": {} }"""
        };
        var expectedFormDefinition = request.FormDefinitionJsonData;
        var expectedWebHooks = request.WebHookSettingsJson;
#pragma warning restore CS0618
        var result = Result<Form>.Created(Form.Create(new FormCreateArgs(TenantId: SampleData.TENANT_ID, Name: "Test Form")));

        _mediator.Send(Arg.Any<CreateFormCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<CreateFormCommand>(cmd =>
                cmd.FormDefinitionJsonData == expectedFormDefinition &&
                cmd.WebHookSettingsJson == expectedWebHooks
            ),
            Arg.Any<CancellationToken>()
        );
    }
}
