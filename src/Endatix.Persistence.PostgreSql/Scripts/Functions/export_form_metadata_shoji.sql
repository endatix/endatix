-- =============================================
-- Function: export_form_metadata_shoji
-- Description: Exports JSON shoji document, compatible with Crunch.io. It is used as metadata for the CSV
-- Parameters: @target_form_id - The ID of the form to export (NULL for all forms with submissions)
-- Returns: Singe cell containing JSON
-- Database: PostgreSQL
-- =============================================

CREATE OR REPLACE FUNCTION public.export_form_metadata_shoji(target_form_id bigint)
 RETURNS jsonb
 LANGUAGE sql
AS $function$
WITH RECURSIVE
    -- *** Reusing Column Calculation Logic from export_form_submissions_nested_loops ***
    form_defs AS (
        SELECT DISTINCT
            fd."FormId",
            fd."JsonData"::jsonb AS definition
        FROM "FormDefinitions" fd
        WHERE fd."FormId" = target_form_id
    ),

    -- Identify Pages and assign a default title if missing
    pages AS (
        SELECT
            fd."FormId",
            page,
            -- Use the page title or generate a default based on index
            COALESCE(page->>'title', 'Page ' || (row_number() OVER (PARTITION BY fd."FormId"))::text) AS page_title,
            (page->'elements') AS elements
        FROM form_defs fd,
             jsonb_array_elements(fd.definition->'pages') WITH ORDINALITY AS page_data(page, page_index)
    ),

    all_elements AS (
        SELECT
            p."FormId",
            elem,
            elem->>'type' AS question_type,
            COALESCE(elem->>'title', elem->>'name') AS question_title,
            elem->>'name' AS question_name,
            p.page_title, -- Page title for top-level elements
            NULL::text AS parent_value_name,
            0 AS depth
        FROM pages p,
             jsonb_array_elements(p.elements) AS elem

        UNION ALL

        -- Recursively get elements from template elements
        SELECT
            ae."FormId",
            template_elem,
            template_elem->>'type',
            COALESCE(template_elem->>'title', template_elem->>'name'),
            template_elem->>'name',
            ae.page_title, -- Propagate page title for nested elements
            ae.elem->>'valueName',
            ae.depth + 1
        FROM all_elements ae,
             jsonb_array_elements(ae.elem->'templateElements') AS template_elem
        WHERE ae.question_type = 'paneldynamic'
          AND template_elem->>'type' IS NOT NULL
          AND template_elem->>'name' IS NOT NULL
          AND ae.depth < 10
    ),

    driving_checkboxes AS (
        SELECT DISTINCT
            ae."FormId",
            ae.question_name AS checkbox_name,
            ae.elem->>'valuePropertyName' AS value_property_name,
            ae.elem->'choices' AS choices
        FROM all_elements ae
        WHERE ae.question_type IN ('checkbox', 'radiogroup')
          AND ae.elem ? 'valuePropertyName'
    ),

    -- Panel Path Logic (Steps 4-9) - Condensed
    dynamic_panels_tree AS (
        SELECT
            ae."FormId", ae.question_name AS panel_name, ae.question_name AS value_name, ae.parent_value_name, ae.depth,
            dc.value_property_name, dc.choices, ae.elem->'templateElements' AS template_elements
        FROM all_elements ae
        JOIN driving_checkboxes dc ON dc."FormId" = ae."FormId" AND dc.checkbox_name = ae.question_name
        WHERE ae.question_type = 'paneldynamic'
    ),
    panel_paths AS (
        SELECT
            dpt."FormId", dpt.panel_name, dpt.value_name,
            ARRAY[dpt.value_name] AS loop_path, ARRAY[dpt.value_property_name] AS property_path, ARRAY[dpt.choices] AS choices_path,
            dpt.template_elements, dpt.depth
        FROM dynamic_panels_tree dpt WHERE dpt.parent_value_name IS NULL
        UNION ALL
        SELECT
            dpt."FormId", dpt.panel_name, dpt.value_name,
            pp.loop_path || dpt.value_name, pp.property_path || dpt.value_property_name, pp.choices_path || dpt.choices,
            dpt.template_elements, dpt.depth
        FROM panel_paths pp JOIN dynamic_panels_tree dpt ON dpt."FormId" = pp."FormId" AND dpt.parent_value_name = pp.value_name
    ),
    panel_level_questions AS (
        SELECT
            pp."FormId", pp.loop_path, pp.property_path, pp.choices_path,
            q->>'name' AS question_name, q->>'type' AS question_type, COALESCE(q->>'title', q->>'name') AS question_title
        FROM panel_paths pp,
             jsonb_array_elements(pp.template_elements) AS q
        WHERE q->>'type' NOT IN ('paneldynamic', 'html') AND q->>'name' IS NOT NULL
          AND NOT EXISTS ( SELECT 1 FROM driving_checkboxes dc WHERE dc."FormId" = pp."FormId" AND dc.checkbox_name = q->>'name' )
    ),
    normalized_panel_choices AS (
        SELECT plq."FormId", plq.loop_path, plq.property_path, plq.question_name, plq.question_title,
            generate_subscripts(plq.choices_path, 1) AS level_idx,
            plq.choices_path[generate_subscripts(plq.choices_path, 1)] AS level_choices
        FROM panel_level_questions plq
    ),
    choices_expanded AS (
        SELECT npc."FormId", npc.loop_path, npc.property_path, npc.question_name, npc.question_title, npc.level_idx,
            CASE WHEN jsonb_typeof(choice) = 'string' THEN jsonb_build_object('text', choice, 'value', choice)
                 ELSE jsonb_build_object('text', COALESCE(choice->>'text', choice->>'value'), 'value', choice->>'value')
            END AS choice_obj
        FROM normalized_panel_choices npc, jsonb_array_elements(npc.level_choices) AS choice
    ),
    cartesian_base AS (
        SELECT ce."FormId", ce.loop_path, ce.property_path, ce.question_name, ce.question_title,
            ARRAY[ce.choice_obj->>'text'] AS choice_text_path, ARRAY[ce.choice_obj->>'value'] AS choice_value_path,
            ce.level_idx AS current_level, array_length(ce.loop_path, 1) AS max_levels
        FROM choices_expanded ce WHERE ce.level_idx = 1
    ),
    cartesian_recursive AS (
        SELECT * FROM cartesian_base
        UNION ALL
        SELECT
            ce."FormId", ce.loop_path, ce.property_path, ce.question_name, ce.question_title,
            cr.choice_text_path || (ce.choice_obj->>'text'), cr.choice_value_path || (ce.choice_obj->>'value'),
            ce.level_idx, cr.max_levels
        FROM cartesian_recursive cr
        JOIN choices_expanded ce
          ON ce."FormId" = cr."FormId" AND ce.loop_path = cr.loop_path
          AND ce.question_name = cr.question_name AND ce.level_idx = cr.current_level + 1
    ),
    complete_paths AS (
        SELECT
            cr."FormId", cr.loop_path, cr.property_path, cr.choice_text_path, cr.choice_value_path, cr.question_name, cr.question_title
        FROM cartesian_recursive cr WHERE cr.current_level = cr.max_levels
    ),

    -- Nested Column Specs (Individual Columns)
    nested_column_specs AS (
        SELECT
            cp."FormId",
            -- *** SANITIZED ALIAS: Replace non-alphanumeric/underscore characters with underscore
            regexp_replace((public.build_column_path_with_jsonpath(
                cp.loop_path, cp.property_path, cp.choice_text_path, cp.choice_value_path, cp.question_name
            )).column_name, '[^a-zA-Z0-9_]+', '_', 'g') AS column_name,
            'nested' AS source_type,
            cp.question_name AS original_name,
            cp.question_title AS original_title,
            plq.question_type AS original_type,
            cp.choice_text_path -- Used to build the variable name/description
        FROM complete_paths cp
        JOIN panel_level_questions plq
          ON plq."FormId" = cp."FormId" AND plq.question_name = cp.question_name
          AND plq.loop_path = cp.loop_path
    ),

    -- *** NEW CTE: Filter to a single definitive top-level checkbox element ***
    single_top_level_checkboxes AS (
        SELECT DISTINCT ON (question_name)
            "FormId", elem, question_name, question_title, question_type
        FROM all_elements ae
        WHERE ae.depth = 0
          AND ae.question_type IN ('checkbox')
          AND NOT ae.elem ? 'valuePropertyName'
        ORDER BY question_name, "FormId"
    ),

    -- Exploded Column Specs (Binary Columns from ONLY top-level checkbox)
    exploded_column_specs AS (
        SELECT
            stlc."FormId",
            -- *** SANITIZED ALIAS: Replace non-alphanumeric/underscore characters with underscore
            regexp_replace((stlc.question_name || '_' || (choice_obj->>'value')), '[^a-zA-Z0-9_]+', '_', 'g') AS column_name,
            'exploded' AS source_type,
            stlc.question_name AS original_name,
            stlc.question_title AS original_title,
            stlc.question_type AS original_type,
            choice_obj->>'text' AS choice_text -- For description
        FROM single_top_level_checkboxes stlc, -- JOIN against the pre-filtered unique elements
             jsonb_array_elements(stlc.elem->'choices') AS choice,
             LATERAL (
                 SELECT CASE WHEN jsonb_typeof(choice) = 'string' THEN jsonb_build_object('text', choice, 'value', choice)
                             ELSE jsonb_build_object('text', COALESCE(choice->>'text', choice->>'value'), 'value', choice->>'value')
                        END AS choice_obj
             ) AS normalized_choice
    ),

    -- Simple Column Specs (Text, Numeric, Date, AND NOW radiogroup)
    simple_column_specs AS (
        SELECT
            ae."FormId",
            -- *** SANITIZED ALIAS: Replace non-alphanumeric/underscore characters with underscore
            regexp_replace(ae.question_name, '[^a-zA-Z0-9_]+', '_', 'g') AS column_name,
            'simple' AS source_type,
            ae.question_name AS original_name,
            ae.question_title AS original_title,
            ae.question_type AS original_type,
            NULL::text AS choice_text
        FROM all_elements ae
        WHERE ae.depth = 0
          -- Exclude types handled elsewhere (nested, html, panel, and checkbox)
          AND ae.question_type NOT IN ('paneldynamic', 'panel', 'html', 'checkbox')
          AND ae.question_name IS NOT NULL
    ),

    -- System Column Specs
    system_column_specs AS (
        SELECT
            target_form_id AS "FormId",
            'Id'::text AS column_name, 'system' AS source_type, 'Submission ID' AS original_name, 'Submission ID' AS original_title, 'numeric' AS original_type, NULL::text AS choice_text
        UNION ALL
        SELECT target_form_id, 'FormId', 'system', 'FormId', 'Form ID', 'numeric', NULL
        UNION ALL
        SELECT target_form_id, 'IsComplete', 'system', 'IsComplete', 'Is Complete', 'boolean', NULL
        UNION ALL
        SELECT target_form_id, 'CompletedAt', 'system', 'CompletedAt', 'Completed At', 'datetime', NULL
        UNION ALL
        SELECT target_form_id, 'CreatedAt', 'system', 'CreatedAt', 'Created At', 'datetime', NULL
        UNION ALL
        SELECT target_form_id, 'ModifiedAt', 'system', 'ModifiedAt', 'Modified At', 'datetime', NULL
    ),

    -- All Final Export Columns
    all_export_columns AS (
        SELECT "FormId", column_name, source_type, original_name, original_title, original_type, choice_text FROM system_column_specs
        UNION ALL
        SELECT "FormId", column_name, source_type, original_name, original_title, original_type, choice_text FROM simple_column_specs
        UNION ALL
        SELECT "FormId", column_name, source_type, original_name, original_title, original_type, choice_text FROM exploded_column_specs
        UNION ALL
        SELECT "FormId", column_name, source_type, original_name, original_title, original_type, array_to_string(choice_text_path, ' -> ') AS choice_text FROM nested_column_specs
    ),

    -- Shoji Variable Generation (The main mapping logic)
    shoji_variables_raw AS (
        SELECT
            col.column_name AS alias,
            col.original_title AS name,
            -- Determine the Shoji type based on the original SurveyJS/System type
            CASE
                WHEN col.original_type IN ('numeric', 'rating', 'expression') THEN 'numeric'
                WHEN col.original_type IN ('text', 'comment') THEN 'text'
                WHEN col.original_type IN ('date', 'datetime', 'completedat', 'createdat', 'modifiedat') THEN 'datetime'
                -- Radiogroups and Dropdowns are now simple, hence categorical
                WHEN col.original_type IN ('boolean', 'radiogroup', 'dropdown') THEN 'categorical'
                -- Exploded columns (binary) are treated as numeric (subvariables)
                WHEN col.source_type = 'exploded' THEN 'numeric'
                WHEN col.source_type = 'system' AND col.column_name IN ('Id', 'FormId') THEN 'numeric'
                ELSE 'text' -- Default catch-all
            END AS shoji_type,
            col.source_type,
            col.original_type,
            col.original_name,
            -- Build Description: For nested columns, prepend the path. For exploded, append the choice.
            CASE
                WHEN col.source_type = 'nested' THEN
                     'Nested question response: ' || col.choice_text || ' / ' || col.original_title
                WHEN col.source_type = 'exploded' THEN
                     col.original_title || ' (Choice: ' || col.choice_text || ')'
                ELSE col.original_title
            END AS description
        FROM all_export_columns col
    ),

    -- Build JSON Objects for individual variables
    shoji_json_variables AS (
        SELECT
            raw.alias,
            raw.original_name, -- Keep for joining choices
            jsonb_strip_nulls(jsonb_build_object(
                'name', raw.name,
                'alias', raw.alias,
                'description', raw.description,
                'type', raw.shoji_type,
                -- Categories for boolean/radiogroup/system columns
                'categories', CASE
                    -- For Radiogroups/Dropdowns (now 'simple') we need the choices
                    WHEN raw.original_type IN ('radiogroup', 'dropdown') AND raw.source_type = 'simple' THEN
                         (
                            -- FIX: Isolate the single correct element before expanding choices to prevent duplication.
                            WITH single_element AS (
                                SELECT elem
                                FROM all_elements ae
                                WHERE ae.question_name = raw.original_name
                                  AND ae.depth = 0
                                  AND ae.question_type = raw.original_type
                                LIMIT 1 -- Guarantee we only look at one definition
                            )
                            -- Then expand its choices
                            SELECT jsonb_agg(
                                jsonb_build_object(
                                    'name', COALESCE(choice_obj->>'text', choice_obj->>'value'),
                                    'id', rn,
                                    'numeric_value', rn
                                ) ORDER BY rn
                            )
                            FROM single_element se,
                                 jsonb_array_elements(se.elem->'choices') WITH ORDINALITY AS choice_data(choice, rn),
                                 LATERAL (
                                     SELECT CASE WHEN jsonb_typeof(choice) = 'string' THEN jsonb_build_object('text', choice, 'value', choice)
                                                 ELSE jsonb_build_object('text', COALESCE(choice->>'text', choice->>'value'), 'value', choice->>'value')
                                            END AS choice_obj
                                 ) AS normalized_choice
                         )
                    -- System Boolean
                    WHEN raw.alias = 'IsComplete' THEN
                        jsonb_build_array(
                            jsonb_build_object('name', 'False', 'id', 0, 'numeric_value', 0, 'missing', false),
                            jsonb_build_object('name', 'True', 'id', 1, 'numeric_value', 1, 'missing', false)
                        )
                    ELSE NULL
                END
            )) AS metadata_json
        FROM shoji_variables_raw raw
        -- Only include System, Simple (now including radiogroup), and Nested types.
        WHERE source_type IN ('system', 'simple', 'nested')
    ),

    -- Group top-level checkboxes into 'multiple_response' variables
    shoji_multiple_response_groups AS (
        SELECT
            raw.original_name AS original_name, -- Keep original name for grouping
            (SELECT COALESCE(elem->>'title', elem->>'name') FROM all_elements ae WHERE ae.question_name = raw.original_name AND ae.depth = 0 AND ae.question_type = 'checkbox' LIMIT 1) AS name,
            -- Use the *sanitized column name* as the subvariable alias
            jsonb_agg(jsonb_build_object('name', choice_text, 'alias', raw.column_name) ORDER BY choice_text) AS subvariables
        FROM all_export_columns raw
        WHERE source_type = 'exploded'
          AND original_type = 'checkbox' -- Only checkboxes should form the MR group
        GROUP BY original_name
    ),

    -- Build the JSON Objects for multiple_response groups
    shoji_multiple_response_json AS (
        SELECT
            -- Use the sanitized column name of the source question as the final group alias
            regexp_replace(original_name, '[^a-zA-Z0-9_]+', '_', 'g') AS alias,
            jsonb_strip_nulls(jsonb_build_object(
                'name', name,
                'alias', regexp_replace(original_name, '[^a-zA-Z0-9_]+', '_', 'g'), -- Ensure alias is sanitized here too
                'description', name,
                'type', 'multiple_response',
                'categories', jsonb_build_array(
                    jsonb_build_object('name', 'not selected', 'id', 2, 'numeric_value', NULL, 'missing', false, 'selected', false),
                    jsonb_build_object('name', 'selected', 'id', 1, 'numeric_value', NULL, 'missing', false, 'selected', true)
                ),
                'subvariables', subvariables
            )) AS metadata_json
        FROM shoji_multiple_response_groups
        WHERE name IS NOT NULL -- Only include if the original checkbox element was found
    ),

    -- Combine all variables (simple, nested, system, and multiple_response groups)
    all_shoji_metadata AS (
        SELECT alias, metadata_json FROM shoji_json_variables
        UNION ALL
        -- Add the multiple_response group metadata
        SELECT alias, metadata_json FROM shoji_multiple_response_json
    )

SELECT
    jsonb_build_object(
        'element', 'shoji:entity',
        'body', jsonb_build_object(
            'name', COALESCE((SELECT definition->>'title' FROM form_defs LIMIT 1), 'Survey Export Data'),
            'description', 'Metadata for Form ID ' || target_form_id,
            'table', jsonb_build_object(
                'element', 'crunch:table',
                'metadata', (
                    -- Aggregate all metadata entries into a single object keyed by alias
                    SELECT jsonb_object_agg(alias, metadata_json ORDER BY alias)
                    FROM all_shoji_metadata
                )
                -- REMOVED 'order' section entirely to save space and simplify the output
            )
        )
    )

$$ LANGUAGE plpgsql;