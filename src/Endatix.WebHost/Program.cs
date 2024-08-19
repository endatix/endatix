using Endatix.Setup;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

var endatixBuilder = builder.CreateEndatix()
                            .AddDefaultSetup()
                            .AddApiEndpoints();

// TODO: expose custom plugin logic for plugin custom domain event handlers 
// builder.Services.AddMediatRInfrastructure(options =>
//                 {
//                     options.UsePipelineLogging();
//                     options.AdditionalAssemblies =
//                         [
//                             Endatix.Samples.CustomEventHandlers.AssemblyReference.Assembly
//                         ];
//                 });

var app = builder.Build();

app.UseEndatixApi();

app.Run();

