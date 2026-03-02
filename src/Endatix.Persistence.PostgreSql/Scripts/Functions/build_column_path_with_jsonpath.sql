-- =============================================
-- Function: build_column_path_with_jsonpath
-- Description: Helper function that builds column names and JSONPath expressions for navigating nested loop structures in form data
-- Parameters:
--   @loop_path - Array of nested panel value names defining the hierarchy path
--   @property_names - Array of property names used to filter at each level
--   @choice_texts - Array of display names for choices (used in column names)
--   @choice_values - Array of actual values in JSON for matching
--   @question_name - The name of the question/field being accessed
-- Returns: Table with column_name and jsonpath_expression for data extraction
-- Database: PostgreSQL
-- =============================================

CREATE OR REPLACE FUNCTION public.build_column_path_with_jsonpath(loop_path text[], property_names text[], choice_texts text[], choice_values text[], question_name text)
 RETURNS TABLE(column_name text, jsonpath_expression text)
 LANGUAGE plpgsql
 IMMUTABLE
AS $function$
DECLARE
    col_name text := '';
    json_path text := '$';
    i int;
BEGIN
    -- Build column name
    -- Use choice_texts (display names) for column names
    IF array_length(choice_texts, 1) > 0 THEN
        col_name := array_to_string(choice_texts, '_') || '_' || question_name;
    ELSE
        col_name := question_name;
    END IF;

    -- Build JSONPath expression
    -- Use choice_values (actual values in JSON) for matching
    FOR i IN 1..COALESCE(array_length(loop_path, 1), 0) LOOP
        json_path := json_path || format(
            '.%s[*]',
            loop_path[i]
        );

        -- Add filter if we have a specific choice value to match
        IF i <= COALESCE(array_length(choice_values, 1), 0) THEN
            -- Escape double quotes in the value for JSON string
            json_path := json_path || format(
                ' ? (@.%s == "%s")',
                property_names[i],
                replace(choice_values[i], '"', '\"')
            );
        END IF;
    END LOOP;

    -- Add the final question name
    json_path := json_path || '.' || question_name;

    RETURN QUERY SELECT col_name, json_path;
END;
$function$
;
