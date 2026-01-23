-- ============================================================================
-- PRE-MIGRATION AUDIT SCRIPT
-- ============================================================================
-- This script identifies invalid JSON data that will prevent migration to JSONB
-- Run this BEFORE attempting the migration to see what data needs fixing
-- This script makes NO changes to your database
-- ============================================================================

-- Create temporary function to validate JSON
CREATE OR REPLACE FUNCTION is_valid_json(p_json text)
RETURNS boolean AS $$
BEGIN
    IF p_json IS NULL OR p_json = '' THEN
        RETURN false;
    END IF;
    PERFORM p_json::json;
    RETURN true;
EXCEPTION WHEN OTHERS THEN
    RETURN false;
END;
$$ LANGUAGE plpgsql IMMUTABLE;

-- ============================================================================
-- DETAILED RESULTS - All invalid JSON data across all tables
-- ============================================================================
SELECT
    'FormDefinitions.JsonData' as table_column,
    fd."Id" as record_id,
    fd."FormId" as form_id,
    f."Name" as form_name,
    fd."CreatedAt" as created_at,
    fd."ModifiedAt" as modified_at,
    LENGTH(fd."JsonData") as json_length,
    CASE
        WHEN fd."JsonData" = '' THEN 'EMPTY STRING'
        WHEN TRIM(fd."JsonData") = '' THEN 'WHITESPACE ONLY'
        ELSE 'INVALID JSON'
    END as issue_type,
    LEFT(fd."JsonData", 100) as json_preview
FROM "FormDefinitions" fd
INNER JOIN "Forms" f ON fd."FormId" = f."Id"
WHERE NOT is_valid_json(fd."JsonData")

UNION ALL

SELECT
    'Submissions.JsonData' as table_column,
    s."Id" as record_id,
    s."FormId" as form_id,
    f."Name" as form_name,
    s."CreatedAt" as created_at,
    s."ModifiedAt" as modified_at,
    LENGTH(s."JsonData") as json_length,
    CASE
        WHEN s."JsonData" = '' THEN 'EMPTY STRING'
        WHEN TRIM(s."JsonData") = '' THEN 'WHITESPACE ONLY'
        ELSE 'INVALID JSON'
    END as issue_type,
    LEFT(s."JsonData", 100) as json_preview
FROM "Submissions" s
INNER JOIN "Forms" f ON s."FormId" = f."Id"
WHERE NOT is_valid_json(s."JsonData")

UNION ALL

SELECT
    'Submissions.Metadata' as table_column,
    s."Id" as record_id,
    s."FormId" as form_id,
    f."Name" as form_name,
    s."CreatedAt" as created_at,
    s."ModifiedAt" as modified_at,
    LENGTH(s."Metadata") as json_length,
    CASE
        WHEN s."Metadata" = '' THEN 'EMPTY STRING'
        WHEN TRIM(s."Metadata") = '' THEN 'WHITESPACE ONLY'
        ELSE 'INVALID JSON'
    END as issue_type,
    LEFT(s."Metadata", 100) as json_preview
FROM "Submissions" s
INNER JOIN "Forms" f ON s."FormId" = f."Id"
WHERE s."Metadata" IS NOT NULL
  AND NOT is_valid_json(s."Metadata")

UNION ALL

SELECT
    'SubmissionVersions.JsonData' as table_column,
    sv."Id" as record_id,
    s."FormId" as form_id,
    f."Name" as form_name,
    sv."CreatedAt" as created_at,
    NULL::timestamp as modified_at,
    LENGTH(sv."JsonData") as json_length,
    CASE
        WHEN sv."JsonData" = '' THEN 'EMPTY STRING'
        WHEN TRIM(sv."JsonData") = '' THEN 'WHITESPACE ONLY'
        ELSE 'INVALID JSON'
    END as issue_type,
    LEFT(sv."JsonData", 100) as json_preview
FROM "SubmissionVersions" sv
INNER JOIN "Submissions" s ON sv."SubmissionId" = s."Id"
INNER JOIN "Forms" f ON s."FormId" = f."Id"
WHERE NOT is_valid_json(sv."JsonData")

UNION ALL

