-- This function replaces hostnames in JSONB columns across multiple tables. It can be used to update blob storage hostnames in the database.
-- Parameters:
-- - source_host: The hostname to be replaced.
-- - destination_host: The new hostname to replace with.
-- - dry_run: If true, the function will perform the updates but will raise an exception at the end to prevent committing the changes. Set to false to apply the changes permanently.
-- Example usage:
-- EXEC dbo.ReplaceBlobHostnames
--    @SourceHost = 'https://old-hostname.blob.core.windows.net/',
--    @DestinationHost = 'https://new-hostname.blob.core.windows.net/',
--    @DryRun = 1;

CREATE OR ALTER PROCEDURE dbo.ReplaceBlobHostnames
(
    @SourceHost NVARCHAR(500),
    @DestinationHost NVARCHAR(500),
    @DryRun BIT = 1
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Results TABLE
    (
        TableName NVARCHAR(200),
        AffectedRows INT
    );

    BEGIN TRANSACTION;

    BEGIN TRY

        -- FormDefinitions
        UPDATE dbo.FormDefinitions
        SET JsonData = REPLACE(
            CAST(JsonData AS NVARCHAR(MAX)),
            @SourceHost,
            @DestinationHost
        )
        WHERE CAST(JsonData AS NVARCHAR(MAX)) LIKE '%' + @SourceHost + '%';

        INSERT INTO @Results
        VALUES ('FormDefinitions', @@ROWCOUNT);

        -- Submissions
        UPDATE dbo.Submissions
        SET JsonData = REPLACE(
            CAST(JsonData AS NVARCHAR(MAX)),
            @SourceHost,
            @DestinationHost
        )
        WHERE CAST(JsonData AS NVARCHAR(MAX)) LIKE '%' + @SourceHost + '%';

        INSERT INTO @Results
        VALUES ('Submissions', @@ROWCOUNT);

        -- SubmissionVersions
        UPDATE dbo.SubmissionVersions
        SET JsonData = REPLACE(
            CAST(JsonData AS NVARCHAR(MAX)),
            @SourceHost,
            @DestinationHost
        )
        WHERE CAST(JsonData AS NVARCHAR(MAX)) LIKE '%' + @SourceHost + '%';

        INSERT INTO @Results
        VALUES ('SubmissionVersions', @@ROWCOUNT);

        -- FormTemplates
        UPDATE dbo.FormTemplates
        SET JsonData = REPLACE(
            CAST(JsonData AS NVARCHAR(MAX)),
            @SourceHost,
            @DestinationHost
        )
        WHERE CAST(JsonData AS NVARCHAR(MAX)) LIKE '%' + @SourceHost + '%';

        INSERT INTO @Results
        VALUES ('FormTemplates', @@ROWCOUNT);

        -- Return structured result
        SELECT
            TableName,
            AffectedRows
        FROM @Results;

        IF @DryRun = 1
        BEGIN
            ROLLBACK TRANSACTION;
        END
        ELSE
        BEGIN
            COMMIT TRANSACTION;
        END

    END TRY
    BEGIN CATCH

        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        THROW;

    END CATCH
END