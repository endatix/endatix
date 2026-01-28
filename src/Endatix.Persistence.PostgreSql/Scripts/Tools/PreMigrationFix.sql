-- ============================================================================
-- PRE-MIGRATION FIX SCRIPT
-- ============================================================================
-- This script fixes invalid JSON data by setting it to empty JSON object {}
-- Run this AFTER reviewing the PreMigrationAudit.sql results
-- This script runs in a transaction - you must COMMIT or ROLLBACK at the end
-- ============================================================================

BEGIN;

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
-- Fix FormDefinitions.JsonData
-- ============================================================================
DO $$
DECLARE
    affected_count int;
BEGIN
    SELECT COUNT(*) INTO affected_count
    FROM "FormDefinitions"
    WHERE NOT is_valid_json("JsonData");

    RAISE NOTICE '============================================================================';
    RAISE NOTICE 'FormDefinitions.JsonData - Records to fix: %', affected_count;
    RAISE NOTICE '============================================================================';
END $$;

UPDATE "FormDefinitions"
SET "JsonData" = '{}'
WHERE NOT is_valid_json("JsonData");

-- ============================================================================
-- Fix Submissions.JsonData
-- ============================================================================
DO $$
DECLARE
    affected_count int;
BEGIN
    SELECT COUNT(*) INTO affected_count
    FROM "Submissions"
    WHERE NOT is_valid_json("JsonData");

    RAISE NOTICE '============================================================================';
    RAISE NOTICE 'Submissions.JsonData - Records to fix: %', affected_count;
    RAISE NOTICE '============================================================================';
END $$;

UPDATE "Submissions"
SET "JsonData" = '{}'
WHERE NOT is_valid_json("JsonData");

-- ============================================================================
-- Fix Submissions.Metadata
-- ============================================================================
DO $$
DECLARE
    affected_count int;
BEGIN
    SELECT COUNT(*) INTO affected_count
    FROM "Submissions"
    WHERE "Metadata" IS NOT NULL
      AND NOT is_valid_json("Metadata");

    RAISE NOTICE '============================================================================';
    RAISE NOTICE 'Submissions.Metadata - Records to fix: %', affected_count;
    RAISE NOTICE '============================================================================';
END $$;

UPDATE "Submissions"
SET "Metadata" = '{}'
WHERE "Metadata" IS NOT NULL
  AND NOT is_valid_json("Metadata");

-- ============================================================================
-- Fix SubmissionVersions.JsonData
-- ============================================================================
DO $$
DECLARE
    affected_count int;
BEGIN
    SELECT COUNT(*) INTO affected_count
    FROM "SubmissionVersions"
    WHERE NOT is_valid_json("JsonData");

    RAISE NOTICE '============================================================================';
    RAISE NOTICE 'SubmissionVersions.JsonData - Records to fix: %', affected_count;
    RAISE NOTICE '============================================================================';
END $$;

UPDATE "SubmissionVersions"
SET "JsonData" = '{}'
WHERE NOT is_valid_json("JsonData");

-- ============================================================================
-- Fix FormTemplates.JsonData
-- ============================================================================
DO $$
DECLARE
    affected_count int;
BEGIN
    SELECT COUNT(*) INTO affected_count
    FROM "FormTemplates"
    WHERE NOT is_valid_json("JsonData");

    RAISE NOTICE '============================================================================';
    RAISE NOTICE 'FormTemplates.JsonData - Records to fix: %', affected_count;
    RAISE NOTICE '============================================================================';
END $$;

UPDATE "FormTemplates"
SET "JsonData" = '{}'
WHERE NOT is_valid_json("JsonData");

-- ============================================================================
-- Fix Themes.JsonData
-- ============================================================================
DO $$
DECLARE
    affected_count int;
BEGIN
    SELECT COUNT(*) INTO affected_count
    FROM "Themes"
    WHERE NOT is_valid_json("JsonData");

    RAISE NOTICE '============================================================================';
    RAISE NOTICE 'Themes.JsonData - Records to fix: %', affected_count;
    RAISE NOTICE '============================================================================';
END $$;

UPDATE "Themes"
SET "JsonData" = '{}'
WHERE NOT is_valid_json("JsonData");

-- ============================================================================
-- Fix CustomQuestions.JsonData
-- ============================================================================
DO $$
DECLARE
    affected_count int;
