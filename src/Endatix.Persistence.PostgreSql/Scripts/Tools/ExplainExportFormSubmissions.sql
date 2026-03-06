-- =============================================
-- Tool: EXPLAIN ANALYZE for export_form_submissions_nested_loops
-- Description: Run this in a PostgreSQL client to capture execution plan and timings.
-- Replace :form_id with a real form ID (small, medium, and large form) to baseline performance.
-- Usage: \timing on then run EXPLAIN (ANALYZE, BUFFERS, VERBOSE) SELECT * FROM export_form_submissions_nested_loops(:form_id);
-- =============================================

-- Example: form_id as variable (replace 1 with your form ID for small/medium/large tests)
-- DO $$ BEGIN PERFORM 1; END $$;

EXPLAIN (ANALYZE, BUFFERS, VERBOSE)
SELECT * FROM export_form_submissions_nested_loops({{form_id}});
