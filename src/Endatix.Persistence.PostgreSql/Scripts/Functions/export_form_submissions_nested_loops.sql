-- =============================================
-- Function: export_form_submissions_nested_loops
-- Description: Advanced export function that flattens hierarchical form submissions with nested dynamic panels into tabular format.
--              Handles nested loops driven by checkboxes/radiogroups, checkbox explosion (binary columns per choice),
--              simple questions, and calculated values. Uses two-phase approach: calculates column structure once,
--              then streams submissions for optimal performance.
-- Parameters: @target_form_id - The ID of the form to export
-- Returns: Table with submission details and flattened AnswersModel jsonb containing all columns
-- Database: PostgreSQL
-- =============================================

CREATE OR REPLACE FUNCTION export_form_submissions_nested_loops(form_id bigint, after_id bigint DEFAULT NULL, page_size int DEFAULT NULL)
 RETURNS TABLE("FormId" bigint, "Id" bigint, "IsComplete" boolean, "CompletedAt" timestamp with time zone, "CreatedAt" timestamp with time zone, "ModifiedAt" timestamp with time zone, "AnswersModel" jsonb)
 LANGUAGE plpgsql
AS $function$
DECLARE
    submission_cursor CURSOR FOR
        SELECT s."Id", s."FormId", s."IsComplete", s."CompletedAt", s."CreatedAt", s."ModifiedAt", s."JsonData"::jsonb
        FROM "Submissions" s
        WHERE s."FormId" = export_form_submissions_nested_loops.form_id
          AND (export_form_submissions_nested_loops.after_id IS NULL OR s."Id" > export_form_submissions_nested_loops.after_id)
        ORDER BY s."Id"
        LIMIT export_form_submissions_nested_loops.page_size;

    submission_record RECORD;
    v_column_specs jsonb; -- Nested loop columns
    v_simple_questions jsonb; -- Simple columns (text, number, etc.)
    v_exploded_specs jsonb; -- Top-level checkbox/radiogroup columns
    v_all_columns jsonb; -- All column names
    v_flat_data jsonb;
    v_calculated_values jsonb; -- Query string parameters
