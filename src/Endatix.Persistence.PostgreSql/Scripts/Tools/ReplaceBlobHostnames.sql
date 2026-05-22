-- This function replaces hostnames in JSONB columns across multiple tables. It can be used to update blob storage hostnames in the database.
-- Parameters:
-- - source_host: The hostname to be replaced.
-- - destination_host: The new hostname to replace with.
-- - dry_run: If true, counts matching rows without updating. Set to false to apply changes permanently.
-- Example usage:
-- Dry run:
-- SELECT *
-- FROM public.replace_blob_hostnames(
--     'https://old-hostname.blob.core.windows.net/',
--     'https://new-hostname.blob.core.windows.net/',
--     true
-- );
--
-- Apply changes:
-- SELECT *
-- FROM public.replace_blob_hostnames(
--     'https://old-hostname.blob.core.windows.net/',
--     'https://new-hostname.blob.core.windows.net/',
--     false
-- );

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
    IF dry_run THEN
        SELECT COUNT(*)::integer
        INTO affected_count
        FROM public."FormDefinitions"
        WHERE "JsonData"::text ILIKE '%' || source_host || '%';
    ELSE
        UPDATE public."FormDefinitions"
        SET "JsonData" = replace(
            "JsonData"::text,
            source_host,
            destination_host
        )::jsonb
        WHERE "JsonData"::text ILIKE '%' || source_host || '%';

        GET DIAGNOSTICS affected_count = ROW_COUNT;
    END IF;

    table_name := 'FormDefinitions';
    affected_rows := affected_count;
    RETURN NEXT;

    -- Submissions
    IF dry_run THEN
        SELECT COUNT(*)::integer
        INTO affected_count
        FROM public."Submissions"
        WHERE "JsonData"::text ILIKE '%' || source_host || '%';
    ELSE
        UPDATE public."Submissions"
        SET "JsonData" = replace(
            "JsonData"::text,
            source_host,
            destination_host
        )::jsonb
        WHERE "JsonData"::text ILIKE '%' || source_host || '%';

        GET DIAGNOSTICS affected_count = ROW_COUNT;
    END IF;

    table_name := 'Submissions';
    affected_rows := affected_count;
    RETURN NEXT;

    -- SubmissionVersions
    IF dry_run THEN
        SELECT COUNT(*)::integer
        INTO affected_count
        FROM public."SubmissionVersions"
        WHERE "JsonData"::text ILIKE '%' || source_host || '%';
    ELSE
        UPDATE public."SubmissionVersions"
        SET "JsonData" = replace(
            "JsonData"::text,
            source_host,
            destination_host
        )::jsonb
        WHERE "JsonData"::text ILIKE '%' || source_host || '%';

        GET DIAGNOSTICS affected_count = ROW_COUNT;
    END IF;

    table_name := 'SubmissionVersions';
    affected_rows := affected_count;
    RETURN NEXT;

    -- FormTemplates
    IF dry_run THEN
        SELECT COUNT(*)::integer
        INTO affected_count
        FROM public."FormTemplates"
        WHERE "JsonData"::text ILIKE '%' || source_host || '%';
    ELSE
        UPDATE public."FormTemplates"
        SET "JsonData" = replace(
            "JsonData"::text,
            source_host,
            destination_host
        )::jsonb
        WHERE "JsonData"::text ILIKE '%' || source_host || '%';

        GET DIAGNOSTICS affected_count = ROW_COUNT;
    END IF;

    table_name := 'FormTemplates';
    affected_rows := affected_count;
    RETURN NEXT;

END;
$$;
