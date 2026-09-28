-- Quartz.NET 4.2.2 schema for PostgreSQL (database/tables/tables_postgres.sql, Apache-2.0),
-- with every table placed in the "jobs" schema under the prefix "qrtz_". Index names are unchanged:
-- PostgreSQL creates an index in its table's schema. Applied by the jobs migrations only; the
-- scheduler validates this schema at startup and never creates it.
CREATE TABLE jobs.qrtz_job_details
  (
    sched_name TEXT NOT NULL,
    job_name TEXT NOT NULL,
    job_group TEXT NOT NULL,
    description TEXT NULL,
    job_class_name TEXT NOT NULL,
    is_durable BOOL NOT NULL,
    is_nonconcurrent BOOL NOT NULL,
    is_update_data BOOL NOT NULL,
    requests_recovery BOOL NOT NULL,
    job_data BYTEA NULL,
    PRIMARY KEY (sched_name, job_name, job_group)
);

CREATE TABLE jobs.qrtz_triggers
  (
    sched_name TEXT NOT NULL,
    trigger_name TEXT NOT NULL,
    trigger_group TEXT NOT NULL,
    job_name TEXT NOT NULL,
    job_group TEXT NOT NULL,
    description TEXT NULL,
    next_fire_time BIGINT NULL,
    prev_fire_time BIGINT NULL,
    priority INTEGER NULL,
    trigger_state TEXT NOT NULL,
    trigger_type TEXT NOT NULL,
    start_time BIGINT NOT NULL,
    end_time BIGINT NULL,
    calendar_name TEXT NULL,
    misfire_instr SMALLINT NULL,
    misfire_orig_fire_time BIGINT NULL,
    execution_group VARCHAR(200) NULL,
    preferred_node VARCHAR(200) NULL,
    preferred_node_auto BOOL NOT NULL DEFAULT FALSE,
    retry_policy VARCHAR(250) NULL,
    retry_attempt INTEGER NULL,
    continues_trigger_name TEXT NULL,
    continues_trigger_group TEXT NULL,
    continuation_condition INTEGER NULL,
    job_data BYTEA NULL,
    PRIMARY KEY (sched_name, trigger_name, trigger_group),
    FOREIGN KEY (sched_name, job_name, job_group)
      REFERENCES jobs.qrtz_job_details (sched_name, job_name, job_group)
);

CREATE TABLE jobs.qrtz_simple_triggers
  (
    sched_name TEXT NOT NULL,
    trigger_name TEXT NOT NULL,
    trigger_group TEXT NOT NULL,
    repeat_count BIGINT NOT NULL,
    repeat_interval BIGINT NOT NULL,
    times_triggered BIGINT NOT NULL,
    PRIMARY KEY (sched_name, trigger_name, trigger_group),
    FOREIGN KEY (sched_name, trigger_name, trigger_group)
      REFERENCES jobs.qrtz_triggers (sched_name, trigger_name, trigger_group)
      ON DELETE CASCADE
);

CREATE TABLE jobs.qrtz_simprop_triggers
  (
    sched_name TEXT NOT NULL,
    trigger_name TEXT NOT NULL,
    trigger_group TEXT NOT NULL,
    str_prop_1 TEXT NULL,
    str_prop_2 TEXT NULL,
    str_prop_3 TEXT NULL,
    int_prop_1 INTEGER NULL,
    int_prop_2 INTEGER NULL,
    long_prop_1 BIGINT NULL,
    long_prop_2 BIGINT NULL,
    dec_prop_1 NUMERIC NULL,
    dec_prop_2 NUMERIC NULL,
    bool_prop_1 BOOL NULL,
    bool_prop_2 BOOL NULL,
    time_zone_id TEXT NULL,
    PRIMARY KEY (sched_name, trigger_name, trigger_group),
    FOREIGN KEY (sched_name, trigger_name, trigger_group)
      REFERENCES jobs.qrtz_triggers (sched_name, trigger_name, trigger_group)
      ON DELETE CASCADE
);

CREATE TABLE jobs.qrtz_cron_triggers
  (
    sched_name TEXT NOT NULL,
    trigger_name TEXT NOT NULL,
    trigger_group TEXT NOT NULL,
    cron_expression TEXT NOT NULL,
    time_zone_id TEXT,
    PRIMARY KEY (sched_name, trigger_name, trigger_group),
    FOREIGN KEY (sched_name, trigger_name, trigger_group)
      REFERENCES jobs.qrtz_triggers (sched_name, trigger_name, trigger_group)
      ON DELETE CASCADE
);

CREATE TABLE jobs.qrtz_blob_triggers
  (
    sched_name TEXT NOT NULL,
    trigger_name TEXT NOT NULL,
    trigger_group TEXT NOT NULL,
    blob_data BYTEA NULL,
    PRIMARY KEY (sched_name, trigger_name, trigger_group),
    FOREIGN KEY (sched_name, trigger_name, trigger_group)
      REFERENCES jobs.qrtz_triggers (sched_name, trigger_name, trigger_group)
      ON DELETE CASCADE
);

