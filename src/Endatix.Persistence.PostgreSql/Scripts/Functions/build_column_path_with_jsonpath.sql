-- =============================================
-- Function: build_column_path_with_jsonpath
-- Description: Builds a column name and JSONPath expression for nested loop questions
--              Handles arbitrary nesting depth using recursive arrays
-- Parameters:
--   @loop_path - Array of loop properties (e.g., ['brands', 'brandProducts'])
--   @property_names - Array of property names (e.g., ['brand', 'brandProduct'])
--   @choice_texts - Array of choice display texts (e.g., ['Puma', 'Shoes'])
--   @choice_values - Array of choice values (e.g., ['Puma', 'Shoes'])
--   @question_name - The actual question name (e.g., 'Rating')
-- Returns: Record with column_name and jsonpath_expression
-- Database: PostgreSQL
-- =============================================

CREATE OR REPLACE FUNCTION build_column_path_with_jsonpath(
    loop_path text[],
    property_names text[],
    choice_texts text[],
    choice_values text[],
    question_name text
)
RETURNS TABLE (
    column_name text,
    jsonpath_expression text
) AS $$
DECLARE
    col_name text := '';
    json_path text := '$';
    i int;
BEGIN
    -- Build column name: "Puma_Shoes_Rating"
    -- Use choice_texts (display names) for column names
    IF array_length(choice_texts, 1) > 0 THEN
        col_name := array_to_string(choice_texts, '_') || '_' || question_name;
    ELSE
        col_name := question_name;
    END IF;

    -- Build JSONPath expression: $.brands[*] ? (@.brand == "Puma").brandProducts[*] ? (@.brandProduct == "Shoes").Rating
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
$$ LANGUAGE plpgsql IMMUTABLE;
