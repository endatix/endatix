-- =============================================
-- Procedure: export_form_submissions (v4)
-- Description: Soft-delete exclusion (IsDeleted = 0) plus scalar+complex
--              answer projection (JSON_QUERY alone drops scalars).
-- Note: Edit in place only while SoftDeleteSubmissions is unmerged; after
--       shipping, further changes require v5+.
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
        StartedAt datetime2,
        SubmitterId bigint,
        SubmitterDisplayId nvarchar(256),
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
            StartedAt,
            SubmitterId,
            SubmitterDisplayId,
            CONVERT(nvarchar(max), JsonData) AS JsonData,
            '{}' AS AnswersJson
        FROM dbo.Submissions
        WHERE FormId = @form_id
          AND IsDeleted = 0
          AND (@after_id IS NULL OR Id > @after_id)
    )
    INSERT INTO #Results
        (FormId, Id, IsComplete, CompletedAt, CreatedAt, ModifiedAt, StartedAt, SubmitterId, SubmitterDisplayId, JsonData, AnswersJson)
    SELECT TOP (ISNULL(@page_size, 2147483647))
        FormId,
        Id,
        IsComplete,
        CompletedAt,
        CreatedAt,
        ModifiedAt,
        StartedAt,
        SubmitterId,
        SubmitterDisplayId,
        JsonData,
        AnswersJson
    FROM BaseSubmissions
    ORDER BY Id ASC;

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
                        CROSS APPLY OPENJSON(JSON_QUERY(CONVERT(nvarchar(max), fd.JsonData), '$.pages')) AS page
                        CROSS APPLY OPENJSON(JSON_QUERY(page.value, '$.elements')) AS elem
                WHERE 
                        fd.FormId = @form_id
                    AND ISJSON(CONVERT(nvarchar(max), fd.JsonData)) = 1
                    AND JSON_QUERY(CONVERT(nvarchar(max), fd.JsonData), '$.pages') IS NOT NULL

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
        -- JSON_QUERY returns NULL for scalars. Project by OPENJSON type so JSON_MODIFY
        -- receives a typed SQL value (string/number/bool) or JSON_QUERY for object/array.
        -- Escape once per context: SQL literals for OPENJSON key / dynamic SQL embedding;
        -- JSON path segment via STRING_ESCAPE so quotes and backslashes are valid.
        DECLARE @escapedName nvarchar(510) = REPLACE(@name, N'''', N'''''');
        DECLARE @jsonPath nvarchar(max) = N'$."' + STRING_ESCAPE(@name, 'json') + N'"';
        DECLARE @escapedJsonPath nvarchar(max) = REPLACE(@jsonPath, N'''', N'''''');
        DECLARE @updateSql nvarchar(max) = N'
                    UPDATE r
                    SET AnswersJson = CASE o.[type]
                        WHEN 1 THEN JSON_MODIFY(r.AnswersJson, N''' + @escapedJsonPath + N''', o.[value])
                        WHEN 2 THEN CASE
                            WHEN TRY_CONVERT(bigint, o.[value]) IS NOT NULL
                                 AND CHARINDEX(N''.'', o.[value]) = 0
                                 AND CHARINDEX(N''e'', o.[value]) = 0
                                 AND CHARINDEX(N''E'', o.[value]) = 0
                                THEN JSON_MODIFY(r.AnswersJson, N''' + @escapedJsonPath + N''', TRY_CONVERT(bigint, o.[value]))
                            ELSE JSON_MODIFY(r.AnswersJson, N''' + @escapedJsonPath + N''', TRY_CONVERT(float(53), o.[value]))
                        END
                        WHEN 3 THEN JSON_MODIFY(
                            r.AnswersJson,
                            N''' + @escapedJsonPath + N''',
                            CONVERT(bit, CASE WHEN o.[value] = N''true'' THEN 1 ELSE 0 END))
                        WHEN 4 THEN JSON_MODIFY(r.AnswersJson, N''' + @escapedJsonPath + N''', JSON_QUERY(o.[value]))
                        WHEN 5 THEN JSON_MODIFY(r.AnswersJson, N''' + @escapedJsonPath + N''', JSON_QUERY(o.[value]))
                        ELSE JSON_MODIFY(r.AnswersJson, N''' + @escapedJsonPath + N''', N'''')
                    END
                    FROM #Results r
                    OUTER APPLY (
                        SELECT j.[type], j.[value]
                        FROM OPENJSON(r.JsonData) AS j
                        WHERE j.[key] = N''' + @escapedName + N'''
                    ) AS o;';

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
        StartedAt,
        SubmitterId,
        SubmitterDisplayId,
        AnswersJson AS AnswersModel
    FROM #Results
    ORDER BY Id ASC;

    -- Clean up
    DROP TABLE #Results;
END;
