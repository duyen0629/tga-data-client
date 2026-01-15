-- Non-destructive update for existing table (keeps data)
-- end_date must be nullable, so it cannot be part of the primary key
ALTER TABLE IF EXISTS recognition_manager_assignments
    DROP CONSTRAINT IF EXISTS recognition_manager_assignments_pkey;

-- Full rebuild (drops data) - uncomment if you want a clean recreate
DROP TABLE IF EXISTS recognition_manager_assignments CASCADE;

-- Create recognition_manager_assignments table
CREATE TABLE IF NOT EXISTS recognition_manager_assignments (
    training_component_code TEXT NOT NULL,
    recognition_manager_code TEXT NOT NULL,
    action_on_entity TEXT,
    start_date TEXT,
    end_date TEXT NULL,
    extension_data_present BOOLEAN DEFAULT FALSE,
    extension_data_element_count INTEGER,
    extension_data TEXT,
    fetched_created_at TIMESTAMPTZ DEFAULT NOW(),
    fetched_updated_at TIMESTAMPTZ DEFAULT NOW()
    -- Primary key added below to avoid multiple PK errors on re-run
);

-- Ensure new columns exist if table was created earlier
ALTER TABLE IF EXISTS recognition_manager_assignments
    ADD COLUMN IF NOT EXISTS extension_data_present BOOLEAN DEFAULT FALSE;
ALTER TABLE IF EXISTS recognition_manager_assignments
    ADD COLUMN IF NOT EXISTS extension_data_element_count INTEGER;
ALTER TABLE IF EXISTS recognition_manager_assignments
    ADD COLUMN IF NOT EXISTS extension_data TEXT;
ALTER TABLE IF EXISTS recognition_manager_assignments
    ADD COLUMN IF NOT EXISTS fetched_created_at TIMESTAMPTZ DEFAULT NOW();
ALTER TABLE IF EXISTS recognition_manager_assignments
    ADD COLUMN IF NOT EXISTS fetched_updated_at TIMESTAMPTZ DEFAULT NOW();

-- Recreate primary key if missing (safe on re-run)
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'recognition_manager_assignments_pkey'
          AND conrelid = 'recognition_manager_assignments'::regclass
    ) THEN
        ALTER TABLE recognition_manager_assignments
            ADD CONSTRAINT recognition_manager_assignments_pkey
            PRIMARY KEY (training_component_code, recognition_manager_code, start_date);
    END IF;
END $$;

-- Create indexes for faster lookups
CREATE INDEX IF NOT EXISTS idx_rec_mgr_assignments_component_code ON recognition_manager_assignments(training_component_code);
CREATE INDEX IF NOT EXISTS idx_rec_mgr_assignments_manager_code ON recognition_manager_assignments(recognition_manager_code);

-- Create or replace function to automatically update fetched_updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.fetched_updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Create trigger to automatically update fetched_updated_at on row updates
DROP TRIGGER IF EXISTS update_recognition_manager_assignments_updated_at ON recognition_manager_assignments;
CREATE TRIGGER update_recognition_manager_assignments_updated_at
    BEFORE UPDATE ON recognition_manager_assignments
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Grant necessary permissions (adjust as needed for your Supabase setup)
-- GRANT SELECT, INSERT, UPDATE ON recognition_manager_assignments TO authenticated;
-- GRANT SELECT ON recognition_manager_assignments TO anon;