BEGIN
    -- PHASE 1: Calculate column structure once (one-time work per form)
    WITH RECURSIVE
    -- Step 1: Get all relevant form definitions
    form_defs AS (
        SELECT DISTINCT
            fd."FormId",
            fd."JsonData"::jsonb AS definition
        FROM "FormDefinitions" fd
        WHERE fd."FormId" = export_form_submissions_nested_loops.form_id
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

    -- Step 4-10: Dynamic panel and nested loop column calculations
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
        WHERE dpt.parent_value_name IS NULL

        UNION ALL

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
          AND NOT EXISTS (
              SELECT 1 FROM driving_checkboxes dc
              WHERE dc."FormId" = pp."FormId"
                AND dc.checkbox_name = q->>'name'
          )
    ),

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

    column_specs_nested AS (
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
    -- END Steps (4-10)

    -- Step 11-A: Identify top-level checkbox questions for explosion
    exploded_simple_choices AS (
        SELECT
            ae."FormId",
            ae.elem->>'name' AS question_name,
            CASE
                -- Normalize choice: ensure we get the 'value' property
                WHEN jsonb_typeof(choice) = 'string' THEN jsonb_build_object('value', choice)
                WHEN jsonb_typeof(choice) = 'object' THEN jsonb_build_object('value', COALESCE(choice->>'value', choice->>'text'))
            END AS choice_obj
        FROM all_elements ae,
             jsonb_array_elements(ae.elem->'choices') AS choice
        WHERE ae.depth = 0 -- Top level
          AND ae.elem->>'type' IN ('checkbox')
          AND ae.elem->>'name' IS NOT NULL
          -- Exclude driving checkboxes (those that define a dynamic panel)
          AND NOT EXISTS (
              SELECT 1 FROM driving_checkboxes dc
              WHERE dc."FormId" = ae."FormId"
                AND dc.checkbox_name = ae.elem->>'name'
                AND dc.value_property_name IS NOT NULL -- The check to ensure it drives a panel
          )
    ),

    -- Step 11-B: Build column specs for the simple exploded columns
    exploded_column_specs AS (
        SELECT
            esc."FormId",
            esc.question_name AS source_question_name,
            (esc.question_name || '_' || (esc.choice_obj->>'value')) AS column_name,
            esc.choice_obj->>'value' AS choice_value
        FROM exploded_simple_choices esc
    ),

    -- Step 11: Get simple (non-panel) questions. Exclude exploded types.
    simple_questions AS (
        SELECT DISTINCT
            ae."FormId",
            ae.elem->>'name' AS question_name
        FROM all_elements ae
        WHERE ae.depth = 0
          -- Exclude types handled elsewhere (nested, html, panel, and now: checkbox)
          AND ae.elem->>'type' NOT IN ('paneldynamic', 'panel', 'html', 'checkbox')
          AND ae.elem->>'name' IS NOT NULL
          AND NOT EXISTS (
              SELECT 1 FROM driving_checkboxes dc
              WHERE dc."FormId" = ae."FormId"
                AND dc.checkbox_name = ae.elem->>'name'
          )
    ),

	-- Calculated values that are stored in results
	calculated_values AS (
	    SELECT
	        fd."FormId",
	        cv->>'name' AS calc_name
	    FROM form_defs fd,
	         jsonb_array_elements(fd.definition->'calculatedValues') AS cv
	    WHERE (cv->>'includeIntoResult')::boolean = true
	),

    -- Step 12: Get all possible columns (combined)
    all_columns AS (
        -- Simple columns (text, number, etc.)
        SELECT DISTINCT sq."FormId", sq.question_name AS col_name FROM simple_questions sq
        UNION
        -- Nested loop columns
        SELECT DISTINCT cs."FormId", cs.column_name AS col_name FROM column_specs_nested cs
        UNION
        -- Exploded simple columns
        SELECT DISTINCT ecs."FormId", ecs.column_name AS col_name FROM exploded_column_specs ecs
		UNION
		-- Calculated values
		SELECT DISTINCT cv."FormId", cv.calc_name AS col_name FROM calculated_values cv
    ),

    -- Step 13: Aggregate metadata for reuse
    column_metadata AS (
        SELECT
            export_form_submissions_nested_loops.form_id AS "FormId",
            (
                SELECT jsonb_agg(jsonb_build_object(
                    'column_name', cs.column_name,
                    'jsonpath_expression', cs.jsonpath_expression
                ))
                FROM column_specs_nested cs
                WHERE cs."FormId" = export_form_submissions_nested_loops.form_id
            ) AS column_specs,
            (
                SELECT jsonb_agg(sq.question_name)
                FROM simple_questions sq
                WHERE sq."FormId" = export_form_submissions_nested_loops.form_id
            ) AS simple_questions,
            (
                SELECT jsonb_agg(jsonb_build_object(
                    'column_name', ecs.column_name,
                    'source_question', ecs.source_question_name,
                    'choice_value', ecs.choice_value
                ))
                FROM exploded_column_specs ecs
                WHERE ecs."FormId" = export_form_submissions_nested_loops.form_id
            ) AS exploded_specs,
            (
                SELECT jsonb_agg(ac.col_name)
                FROM all_columns ac
                WHERE ac."FormId" = export_form_submissions_nested_loops.form_id
            ) AS all_columns,
			(
			    SELECT jsonb_agg(cv.calc_name)
			    FROM calculated_values cv
			    WHERE cv."FormId" = export_form_submissions_nested_loops.form_id
			) AS calculated_values
		)

    -- Store column metadata in variables for reuse
    SELECT column_specs, simple_questions, exploded_specs, all_columns, calculated_values
    INTO v_column_specs, v_simple_questions, v_exploded_specs, v_all_columns, v_calculated_values
    FROM column_metadata;

    -- PHASE 2: Stream submissions one by one
    OPEN submission_cursor;

    LOOP
        FETCH submission_cursor INTO submission_record;
        EXIT WHEN NOT FOUND;

        -- Build flattened data for this submission only
        v_flat_data := '{}'::jsonb;

		-- 2.1.a: Add calculated values
		IF v_calculated_values IS NOT NULL THEN
		    SELECT v_flat_data || COALESCE(
		        jsonb_object_agg(
		            calc_name,
		            submission_record."JsonData"->calc_name
		        ),
		        '{}'::jsonb
		    )
		    INTO v_flat_data
		    FROM jsonb_array_elements_text(v_calculated_values) AS calc_name;
		END IF;
        -- 2.1: Add simple questions
        IF v_simple_questions IS NOT NULL THEN
            SELECT v_flat_data || COALESCE(
                jsonb_object_agg(
                    question_name,
                    submission_record."JsonData"->question_name
                ),
                '{}'::jsonb
            )
            INTO v_flat_data
            FROM jsonb_array_elements_text(v_simple_questions) AS question_name;
        END IF;

        -- 2.2: Add exploded simple questions (0 or 1)
        IF v_exploded_specs IS NOT NULL THEN
            SELECT v_flat_data || COALESCE(
                jsonb_object_agg(
                    spec->>'column_name',
                    CASE
                        -- Case 1: Checkbox (answer is an array)
                        WHEN jsonb_typeof(submission_record."JsonData"->(spec->>'source_question')) = 'array'
                             AND submission_record."JsonData"->(spec->>'source_question') @> to_jsonb(spec->>'choice_value')
                             THEN to_jsonb(1) -- Match found, set to 1
                        ELSE to_jsonb(0) -- No match, set to 0
                    END
                ),
                '{}'::jsonb
            )
            INTO v_flat_data
            FROM jsonb_array_elements(v_exploded_specs) AS spec;
        END IF;

        -- 2.3: Add nested loop questions (using JSONPath)
        IF v_column_specs IS NOT NULL THEN
            SELECT v_flat_data || COALESCE(
                jsonb_object_agg(
                    col->>'column_name',
                    jsonb_path_query_first(
                        submission_record."JsonData",
                        (col->>'jsonpath_expression')::jsonpath
                    )
                ),
                '{}'::jsonb
            )
            INTO v_flat_data
            FROM jsonb_array_elements(v_column_specs) AS col;
        END IF;

        -- 2.4: Ensure all columns are present (fill missing with null)
        IF v_all_columns IS NOT NULL THEN
            SELECT jsonb_object_agg(
                col_name,
                COALESCE(v_flat_data->col_name, 'null'::jsonb)
            )
            INTO v_flat_data
            FROM jsonb_array_elements_text(v_all_columns) AS col_name;
        END IF;

        -- Assign to output record and return immediately (stream)
        "FormId" := submission_record."FormId";
        "Id" := submission_record."Id";
        "IsComplete" := submission_record."IsComplete";
        "CompletedAt" := submission_record."CompletedAt";
        "CreatedAt" := submission_record."CreatedAt";
        "ModifiedAt" := submission_record."ModifiedAt";
        "AnswersModel" := COALESCE(v_flat_data, '{}'::jsonb);

        RETURN NEXT;
    END LOOP;

    CLOSE submission_cursor;
END;
$function$
;
