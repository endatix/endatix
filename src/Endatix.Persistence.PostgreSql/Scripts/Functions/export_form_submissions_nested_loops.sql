-- =============================================
-- Function: export_form_submissions_nested_loops
-- Description: Exports form submissions with support for nested dynamic panels (loops).
--              Flattens nested JSON structures into columnar format using JSONPath queries.
--              Generates all possible column combinations (Cartesian product) including empty ones.
--              Handles arbitrary nesting depth and both string/object choice formats.
-- Parameters: @target_form_id - The ID of the form to export (NULL for all forms with submissions)
-- Returns: Table with submission metadata and flattened answers as JSONB
--          Each row contains FlattenedAnswers with columns like "Brand_Product_Question"
-- Database: PostgreSQL
-- =============================================

CREATE OR REPLACE FUNCTION export_form_submissions_nested_loops(target_form_id bigint DEFAULT NULL)
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
    WITH RECURSIVE
    -- Step 1: Get all relevant form definitions
    form_defs AS (
        SELECT DISTINCT
            fd."FormId",
            fd."JsonData"::jsonb AS definition
        FROM "FormDefinitions" fd
        WHERE (target_form_id IS NULL OR fd."FormId" = target_form_id)
          AND EXISTS (SELECT 1 FROM "Submissions" s WHERE s."FormId" = fd."FormId")
    ),

    -- Step 2: Extract ALL elements from all pages (including template elements)
    all_elements AS (
        SELECT
            fd."FormId",
            elem,
            NULL::text AS parent_value_name,
            0 AS depth
        FROM form_defs fd,
             jsonb_array_elements(fd.definition->'pages') AS page,
             jsonb_array_elements(page->'elements') AS elem

        UNION ALL

        -- Recursively get elements from template elements
        SELECT
            ae."FormId",
            template_elem,
            ae.elem->>'valueName' AS parent_value_name,
            ae.depth + 1
        FROM all_elements ae,
             jsonb_array_elements(ae.elem->'templateElements') AS template_elem
        WHERE ae.elem->>'type' = 'paneldynamic'
          AND ae.depth < 10
    ),

    -- Step 3: Identify driving checkboxes (those with valuePropertyName)
    driving_checkboxes AS (
        SELECT DISTINCT
            ae."FormId",
            ae.elem->>'name' AS checkbox_name,
            ae.elem->>'valuePropertyName' AS value_property_name,
            ae.elem->'choices' AS choices
        FROM all_elements ae
        WHERE ae.elem->>'type' IN ('checkbox', 'radiogroup')
          AND ae.elem ? 'valuePropertyName'
    ),

    -- Step 4: Identify dynamic panels and their nesting structure
    dynamic_panels_tree AS (
        SELECT
            ae."FormId",
            ae.elem->>'name' AS panel_name,
            ae.elem->>'valueName' AS value_name,
            ae.parent_value_name,
            ae.depth,
            dc.value_property_name,
            dc.choices,
            ae.elem->'templateElements' AS template_elements
        FROM all_elements ae
        JOIN driving_checkboxes dc
          ON dc."FormId" = ae."FormId"
         AND dc.checkbox_name = ae.elem->>'valueName'
        WHERE ae.elem->>'type' = 'paneldynamic'
    ),

    -- Step 5: Build panel hierarchy paths
    panel_paths AS (
        SELECT
            dpt."FormId",
            dpt.panel_name,
            dpt.value_name,
            ARRAY[dpt.value_name] AS loop_path,
            ARRAY[dpt.value_property_name] AS property_path,
            ARRAY[dpt.choices] AS choices_path,
            dpt.template_elements,
            dpt.depth
        FROM dynamic_panels_tree dpt
        WHERE dpt.parent_value_name IS NULL  -- Top-level panels

        UNION ALL

        -- Add nested panels
        SELECT
            dpt."FormId",
            dpt.panel_name,
            dpt.value_name,
            pp.loop_path || dpt.value_name,
            pp.property_path || dpt.value_property_name,
            pp.choices_path || dpt.choices,
            dpt.template_elements,
            dpt.depth
        FROM panel_paths pp
        JOIN dynamic_panels_tree dpt
          ON dpt."FormId" = pp."FormId"
         AND dpt.parent_value_name = pp.value_name
    ),

    -- Step 6: Extract questions from each panel level
    panel_level_questions AS (
        SELECT
            pp."FormId",
            pp.loop_path,
            pp.property_path,
            pp.choices_path,
            q->>'name' AS question_name,
            q->>'type' AS question_type
        FROM panel_paths pp,
             jsonb_array_elements(pp.template_elements) AS q
        WHERE q->>'type' NOT IN ('paneldynamic', 'html')
          AND q->>'name' IS NOT NULL
          -- Exclude driving checkboxes
          AND NOT EXISTS (
              SELECT 1 FROM driving_checkboxes dc
              WHERE dc."FormId" = pp."FormId"
                AND dc.checkbox_name = q->>'name'
          )
    ),

    -- Step 7: Normalize choices (handle string vs object format)
    normalized_panel_choices AS (
        SELECT
            plq."FormId",
            plq.loop_path,
            plq.property_path,
            plq.question_name,
            generate_subscripts(plq.choices_path, 1) AS level_idx,
            plq.choices_path[generate_subscripts(plq.choices_path, 1)] AS level_choices
        FROM panel_level_questions plq
    ),

    choices_expanded AS (
        SELECT
            npc."FormId",
            npc.loop_path,
            npc.property_path,
            npc.question_name,
            npc.level_idx,
            CASE
                WHEN jsonb_typeof(choice) = 'string' THEN
                    jsonb_build_object('text', choice, 'value', choice)
                WHEN jsonb_typeof(choice) = 'object' THEN
                    jsonb_build_object(
                        'text', COALESCE(choice->>'text', choice->>'value'),
                        'value', choice->>'value'
                    )
            END AS choice_obj
        FROM normalized_panel_choices npc,
             jsonb_array_elements(npc.level_choices) AS choice
    ),

    -- Step 8: Build Cartesian product using recursive CTE
    -- Start with level 1 choices
    cartesian_base AS (
        SELECT
            ce."FormId",
            ce.loop_path,
            ce.property_path,
            ce.question_name,
            ARRAY[ce.choice_obj->>'text'] AS choice_text_path,
            ARRAY[ce.choice_obj->>'value'] AS choice_value_path,
            ce.level_idx AS current_level,
            array_length(ce.loop_path, 1) AS max_levels
        FROM choices_expanded ce
        WHERE ce.level_idx = 1
    ),

    cartesian_recursive AS (
        SELECT * FROM cartesian_base

        UNION ALL

        -- Add next level choices
        SELECT
            ce."FormId",
            ce.loop_path,
            ce.property_path,
            ce.question_name,
            cr.choice_text_path || (ce.choice_obj->>'text'),
            cr.choice_value_path || (ce.choice_obj->>'value'),
            ce.level_idx,
            cr.max_levels
        FROM cartesian_recursive cr
        JOIN choices_expanded ce
          ON ce."FormId" = cr."FormId"
         AND ce.loop_path = cr.loop_path
         AND ce.property_path = cr.property_path
         AND ce.question_name = cr.question_name
         AND ce.level_idx = cr.current_level + 1
    ),

    -- Step 9: Get complete paths (all levels filled)
    complete_paths AS (
        SELECT
            cr."FormId",
            cr.loop_path,
            cr.property_path,
            cr.choice_text_path,
            cr.choice_value_path,
            cr.question_name
        FROM cartesian_recursive cr
        WHERE cr.current_level = cr.max_levels
    ),

    -- Step 10: Build column names and JSONPath expressions
    column_specs AS (
        SELECT
            cp."FormId",
            (build_column_path_with_jsonpath(
                cp.loop_path,
                cp.property_path,
                cp.choice_text_path,
                cp.choice_value_path,
                cp.question_name
            )).*
        FROM complete_paths cp
    ),

    -- Step 11: Get simple (non-panel) questions
    simple_questions AS (
        SELECT DISTINCT
            ae."FormId",
            ae.elem->>'name' AS question_name
        FROM all_elements ae
        WHERE ae.depth = 0  -- Top level only
          AND ae.elem->>'type' NOT IN ('paneldynamic', 'panel', 'html')
          AND ae.elem->>'name' IS NOT NULL
          AND NOT EXISTS (
              SELECT 1 FROM driving_checkboxes dc
              WHERE dc."FormId" = ae."FormId"
                AND dc.checkbox_name = ae.elem->>'name'
          )
    ),

    -- Step 12: Flatten each submission
    flattened_subs AS (
        SELECT
            s."Id",
            s."FormId",
            s."IsComplete",
            s."CompletedAt",
            s."CreatedAt",
            s."ModifiedAt",
            -- Combine simple questions and nested questions
            COALESCE(
                (
                    SELECT jsonb_object_agg(sq.question_name, s."JsonData"::jsonb->sq.question_name)
                    FROM simple_questions sq
                    WHERE sq."FormId" = s."FormId"
                ),
                '{}'::jsonb
            ) ||
            COALESCE(
                (
                    SELECT jsonb_object_agg(
                        cs.column_name,
                        jsonb_path_query_first(
                            s."JsonData"::jsonb,
                            cs.jsonpath_expression::jsonpath
                        )
                    )
                    FROM column_specs cs
                    WHERE cs."FormId" = s."FormId"
                ),
                '{}'::jsonb
            ) AS flat_data
        FROM "Submissions" s
        WHERE (target_form_id IS NULL OR s."FormId" = target_form_id)
    ),

    -- Step 13: Get all possible columns (including empty ones)
    all_columns AS (
        -- Simple questions
        SELECT DISTINCT
            sq."FormId",
            sq.question_name AS col_name
        FROM simple_questions sq

        UNION

        -- All Cartesian product columns
        SELECT DISTINCT
            cs."FormId",
            cs.column_name AS col_name
        FROM column_specs cs
    ),

    -- Step 14: Final result with ALL columns (including empty ones)
    final_data AS (
        SELECT
            fs."FormId",
            fs."Id",
            fs."IsComplete",
            fs."CompletedAt",
            fs."CreatedAt",
            fs."ModifiedAt",
            -- Include all columns, even if they have no data
            (
                SELECT jsonb_object_agg(
                    ac.col_name,
                    COALESCE(fs.flat_data->ac.col_name, 'null'::jsonb)
                )
                FROM all_columns ac
                WHERE ac."FormId" = fs."FormId"
            ) AS "FlattenedAnswers"
        FROM flattened_subs fs
    )

    SELECT
        fd."FormId",
        fd."Id",
        fd."IsComplete",
        fd."CompletedAt",
        fd."CreatedAt",
        fd."ModifiedAt",
        COALESCE(fd."FlattenedAnswers", '{}'::jsonb) AS "FlattenedAnswers"
    FROM final_data fd
    ORDER BY fd."FormId", fd."Id";
END;
$$ LANGUAGE plpgsql;
