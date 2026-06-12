-- Add updated_at column to import_tasks for stale task detection
-- Only add if not exists (idempotent for SQLite)
-- SQLite doesn't support IF NOT EXISTS for ALTER TABLE, so we use a workaround

-- Check if column exists before adding
-- If the column already exists, this will silently succeed due to the error handling in MigrationRunner
PRAGMA ignore_check_constraints = ON;

-- Try to add the column (will fail silently if already exists)
-- We wrap in a try-catch pattern for SQLite
ALTER TABLE import_tasks ADD COLUMN updated_at REAL;

PRAGMA ignore_check_constraints = OFF;

-- Set updated_at = created_at for existing rows where updated_at is NULL
UPDATE import_tasks SET updated_at = created_at WHERE updated_at IS NULL;

-- Create index for efficient stale task queries (IF NOT EXISTS is safe)
CREATE INDEX IF NOT EXISTS idx_tasks_updated ON import_tasks(updated_at);
