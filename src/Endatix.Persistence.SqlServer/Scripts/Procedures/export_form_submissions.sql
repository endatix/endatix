-- =============================================
-- Procedure: export_form_submissions
-- Description: Exports form submissions with answers structured as a JSON model
-- Parameters: @form_id - The ID of the form to export
--             @after_id - Optional cursor (return rows with Id > after_id)
--             @page_size - Optional limit (NULL = all)
-- Returns: Dataset with submission details and structured answers
-- Database: SQL Server
-- =============================================

CREATE OR ALTER PROCEDURE dbo.export_form_submissions
    @form_id bigint,
    @after_id bigint = NULL,
    @page_size int = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Step 1: Create temporary tables for working with the data
    CREATE TABLE #Results
    (
        FormId bigint,
        Id bigint,
        IsComplete bit,
        CompletedAt datetime2,
        CreatedAt datetime2,
        ModifiedAt datetime2,
        JsonData nvarchar(max), -- Store JsonData locally to avoid repeated lookups
        AnswersJson nvarchar(max)
    );

    -- Step 2: Get submissions (with paging)
    ;WITH BaseSubmissions AS
    (
        SELECT
            FormId,
            Id,
            IsComplete,
            CompletedAt,
            CreatedAt,
            ModifiedAt,
            JsonData,
            '{}' AS AnswersJson
        FROM dbo.Submissions
        WHERE FormId = @form_id
          AND (@after_id IS NULL OR Id > @after_id)
    )
    INSERT INTO #Results
        (FormId, Id, IsComplete, CompletedAt, CreatedAt, ModifiedAt, JsonData, AnswersJson)
    SELECT TOP (ISNULL(@page_size, 2147483647))
        FormId,
        Id,
        IsComplete,
        CompletedAt,
        CreatedAt,
        ModifiedAt,
        JsonData,
        AnswersJson
    FROM BaseSubmissions
    ORDER BY Id;

    -- Step 3: Find all question names
    DECLARE @QuestionNames TABLE (name nvarchar(255));

    ;WITH
        element_tree
        AS
        (
            -- Base case
                            SELECT
                    JSON_QUERY(elem.value, '$') AS element
                FROM
                    dbo.FormDefinitions fd
                        CROSS APPLY OPENJSON(JSON_QUERY(fd.JsonData, '$.pages')) AS page
                        CROSS APPLY OPENJSON(JSON_QUERY(page.value, '$.elements')) AS elem
                WHERE 
                        fd.FormId = @form_id
                    AND ISJSON(fd.JsonData) = 1
                    AND JSON_QUERY(fd.JsonData, '$.pages') IS NOT NULL

            UNION ALL

                -- Recursive case
                SELECT
                    JSON_QUERY(nested_elem.value, '$') AS element
                FROM
                    element_tree et
                        CROSS APPLY OPENJSON(JSON_QUERY(et.element, '$.elements')) AS nested_elem
                WHERE 
                        JSON_VALUE(et.element, '$.type') = 'panel'
        )
    INSERT INTO @QuestionNames
    SELECT DISTINCT
        JSON_VALUE(element, '$.name') AS name
    FROM
        element_tree
    WHERE 
                    JSON_VALUE(element, '$.type') <> 'panel'
        AND JSON_VALUE(element, '$.name') IS NOT NULL;

    -- Step 4: Update each result row with question values
    DECLARE @name nvarchar(255);

    DECLARE question_cursor CURSOR FOR 
                SELECT name
    FROM @QuestionNames;

    OPEN question_cursor;
    FETCH NEXT FROM question_cursor INTO @name;

    WHILE @@FETCH_STATUS = 0
                BEGIN
        -- Preserve JSON structure: JSON_QUERY returns array/object as JSON fragment (OPENJSON value would stringify).
        DECLARE @path nvarchar(512) = N'$."' + REPLACE(@name, N'''', N'''''') + N'"';
        DECLARE @updateSql nvarchar(max) = N'
                    UPDATE r
                    SET AnswersJson = JSON_MODIFY(
                        AnswersJson, 
                        ''$."' + REPLACE(@name, N'''', N'''''') + N'"'',
                        ISNULL(JSON_QUERY(r.JsonData, N''' + REPLACE(@path, N'''', N'''''') + N'''), ''""'')
                    )
                    FROM #Results r;';

        EXEC sp_executesql @updateSql;

        FETCH NEXT FROM question_cursor INTO @name;
    END;

    CLOSE question_cursor;
    DEALLOCATE question_cursor;

    -- Return the results
    SELECT
        FormId,
        Id,
        IsComplete,
        CompletedAt,
        CreatedAt,
        ModifiedAt,
        AnswersJson AS AnswersModel
    FROM #Results
    ORDER BY Id;

    -- Clean up
    DROP TABLE #Results;
END;
