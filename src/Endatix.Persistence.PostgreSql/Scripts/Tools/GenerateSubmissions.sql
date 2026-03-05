-- ============================================================================
-- GENERATE SUBMISSIONS SCRIPT
-- ============================================================================
-- This script performs a bulk insert for public."Submissions" by duplicating
-- an existing submission multiple times with incremental IDs and timestamps.
-- Run this to generate test data for submissions.
-- ============================================================================

-- Set these variables as needed:
-- example: \set submission_id 1478782411327143936
\set submission_id {YOUR_SUBMISSION_ID_HERE}
-- example: \set num_items 1000
\set num_items {YOUR_NUMBER_OF_ITEMS_HERE}

-- CTE to select the base submission data that will be duplicated
WITH base AS (
    SELECT
        "IsComplete", "JsonData", "FormId", "FormDefinitionId", "CurrentPage", "Metadata",
        "CompletedAt", "Token_ExpiresAt", "CreatedAt", "ModifiedAt", "DeletedAt",
        "IsDeleted", "Status", "TenantId", "SubmittedBy"
    FROM public."Submissions"
    WHERE "Id" = :submission_id
)
-- Perform the bulk insert with generated IDs and staggered timestamps
INSERT INTO public."Submissions" (
    "Id", "IsComplete", "JsonData", "FormId", "FormDefinitionId", "CurrentPage", "Metadata",
    "CompletedAt", "Token_ExpiresAt", "CreatedAt", "ModifiedAt", "DeletedAt",
    "IsDeleted", "Status", "TenantId", "SubmittedBy"
)
SELECT
    :submission_id + gs.i AS "Id",
    b."IsComplete",
    b."JsonData",
    b."FormId",
    b."FormDefinitionId",
    b."CurrentPage",
    b."Metadata",
    b."CompletedAt" + (gs.i * INTERVAL '1 minute'),
    b."Token_ExpiresAt" + (gs.i * INTERVAL '1 minute'),
    b."CreatedAt" + (gs.i * INTERVAL '1 minute'),
    b."ModifiedAt" + (gs.i * INTERVAL '1 minute'),
    b."DeletedAt",
    b."IsDeleted",
    b."Status",
    b."TenantId",
    b."SubmittedBy"
FROM base b
CROSS JOIN generate_series(1, :num_items) AS gs(i);