SELECT
    'FormTemplates.JsonData' as table_column,
    ft."Id" as record_id,
    NULL::bigint as form_id,
    ft."Name" as form_name,
    ft."CreatedAt" as created_at,
    ft."ModifiedAt" as modified_at,
    LENGTH(ft."JsonData") as json_length,
    CASE
        WHEN ft."JsonData" = '' THEN 'EMPTY STRING'
        WHEN TRIM(ft."JsonData") = '' THEN 'WHITESPACE ONLY'
        ELSE 'INVALID JSON'
    END as issue_type,
    LEFT(ft."JsonData", 100) as json_preview
FROM "FormTemplates" ft
WHERE NOT is_valid_json(ft."JsonData")

UNION ALL

SELECT
    'Themes.JsonData' as table_column,
    t."Id" as record_id,
    NULL::bigint as form_id,
    t."Name" as form_name,
    t."CreatedAt" as created_at,
    t."ModifiedAt" as modified_at,
    LENGTH(t."JsonData") as json_length,
    CASE
        WHEN t."JsonData" = '' THEN 'EMPTY STRING'
        WHEN TRIM(t."JsonData") = '' THEN 'WHITESPACE ONLY'
        ELSE 'INVALID JSON'
    END as issue_type,
    LEFT(t."JsonData", 100) as json_preview
FROM "Themes" t
WHERE NOT is_valid_json(t."JsonData")

UNION ALL

SELECT
    'CustomQuestions.JsonData' as table_column,
    cq."Id" as record_id,
    NULL::bigint as form_id,
    NULL as form_name,
    cq."CreatedAt" as created_at,
    cq."ModifiedAt" as modified_at,
    LENGTH(cq."JsonData") as json_length,
    CASE
        WHEN cq."JsonData" = '' THEN 'EMPTY STRING'
        WHEN TRIM(cq."JsonData") = '' THEN 'WHITESPACE ONLY'
        ELSE 'INVALID JSON'
    END as issue_type,
    LEFT(cq."JsonData", 100) as json_preview
FROM "CustomQuestions" cq
WHERE NOT is_valid_json(cq."JsonData")

UNION ALL

SELECT
    'Forms.WebHookSettingsJson' as table_column,
    f."Id" as record_id,
    NULL::bigint as form_id,
    f."Name" as form_name,
    f."CreatedAt" as created_at,
    f."ModifiedAt" as modified_at,
    LENGTH(f."WebHookSettingsJson") as json_length,
    CASE
        WHEN f."WebHookSettingsJson" = '' THEN 'EMPTY STRING'
        WHEN TRIM(f."WebHookSettingsJson") = '' THEN 'WHITESPACE ONLY'
        ELSE 'INVALID JSON'
    END as issue_type,
    LEFT(f."WebHookSettingsJson", 100) as json_preview
FROM "Forms" f
WHERE f."WebHookSettingsJson" IS NOT NULL
  AND NOT is_valid_json(f."WebHookSettingsJson")

UNION ALL

SELECT
    'TenantSettings.SlackSettingsJson' as table_column,
    ts."TenantId" as record_id,
    NULL::bigint as form_id,
    t."Name" as form_name,
    t."CreatedAt" as created_at,
    t."ModifiedAt" as modified_at,
    LENGTH(ts."SlackSettingsJson") as json_length,
    CASE
        WHEN ts."SlackSettingsJson" = '' THEN 'EMPTY STRING'
        WHEN TRIM(ts."SlackSettingsJson") = '' THEN 'WHITESPACE ONLY'
        ELSE 'INVALID JSON'
    END as issue_type,
    LEFT(ts."SlackSettingsJson", 100) as json_preview
FROM "TenantSettings" ts
INNER JOIN "Tenants" t ON ts."TenantId" = t."Id"
WHERE ts."SlackSettingsJson" IS NOT NULL
  AND NOT is_valid_json(ts."SlackSettingsJson")

UNION ALL

SELECT
    'TenantSettings.WebHookSettingsJson' as table_column,
    ts."TenantId" as record_id,
    NULL::bigint as form_id,
    t."Name" as form_name,
    t."CreatedAt" as created_at,
    t."ModifiedAt" as modified_at,
    LENGTH(ts."WebHookSettingsJson") as json_length,
    CASE
        WHEN ts."WebHookSettingsJson" = '' THEN 'EMPTY STRING'
        WHEN TRIM(ts."WebHookSettingsJson") = '' THEN 'WHITESPACE ONLY'
        ELSE 'INVALID JSON'
    END as issue_type,
    LEFT(ts."WebHookSettingsJson", 100) as json_preview