CREATE TABLE jobs.qrtz_calendars
  (
    sched_name TEXT NOT NULL,
    calendar_name TEXT NOT NULL,
    calendar BYTEA NOT NULL,
    PRIMARY KEY (sched_name, calendar_name)
);

CREATE TABLE jobs.qrtz_paused_trigger_grps
  (
    sched_name TEXT NOT NULL,
    trigger_group TEXT NOT NULL,
    PRIMARY KEY (sched_name, trigger_group)
);

CREATE TABLE jobs.qrtz_paused_job_grps
  (
    sched_name TEXT NOT NULL,
    job_group TEXT NOT NULL,
    PRIMARY KEY (sched_name, job_group)
);

CREATE TABLE jobs.qrtz_fired_triggers
  (
    sched_name TEXT NOT NULL,
    entry_id TEXT NOT NULL,
    trigger_name TEXT NOT NULL,
    trigger_group TEXT NOT NULL,
    instance_name TEXT NOT NULL,
    fired_time BIGINT NOT NULL,
    sched_time BIGINT NOT NULL,
    priority INTEGER NOT NULL,
    state TEXT NOT NULL,
    job_name TEXT NULL,
    job_group TEXT NULL,
    is_nonconcurrent BOOL NOT NULL,
    requests_recovery BOOL NULL,
    execution_group VARCHAR(200) NULL,
    PRIMARY KEY (sched_name, entry_id)
);

CREATE TABLE jobs.qrtz_scheduler_state
  (
    sched_name TEXT NOT NULL,
    instance_name TEXT NOT NULL,
    last_checkin_time BIGINT NOT NULL,
    checkin_interval BIGINT NOT NULL,
    PRIMARY KEY (sched_name, instance_name)
);

CREATE TABLE jobs.qrtz_locks
  (
    sched_name TEXT NOT NULL,
    lock_name TEXT NOT NULL,
    PRIMARY KEY (sched_name, lock_name)
);

-- The two execution history tables. Optional: only a store configured with
-- UsePersistentStore(s => s.UseExecutionHistory()) reads or writes them, and nothing else in
-- this schema references them.
CREATE TABLE jobs.qrtz_execution_history
  (
    sched_name TEXT NOT NULL,
    entry_id TEXT NOT NULL,
    instance_name TEXT NOT NULL,
    job_name TEXT NOT NULL,
    job_group TEXT NOT NULL,
    trigger_name TEXT NOT NULL,
    trigger_group TEXT NOT NULL,
    fired_time BIGINT NOT NULL,
    run_time BIGINT NOT NULL,
    succeeded BOOL NOT NULL,
    error_message TEXT NULL,
    retry_attempt INTEGER NOT NULL DEFAULT 0,
    retry_scheduled BOOL NOT NULL DEFAULT FALSE,
    PRIMARY KEY (sched_name, entry_id)
);

CREATE TABLE jobs.qrtz_misfire_history
  (
    sched_name TEXT NOT NULL,
    entry_id TEXT NOT NULL,
    instance_name TEXT NOT NULL,
    trigger_name TEXT NOT NULL,
    trigger_group TEXT NOT NULL,
    job_name TEXT NULL,
    job_group TEXT NULL,
    misfire_time BIGINT NOT NULL,
    sched_time BIGINT NULL,
    PRIMARY KEY (sched_name, entry_id)
);

CREATE INDEX idx_qrtz_j_g_n ON jobs.qrtz_job_details (sched_name, job_group, job_name);
CREATE INDEX idx_qrtz_t_j ON jobs.qrtz_triggers (sched_name, job_name, job_group);
CREATE INDEX idx_qrtz_t_c ON jobs.qrtz_triggers (sched_name, calendar_name);
CREATE INDEX idx_qrtz_t_g_n ON jobs.qrtz_triggers (sched_name, trigger_group, trigger_name);
CREATE INDEX idx_qrtz_t_nft_st ON jobs.qrtz_triggers (sched_name, trigger_state, next_fire_time asc, priority desc, misfire_instr);
CREATE INDEX idx_qrtz_ft_inst_job_req_rcvry ON jobs.qrtz_fired_triggers (sched_name, instance_name, requests_recovery);
CREATE INDEX idx_qrtz_ft_j_g ON jobs.qrtz_fired_triggers (sched_name, job_name, job_group);
CREATE INDEX idx_qrtz_ft_t_g ON jobs.qrtz_fired_triggers (sched_name, trigger_name, trigger_group);
CREATE INDEX idx_qrtz_eh_fired_time ON jobs.qrtz_execution_history (sched_name, fired_time);
CREATE INDEX idx_qrtz_eh_inst ON jobs.qrtz_execution_history (sched_name, instance_name);
CREATE INDEX idx_qrtz_mh_misfire_time ON jobs.qrtz_misfire_history (sched_name, misfire_time);
CREATE INDEX idx_qrtz_mh_inst ON jobs.qrtz_misfire_history (sched_name, instance_name);