BEGIN
    SELECT COUNT(*) INTO affected_count
    FROM "CustomQuestions"
    WHERE NOT is_valid_json("JsonData");

    RAISE NOTICE '============================================================================';
    RAISE NOTICE 'CustomQuestions.JsonData - Records to fix: %', affected_count;
    RAISE NOTICE '============================================================================';
END $$;

UPDATE "CustomQuestions"
SET "JsonData" = '{}'
WHERE NOT is_valid_json("JsonData");

-- ============================================================================
-- Fix Forms.WebHookSettingsJson
-- ============================================================================
DO $$
DECLARE
    affected_count int;
BEGIN
    SELECT COUNT(*) INTO affected_count
    FROM "Forms"
    WHERE "WebHookSettingsJson" IS NOT NULL
      AND NOT is_valid_json("WebHookSettingsJson");

    RAISE NOTICE '============================================================================';
    RAISE NOTICE 'Forms.WebHookSettingsJson - Records to fix: %', affected_count;
    RAISE NOTICE '============================================================================';
END $$;

UPDATE "Forms"
SET "WebHookSettingsJson" = '{}'
WHERE "WebHookSettingsJson" IS NOT NULL
  AND NOT is_valid_json("WebHookSettingsJson");

-- ============================================================================
-- Fix TenantSettings.SlackSettingsJson
-- ============================================================================
DO $$
DECLARE
    affected_count int;
BEGIN
    SELECT COUNT(*) INTO affected_count
    FROM "TenantSettings"
    WHERE "SlackSettingsJson" IS NOT NULL
      AND NOT is_valid_json("SlackSettingsJson");

    RAISE NOTICE '============================================================================';
    RAISE NOTICE 'TenantSettings.SlackSettingsJson - Records to fix: %', affected_count;
    RAISE NOTICE '============================================================================';
END $$;

UPDATE "TenantSettings"
SET "SlackSettingsJson" = '{}'
WHERE "SlackSettingsJson" IS NOT NULL
  AND NOT is_valid_json("SlackSettingsJson");

-- ============================================================================
-- Fix TenantSettings.WebHookSettingsJson
-- ============================================================================
DO $$
DECLARE
    affected_count int;
BEGIN
    SELECT COUNT(*) INTO affected_count
    FROM "TenantSettings"
    WHERE "WebHookSettingsJson" IS NOT NULL
      AND NOT is_valid_json("WebHookSettingsJson");

    RAISE NOTICE '============================================================================';
    RAISE NOTICE 'TenantSettings.WebHookSettingsJson - Records to fix: %', affected_count;
    RAISE NOTICE '============================================================================';
END $$;

UPDATE "TenantSettings"
SET "WebHookSettingsJson" = '{}'
WHERE "WebHookSettingsJson" IS NOT NULL
  AND NOT is_valid_json("WebHookSettingsJson");

-- ============================================================================
-- Fix TenantSettings.CustomExportsJson
-- ============================================================================
DO $$
DECLARE
    affected_count int;
BEGIN
    SELECT COUNT(*) INTO affected_count
    FROM "TenantSettings"
    WHERE "CustomExportsJson" IS NOT NULL
      AND NOT is_valid_json("CustomExportsJson");

    RAISE NOTICE '============================================================================';
    RAISE NOTICE 'TenantSettings.CustomExportsJson - Records to fix: %', affected_count;
    RAISE NOTICE '============================================================================';
END $$;

UPDATE "TenantSettings"
SET "CustomExportsJson" = '{}'
WHERE "CustomExportsJson" IS NOT NULL
  AND NOT is_valid_json("CustomExportsJson");

-- Cleanup function
DROP FUNCTION is_valid_json(text);

-- ============================================================================
-- TRANSACTION CONTROL
-- ============================================================================
-- Review the output above showing how many records were updated
--
-- After reviewing the output, execute ONE of the following commands
-- in this same query window (type it and execute it - do not edit this file):
--
--   COMMIT;      -- To apply the changes permanently
--   ROLLBACK;    -- To undo all changes
--
-- IMPORTANT: The transaction will stay open until you run COMMIT or ROLLBACK
-- Do not close this query window until you have committed or rolled back!
-- ============================================================================

-- Example commands (type one of these and execute):
-- COMMIT;
-- ROLLBACK;