FROM "TenantSettings" ts
INNER JOIN "Tenants" t ON ts."TenantId" = t."Id"
WHERE ts."WebHookSettingsJson" IS NOT NULL
  AND NOT is_valid_json(ts."WebHookSettingsJson")

UNION ALL

SELECT
    'TenantSettings.CustomExportsJson' as table_column,
    ts."TenantId" as record_id,
    NULL::bigint as form_id,
    t."Name" as form_name,
    t."CreatedAt" as created_at,
    t."ModifiedAt" as modified_at,
    LENGTH(ts."CustomExportsJson") as json_length,
    CASE
        WHEN ts."CustomExportsJson" = '' THEN 'EMPTY STRING'
        WHEN TRIM(ts."CustomExportsJson") = '' THEN 'WHITESPACE ONLY'
        ELSE 'INVALID JSON'
    END as issue_type,
    LEFT(ts."CustomExportsJson", 100) as json_preview
FROM "TenantSettings" ts
INNER JOIN "Tenants" t ON ts."TenantId" = t."Id"
WHERE ts."CustomExportsJson" IS NOT NULL
  AND NOT is_valid_json(ts."CustomExportsJson")

ORDER BY table_column, created_at DESC;

-- ============================================================================
-- SUMMARY COUNTS - Total issues per table
-- ============================================================================
SELECT 'FormDefinitions.JsonData' as table_column, COUNT(*) as total_issues
FROM "FormDefinitions" WHERE NOT is_valid_json("JsonData")

UNION ALL

SELECT 'Submissions.JsonData' as table_column, COUNT(*) as total_issues
FROM "Submissions" WHERE NOT is_valid_json("JsonData")

UNION ALL

SELECT 'Submissions.Metadata' as table_column, COUNT(*) as total_issues
FROM "Submissions" WHERE "Metadata" IS NOT NULL AND NOT is_valid_json("Metadata")

UNION ALL

SELECT 'SubmissionVersions.JsonData' as table_column, COUNT(*) as total_issues
FROM "SubmissionVersions" WHERE NOT is_valid_json("JsonData")

UNION ALL

SELECT 'FormTemplates.JsonData' as table_column, COUNT(*) as total_issues
FROM "FormTemplates" WHERE NOT is_valid_json("JsonData")

UNION ALL

SELECT 'Themes.JsonData' as table_column, COUNT(*) as total_issues
FROM "Themes" WHERE NOT is_valid_json("JsonData")

UNION ALL

SELECT 'CustomQuestions.JsonData' as table_column, COUNT(*) as total_issues
FROM "CustomQuestions" WHERE NOT is_valid_json("JsonData")

UNION ALL

SELECT 'Forms.WebHookSettingsJson' as table_column, COUNT(*) as total_issues
FROM "Forms" WHERE "WebHookSettingsJson" IS NOT NULL AND NOT is_valid_json("WebHookSettingsJson")

UNION ALL

SELECT 'TenantSettings.SlackSettingsJson' as table_column, COUNT(*) as total_issues
FROM "TenantSettings" WHERE "SlackSettingsJson" IS NOT NULL AND NOT is_valid_json("SlackSettingsJson")

UNION ALL

SELECT 'TenantSettings.WebHookSettingsJson' as table_column, COUNT(*) as total_issues
FROM "TenantSettings" WHERE "WebHookSettingsJson" IS NOT NULL AND NOT is_valid_json("WebHookSettingsJson")

UNION ALL

SELECT 'TenantSettings.CustomExportsJson' as table_column, COUNT(*) as total_issues
FROM "TenantSettings" WHERE "CustomExportsJson" IS NOT NULL AND NOT is_valid_json("CustomExportsJson")

ORDER BY table_column;

-- Cleanup
DROP FUNCTION is_valid_json(text);

-- ============================================================================
-- END OF AUDIT
-- ============================================================================
-- If this script shows any issues:
-- 1. Review the problematic data above
-- 2. If you don't need this data, run the PreMigrationFix.sql script
-- 3. Then proceed with the migration
-- ============================================================================
