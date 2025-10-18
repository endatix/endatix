-- =============================================
-- Function: export_form_submissions_nested_loops
-- Description: Simplified and corrected version for nested loops export
-- Parameters: @target_form_id - The ID of the form to export
-- Returns: Table with submission details and flattened answers as JSONB
-- Database: PostgreSQL
-- =============================================

CREATE OR REPLACE FUNCTION export_form_submissions_nested_loops(target_form_id bigint)
RETURNS TABLE (
    "FormId" bigint,
    "Id" bigint,
    "IsComplete" boolean,
    "CompletedAt" timestamptz,
    "CreatedAt" timestamptz,
    "ModifiedAt" timestamptz,
    "FlattenedAnswers" jsonb
) AS $$
BEGIN
    RETURN QUERY
    WITH
    -- Get the form definition
    form_def AS (
        SELECT
            "FormId",
            "JsonData"::jsonb AS definition
        FROM "FormDefinitions"
        WHERE "FormId" = target_form_id
        ORDER BY "CreatedAt" DESC
        LIMIT 1
    ),

    -- Extract all page elements (top level)
    page_elements AS (
        SELECT
            elem AS element,
            elem->>'name' AS elem_name,
            elem->>'type' AS elem_type
        FROM form_def,
             jsonb_array_elements(definition->'pages') AS page,
             jsonb_array_elements(page->'elements') AS elem
    ),

    -- Find driving checkboxes (those that have valuePropertyName)
    driving_checkboxes AS (
        SELECT
            elem_name,
            element->>'valuePropertyName' AS value_prop,
            element->'choices' AS choices
        FROM page_elements
        WHERE elem_type IN ('checkbox', 'radiogroup')
          AND element ? 'valuePropertyName'
    ),

    -- Find dynamic panels and their configurations
    dynamic_panels AS (
        SELECT
            pe.elem_name AS panel_name,
            pe.element->>'valueName' AS value_name,
            dc.value_prop,
            dc.choices,
            pe.element->'templateElements' AS template_elements
        FROM page_elements pe
        JOIN driving_checkboxes dc ON dc.elem_name = pe.element->>'valueName'
        WHERE pe.elem_type = 'paneldynamic'
    ),

    -- Get questions from within panels (first level only for now)
    panel_questions AS (
        SELECT
            dp.panel_name,
            dp.value_name,
            dp.value_prop,
            dp.choices,
            q->>'name' AS question_name,
            q->>'type' AS question_type
        FROM dynamic_panels dp,
             jsonb_array_elements(dp.template_elements) AS q
        WHERE q->>'type' NOT IN ('paneldynamic', 'html', 'panel')
          AND q->>'name' IS NOT NULL
          AND q->>'name' != dp.value_name  -- Exclude the driving checkbox itself
    ),

    -- Expand choices into individual rows
    panel_choices AS (
        SELECT
            pq.value_name,
            pq.value_prop,
            pq.question_name,
            CASE
                WHEN jsonb_typeof(choice) = 'string' THEN choice#>>'{}'
                WHEN jsonb_typeof(choice) = 'object' THEN COALESCE(choice->>'text', choice->>'value')
            END AS choice_text,
            CASE
                WHEN jsonb_typeof(choice) = 'string' THEN choice#>>'{}'
                WHEN jsonb_typeof(choice) = 'object' THEN choice->>'value'
            END AS choice_value
        FROM panel_questions pq,
             jsonb_array_elements(pq.choices) AS choice
    ),

    -- Build column paths for each choice + question combination
    column_paths AS (
        SELECT
            pc.choice_text || '_' || pc.question_name AS column_name,
            pc.value_name,
            pc.value_prop,
            pc.choice_value,
            pc.question_name,
            -- Build JSONPath: $.brands[*] ? (@.brand == "Puma").Rating
            '$.' || pc.value_name || '[*] ? (@.' || pc.value_prop || ' == "' ||
            replace(pc.choice_value, '"', '\"') || '").' || pc.question_name AS jsonpath_expr
        FROM panel_choices pc
    ),

    -- Get simple questions (not part of any panel)
    simple_questions AS (
        SELECT DISTINCT
            pe.elem_name AS question_name
        FROM page_elements pe
        WHERE pe.elem_type NOT IN ('paneldynamic', 'panel', 'html')
          AND pe.elem_name IS NOT NULL
          -- Exclude driving checkboxes
          AND NOT EXISTS (
              SELECT 1 FROM driving_checkboxes dc
              WHERE dc.elem_name = pe.elem_name
          )
    ),

    -- Flatten submissions
    flattened_submissions AS (
        SELECT
            s."Id",
            s."FormId",
            s."IsComplete",
            s."CompletedAt",
            s."CreatedAt",
            s."ModifiedAt",
            -- Simple questions
            (
                SELECT jsonb_object_agg(sq.question_name, s."JsonData"::jsonb->sq.question_name)
                FROM simple_questions sq
            ) ||
            -- Panel questions using JSONPath
            (
                SELECT jsonb_object_agg(
                    cp.column_name,
                    jsonb_path_query_first(
                        s."JsonData"::jsonb,
                        cp.jsonpath_expr::jsonpath
                    )
                )
                FROM column_paths cp
            ) AS flat_answers
        FROM "Submissions" s
        WHERE s."FormId" = target_form_id
    ),

    -- Filter to columns with data
    columns_with_data AS (
        SELECT DISTINCT
            kv.key AS col_name
        FROM flattened_submissions fs,
             jsonb_each(fs.flat_answers) kv
        WHERE kv.value IS NOT NULL
          AND jsonb_typeof(kv.value) != 'null'
    ),

    -- Final result
    final_result AS (
        SELECT
            fs."FormId",
            fs."Id",
            fs."IsComplete",
            fs."CompletedAt",
            fs."CreatedAt",
            fs."ModifiedAt",
            (
                SELECT jsonb_object_agg(kv.key, kv.value)
                FROM jsonb_each(fs.flat_answers) kv
                WHERE kv.key IN (SELECT col_name FROM columns_with_data)
            ) AS "FlattenedAnswers"
        FROM flattened_submissions fs
    )

    SELECT * FROM final_result
    ORDER BY "Id";
END;
$$ LANGUAGE plpgsql;
