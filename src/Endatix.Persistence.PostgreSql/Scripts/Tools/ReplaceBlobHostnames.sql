-- This function replaces hostnames in JSONB columns across multiple tables. It can be used to update blob storage hostnames in the database.
-- Parameters:
-- - source_host: The hostname to be replaced.
-- - destination_host: The new hostname to replace with.
-- - dry_run: If true, the function will perform the updates but will raise an exception at the end to prevent committing the changes. Set to false to apply the changes permanently.
-- Example usage:
--BEGIN;
--
--SELECT *
--FROM public.replace_blob_hostnames(
--    'https://old-hostname.blob.core.windows.net/',
--    'https://new-hostname.blob.core.windows.net/',
--    true
--);
--

ROLLBACK;
CREATE OR REPLACE FUNCTION public.replace_blob_hostnames(
    source_host text,
    destination_host text,
    dry_run boolean DEFAULT true
)
RETURNS TABLE (
    table_name text,
    affected_rows integer
)
LANGUAGE plpgsql
AS $$
DECLARE
    affected_count integer;
BEGIN

    -- FormDefinitions
    UPDATE public."FormDefinitions"
    SET "JsonData" = replace(
        "JsonData"::text,
        source_host,
        destination_host
    )::jsonb
    WHERE "JsonData"::text ILIKE '%' || source_host || '%';

    GET DIAGNOSTICS affected_count = ROW_COUNT;

    table_name := 'FormDefinitions';
    affected_rows := affected_count;
    RETURN NEXT;

    -- Submissions
    UPDATE public."Submissions"
    SET "JsonData" = replace(
        "JsonData"::text,
        source_host,
        destination_host
    )::jsonb
    WHERE "JsonData"::text ILIKE '%' || source_host || '%';

    GET DIAGNOSTICS affected_count = ROW_COUNT;

    table_name := 'Submissions';
    affected_rows := affected_count;
    RETURN NEXT;

    -- SubmissionVersions
    UPDATE public."SubmissionVersions"
    SET "JsonData" = replace(
        "JsonData"::text,
        source_host,
        destination_host
    )::jsonb
    WHERE "JsonData"::text ILIKE '%' || source_host || '%';

    GET DIAGNOSTICS affected_count = ROW_COUNT;

    table_name := 'SubmissionVersions';
    affected_rows := affected_count;
    RETURN NEXT;

    -- FormTemplates
    UPDATE public."FormTemplates"
    SET "JsonData" = replace(
        "JsonData"::text,
        source_host,
        destination_host
    )::jsonb
    WHERE "JsonData"::text ILIKE '%' || source_host || '%';

    GET DIAGNOSTICS affected_count = ROW_COUNT;

    table_name := 'FormTemplates';
    affected_rows := affected_count;
    RETURN NEXT;

    IF dry_run THEN
        RAISE EXCEPTION 'Dry run rollback';
    END IF;

END;
$$;