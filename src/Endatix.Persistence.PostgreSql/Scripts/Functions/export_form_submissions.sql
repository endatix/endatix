-- =============================================
-- Function: export_form_submissions
-- Description: Exports form submissions with answers structured as a JSON model
-- Parameters: @form_id - The ID of the form to export
-- Returns: Table with submission details and structured answers
-- Database: PostgreSQL
-- =============================================

CREATE OR REPLACE FUNCTION export_form_submissions(form_id bigint)
RETURNS TABLE (
    "FormId" bigint,
    "Id" bigint,
    "IsComplete" boolean,
    "CompletedAt" timestamptz,
    "CreatedAt" timestamptz,
    "ModifiedAt" timestamptz,
    "AnswersModel" jsonb
) AS $$
BEGIN
    RETURN QUERY
    -- Recursive CTE to extract all form elements including nested panels
    WITH RECURSIVE element_tree AS (
        -- Base case: Get top-level elements from form pages
        SELECT elem AS element
        FROM "FormDefinitions" fd,
             jsonb_array_elements(fd."JsonData"::jsonb -> 'pages') AS page,
             jsonb_array_elements(page->'elements') AS elem
        WHERE fd."FormId" = form_id
          AND fd."JsonData"::jsonb ? 'pages'

        UNION ALL

        -- Recursive case: Get elements from nested panels
        SELECT jsonb_array_elements(element -> 'elements') AS element
        FROM element_tree
        WHERE (element->>'type') = 'panel'
    ),
    -- Extract unique question names from all elements (excluding panels)
    question_names AS (
        SELECT DISTINCT element->>'name' AS name
        FROM element_tree
        WHERE (element->>'type') IS DISTINCT FROM 'panel'
          AND element ? 'name'
    ),
    -- Combine submission data with answers for each question
    submission_fields AS (
        SELECT
            s."Id",
            s."FormId",
            s."IsComplete",
            s."CompletedAt",
            s."CreatedAt",
            s."ModifiedAt",
            -- Create JSON object with question names as keys and answers as values
            jsonb_object_agg(q.name, COALESCE(s."JsonData"::jsonb ->>q.name, '')) AS AnswersModel
        FROM "Submissions" s
        CROSS JOIN question_names q
        WHERE s."FormId" = form_id
        GROUP BY s."Id", s."FormId", s."IsComplete", s."CompletedAt", s."CreatedAt", s."ModifiedAt"
    )
    -- Final selection of all submission fields
    SELECT
        sf."FormId",
        sf."Id",
        sf."IsComplete",
        sf."CompletedAt",
        sf."CreatedAt",
        sf."ModifiedAt",
        sf.AnswersModel
    FROM submission_fields sf;
END;
$$ LANGUAGE plpgsql;