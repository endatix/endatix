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
        JsonData nvarchar(max),
        AnswersJson nvarchar(max)
    );

    -- Step 2: Get submissions (with paging) directly into the temp table
    INSERT INTO #Results
        (
        FormId, Id, IsComplete, CompletedAt, CreatedAt,
        ModifiedAt, StartedAt, SubmitterId, SubmitterDisplayId,
        JsonData, AnswersJson
        )
    SELECT TOP (ISNULL(@page_size, 2147483647))
        FormId, Id, IsComplete, CompletedAt, CreatedAt,
        ModifiedAt, StartedAt, SubmitterId, SubmitterDisplayId,
        CONVERT(nvarchar(max), JsonData), '{}'
    FROM dbo.Submissions
    WHERE FormId = @form_id
        AND IsDeleted = 0
        AND (@after_id IS NULL OR Id > @after_id)
    ORDER BY Id ASC;

    -- Step 3: Find all question names from the Form Definition
    DECLARE @QuestionNames TABLE (Name nvarchar(255));

    ;WITH
        element_tree
        AS
        (
            -- Base case: Top level elements
                            SELECT elem.[value] AS element
                FROM dbo.FormDefinitions fd
        CROSS APPLY OPENJSON(CONVERT(nvarchar(max), fd.JsonData), '$.pages') AS page
        CROSS APPLY OPENJSON(page.[value], '$.elements') AS elem
                WHERE fd.FormId = @form_id
                    AND ISJSON(CONVERT(nvarchar(max), fd.JsonData)) = 1

            UNION ALL

                -- Recursive case: Nested elements inside panels
                SELECT nested_elem.[value] AS element
                FROM element_tree et
        CROSS APPLY OPENJSON(et.element, '$.elements') AS nested_elem
                WHERE JSON_VALUE(et.element, '$.type') = 'panel'
        )
    INSERT INTO @QuestionNames
        (Name)
    SELECT DISTINCT JSON_VALUE(element, '$.name')
    FROM element_tree
    WHERE JSON_VALUE(element, '$.type') <> 'panel'
        AND JSON_VALUE(element, '$.name') IS NOT NULL;

    -- Step 4: Parameterized Dynamic SQL Template (DRY)
    -- Instead of escaping strings per iteration, we write the logic once
    -- and pass @questionName and @jsonPath securely via sp_executesql
    DECLARE @updateSql nvarchar(max) = N'
        UPDATE r
        SET AnswersJson = CASE o.[type]
            WHEN 1 THEN JSON_MODIFY(r.AnswersJson, @jsonPath, o.[value])
            WHEN 2 THEN CASE
                WHEN TRY_CONVERT(bigint, o.[value]) IS NOT NULL
                     AND CHARINDEX(N''.'', o.[value]) = 0
                     AND CHARINDEX(N''e'', o.[value]) = 0
                     AND CHARINDEX(N''E'', o.[value]) = 0
                    THEN JSON_MODIFY(r.AnswersJson, @jsonPath, TRY_CONVERT(bigint, o.[value]))
                ELSE JSON_MODIFY(r.AnswersJson, @jsonPath, TRY_CONVERT(float(53), o.[value]))
            END
            WHEN 3 THEN JSON_MODIFY(r.AnswersJson, @jsonPath, CAST(CASE WHEN o.[value] = N''true'' THEN 1 ELSE 0 END AS bit))
            WHEN 4 THEN JSON_MODIFY(r.AnswersJson, @jsonPath, JSON_QUERY(o.[value]))
            WHEN 5 THEN JSON_MODIFY(r.AnswersJson, @jsonPath, JSON_QUERY(o.[value]))
            ELSE JSON_MODIFY(r.AnswersJson, @jsonPath, N'''')
        END
        FROM #Results r
        OUTER APPLY (
            SELECT j.[type], j.[value]
            FROM OPENJSON(r.JsonData) AS j
            WHERE j.[key] = @questionName
        ) AS o;
    ';

    -- Step 5: Update each result row with question values iteratively
    DECLARE @questionName nvarchar(255);
    DECLARE @jsonPath nvarchar(max);

    DECLARE question_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT Name
    FROM @QuestionNames;

    OPEN question_cursor;
    FETCH NEXT FROM question_cursor INTO @questionName;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        -- Safely format the JSON Path for the current question
        SET @jsonPath = N'$."' + STRING_ESCAPE(@questionName, 'json') + N'"';

        EXEC sp_executesql
            @stmt = @updateSql,
            @params = N'@questionName nvarchar(255), @jsonPath nvarchar(max)',
            @questionName = @questionName,
            @jsonPath = @jsonPath;

        FETCH NEXT FROM question_cursor INTO @questionName;
    END;

    CLOSE question_cursor;
    DEALLOCATE question_cursor;

    -- Return the final projected results
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
