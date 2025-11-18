using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Entities;
using Endatix.Api.Endpoints.Forms;
using Endatix.Core.UseCases.Forms.Create;

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

    [Fact]
    public async Task ExecuteAsync_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateFormRequest
        {
            Name = "Test Form",
            IsEnabled = true,
            FormDefinitionJsonData = """{ "type": "object" }"""
        };
        
        var result = Result.Invalid();
        
        _mediator.Send(Arg.Any<CreateFormCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

        // Assert
        var badRequestResult = response.Result as BadRequest;
        badRequestResult.Should().NotBeNull();
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
            FormDefinitionJsonData = """{ "type": "object" }"""
        };
        
        var form = new Form(SampleData.TENANT_ID, request.Name) { Id = 1 };
        var result = Result<Form>.Created(form);

        _mediator.Send(Arg.Any<CreateFormCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        var response = await _endpoint.ExecuteAsync(request, default);

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
        var request = new CreateFormRequest
        {
            Name = "Test Form",
            Description = "Test Description",
            IsEnabled = true,
            FormDefinitionJsonData = """{ "type": "object" }"""
        };
        var result = Result<Form>.Created(new Form(SampleData.TENANT_ID, "Test Form"));
        
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
                cmd.FormDefinitionJsonData == request.FormDefinitionJsonData
            ),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task ExecuteAsync_WithFormDefinitionSchema_ShouldSerializeToJsonString()
    {
        // Arrange
        var jsonSchema = System.Text.Json.JsonDocument.Parse("""
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
            """).RootElement;

        var request = new CreateFormRequest
        {
            Name = "Test Form",
            Description = "Test Description",
            IsEnabled = true,
            FormDefinitionSchema = jsonSchema
        };
        var result = Result<Form>.Created(new Form(SampleData.TENANT_ID, "Test Form"));

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
        var webhookSettings = System.Text.Json.JsonDocument.Parse("""
            {
                "events": {
                    "SubmissionCreated": {
                        "isEnabled": true
                    }
                }
            }
            """).RootElement;

        var request = new CreateFormRequest
        {
            Name = "Test Form",
            Description = "Test Description",
            IsEnabled = true,
            FormDefinitionJsonData = """{ "type": "object" }""",
            WebHookSettings = webhookSettings
        };
        var result = Result<Form>.Created(new Form(SampleData.TENANT_ID, "Test Form"));

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
        var newJsonSchema = System.Text.Json.JsonDocument.Parse("""
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
            """).RootElement;

        var request = new CreateFormRequest
        {
            Name = "Test Form",
            IsEnabled = true,
            FormDefinitionJsonData = """{ "type": "old" }""",  // Old string format
            FormDefinitionSchema = newJsonSchema  // New object format (should win)
        };
        var result = Result<Form>.Created(new Form(SampleData.TENANT_ID, "Test Form"));

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
        var newWebhookSettings = System.Text.Json.JsonDocument.Parse("""
            {
                "events": {
                    "SubmissionCreated": {
                        "isEnabled": true
                    }
                }
            }
            """).RootElement;

        var request = new CreateFormRequest
        {
            Name = "Test Form",
            IsEnabled = true,
            FormDefinitionJsonData = """{ "type": "object" }""",
            WebHookSettingsJson = """{ "events": {} }""",  // Old string format
            WebHookSettings = newWebhookSettings  // New object format (should win)
        };
        var result = Result<Form>.Created(new Form(SampleData.TENANT_ID, "Test Form"));

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
        // Arrange - Backward compatibility test
        var request = new CreateFormRequest
        {
            Name = "Test Form",
            Description = "Test Description",
            IsEnabled = true,
            FormDefinitionJsonData = """{ "type": "object" }""",
            WebHookSettingsJson = """{ "events": {} }"""
        };
        var result = Result<Form>.Created(new Form(SampleData.TENANT_ID, "Test Form"));

        _mediator.Send(Arg.Any<CreateFormCommand>(), Arg.Any<CancellationToken>())
            .Returns(result);

        // Act
        await _endpoint.ExecuteAsync(request, CancellationToken.None);

        // Assert
        await _mediator.Received(1).Send(
            Arg.Is<CreateFormCommand>(cmd =>
                cmd.FormDefinitionJsonData == request.FormDefinitionJsonData &&
                cmd.WebHookSettingsJson == request.WebHookSettingsJson
            ),
            Arg.Any<CancellationToken>()
        );
    }
}
