using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Endatix.Persistence.SqlServer.Migrations.AppEntities
{
    /// <inheritdoc />
    public partial class SubmissionsExport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
            CREATE OR ALTER PROCEDURE dbo.export_form_submissions
                @form_id bigint
            AS
            BEGIN
                SET NOCOUNT ON;
                
                -- Step 1: Create temporary tables for working with the data
                CREATE TABLE #Results (
                    FormId bigint,
                    Id bigint,
                    IsComplete bit,
                    CompletedAt datetime2,
                    CreatedAt datetime2,
                    ModifiedAt datetime2,
                    AnswersJson nvarchar(max)
                );
                
                -- Step 2: Get all submissions first
                INSERT INTO #Results (FormId, Id, IsComplete, CompletedAt, CreatedAt, ModifiedAt, AnswersJson)
                SELECT 
                    FormId, 
                    Id, 
                    IsComplete,
                    CompletedAt,
                    CreatedAt,
                    ModifiedAt,
                    '{}'  -- Start with empty JSON object
                FROM dbo.Submissions
                WHERE FormId = @form_id;
                
                -- Step 3: Find all question names
                DECLARE @QuestionNames TABLE (name nvarchar(255));
                
                ;WITH element_tree AS (
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
                SELECT name FROM @QuestionNames;
                
                OPEN question_cursor;
                FETCH NEXT FROM question_cursor INTO @name;
                
                WHILE @@FETCH_STATUS = 0
                BEGIN
                    -- For each submission, add this question's value to the JSON
                    DECLARE @updateSql nvarchar(max) = N'
                    UPDATE r
                    SET AnswersJson = JSON_MODIFY(
                        AnswersJson, 
                        ''$.""' + @name + '""'',
                        ISNULL((
                            SELECT value 
                            FROM OPENJSON((SELECT JsonData FROM dbo.Submissions WHERE Id = r.Id)) 
                            WHERE [key] = ''' + @name + '''
                        ), '''')
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
                FROM #Results;
                
                -- Clean up
                DROP TABLE #Results;
            END;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP PROCEDURE IF EXISTS dbo.export_form_submissions;
            ");
        }
    }
}
