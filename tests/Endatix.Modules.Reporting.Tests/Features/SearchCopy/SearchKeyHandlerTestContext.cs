using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Features.SearchCopy;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Tests.Features.SearchCopy;

internal sealed class SearchKeyHandlerTestContext
{
    public const long FormId = 100;

    public IRepository<Form> Forms { get; } = Substitute.For<IRepository<Form>>();

    public IFormSchemaRepository Schemas { get; } = Substitute.For<IFormSchemaRepository>();

    public IFlattenedSubmissionRepository Rows { get; } = Substitute.For<IFlattenedSubmissionRepository>();

    public IReportingUnitOfWork UnitOfWork { get; } = Substitute.For<IReportingUnitOfWork>();

    public IRepository<OutboxMessage> OutboxMessages { get; } = Substitute.For<IRepository<OutboxMessage>>();

    public UserSearchKeys UserSearchKeys => new(Forms, Schemas, UnitOfWork);

    public static SearchKeyTarget Target(string key, long tenantId = SampleData.TENANT_ID) => new(tenantId, FormId, key);

    public void GivenForm(long tenantId) =>
        Forms.SingleOrDefaultAsync(Arg.Any<ISingleResultSpecification<Form>>(), Arg.Any<CancellationToken>())
            .Returns(Form.Create(new FormCreateArgs(TenantId: tenantId, Name: "Large form")));

    public FormSchemaEntity GivenSchema(string definition)
    {
        var compiled = new FormSchemaCompiler().CompilePersisted(definition);
        FormSchemaEntity schema = new(SampleData.TENANT_ID, FormId, 1, compiled.FlatteningMapJson, compiled.CodebookJson);
        var keys = FormSearchKeys.Empty.Refresh(
            new SearchKeysCompile(compiled.SearchableColumns, new HashSet<string>()),
            DateTime.UtcNow);
        schema.UpdateSearchKeys(keys.ToJson(), keys.Revision);
        Schemas.LockAndGetByFormIdAsync(SampleData.TENANT_ID, FormId, Arg.Any<CancellationToken>()).Returns(schema);
        return schema;
    }

    public static string TextQuestions(int count)
    {
        var elements = Enumerable.Range(1, count).Select(index => $$"""{ "type": "text", "name": "q{{index:D3}}" }""")
            .Append("""{ "type": "text", "name": "age", "inputType": "number" }""");
        return $$"""{ "pages": [ { "elements": [ {{string.Join(", ", elements)}} ] } ] }""";
    }
}
