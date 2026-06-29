-- Replaces blob storage hostnames in JSON columns across multiple tables.
-- Parameters:
-- - @SourceHost: The hostname to be replaced.
-- - @DestinationHost: The new hostname to replace with.
-- - @DryRun: If 1, counts matching rows without updating. Set to 0 to apply changes permanently.
-- Example usage:
-- Dry run:
-- EXEC dbo.ReplaceBlobHostnames
--     @SourceHost = 'https://old-hostname.blob.core.windows.net/',
--     @DestinationHost = 'https://new-hostname.blob.core.windows.net/',
--     @DryRun = 1;
--
-- Apply changes:
-- EXEC dbo.ReplaceBlobHostnames
--     @SourceHost = 'https://old-hostname.blob.core.windows.net/',
--     @DestinationHost = 'https://new-hostname.blob.core.windows.net/',
--     @DryRun = 0;

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

    DECLARE @AffectedRows INT;

    IF @DryRun = 1
    BEGIN
        SELECT @AffectedRows = COUNT(*)
        FROM dbo.FormDefinitions
        WHERE CAST(JsonData AS NVARCHAR(MAX)) LIKE '%' + @SourceHost + '%';

        INSERT INTO @Results
        VALUES ('FormDefinitions', @AffectedRows);

        SELECT @AffectedRows = COUNT(*)
        FROM dbo.Submissions
        WHERE CAST(JsonData AS NVARCHAR(MAX)) LIKE '%' + @SourceHost + '%';

        INSERT INTO @Results
        VALUES ('Submissions', @AffectedRows);

        SELECT @AffectedRows = COUNT(*)
        FROM dbo.SubmissionVersions
        WHERE CAST(JsonData AS NVARCHAR(MAX)) LIKE '%' + @SourceHost + '%';

        INSERT INTO @Results
        VALUES ('SubmissionVersions', @AffectedRows);

        SELECT @AffectedRows = COUNT(*)
        FROM dbo.FormTemplates
        WHERE CAST(JsonData AS NVARCHAR(MAX)) LIKE '%' + @SourceHost + '%';

        INSERT INTO @Results
        VALUES ('FormTemplates', @AffectedRows);
    END
    ELSE
    BEGIN
        BEGIN TRANSACTION;

        BEGIN TRY
            UPDATE dbo.FormDefinitions
            SET JsonData = REPLACE(
                CAST(JsonData AS NVARCHAR(MAX)),
                @SourceHost,
                @DestinationHost
            )
            WHERE CAST(JsonData AS NVARCHAR(MAX)) LIKE '%' + @SourceHost + '%';

            INSERT INTO @Results
            VALUES ('FormDefinitions', @@ROWCOUNT);

            UPDATE dbo.Submissions
            SET JsonData = REPLACE(
                CAST(JsonData AS NVARCHAR(MAX)),
                @SourceHost,
                @DestinationHost
            )
            WHERE CAST(JsonData AS NVARCHAR(MAX)) LIKE '%' + @SourceHost + '%';

            INSERT INTO @Results
            VALUES ('Submissions', @@ROWCOUNT);

            UPDATE dbo.SubmissionVersions
            SET JsonData = REPLACE(
                CAST(JsonData AS NVARCHAR(MAX)),
                @SourceHost,
                @DestinationHost
            )
            WHERE CAST(JsonData AS NVARCHAR(MAX)) LIKE '%' + @SourceHost + '%';

            INSERT INTO @Results
            VALUES ('SubmissionVersions', @@ROWCOUNT);

            UPDATE dbo.FormTemplates
            SET JsonData = REPLACE(
                CAST(JsonData AS NVARCHAR(MAX)),
                @SourceHost,
                @DestinationHost
            )
            WHERE CAST(JsonData AS NVARCHAR(MAX)) LIKE '%' + @SourceHost + '%';

            INSERT INTO @Results
            VALUES ('FormTemplates', @@ROWCOUNT);

            COMMIT TRANSACTION;
        END TRY
        BEGIN CATCH
            IF @@TRANCOUNT > 0
                ROLLBACK TRANSACTION;

            THROW;
        END CATCH
    END

    SELECT
        TableName,
        AffectedRows
    FROM @Results;
END
