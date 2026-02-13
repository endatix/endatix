# Endatix.Samples.CustomTransformers

This sample demonstrates how to customize submission exports using a custom `IValueTransformer`.

## Example: extract only file URLs from file answers

File upload answers are commonly stored as:

```json
{"name":"file.jpg","type":"image/jpeg","content":"https://storage.example.com/..."}
```

or multiple files:

```json
[{"name":"file.jpg","type":"image/jpeg","content":"https://storage.example.com/..."}]
```

`FileContentExtractorTransformer` converts them to:

- Single file: `"https://storage.example.com/..."`
- Multiple files: `["https://storage.example.com/..."]`

## Wiring it into your host

Register the transformer via `ConfigureEndatixWithDefaults`:

```csharp
using Endatix.Hosting;
using Endatix.Samples.CustomTransformers;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Host.ConfigureEndatixWithDefaults(endatix =>
{
    endatix.Services.AddExportTransformer<FileContentExtractorTransformer>();
});

var app = builder.Build();
app.UseEndatix();
app.Run();
```

### Notes

- Transformers run in registration order

