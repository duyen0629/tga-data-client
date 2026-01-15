-- Drop existing objects if they exist (in reverse order of dependencies)
-- Drop table first (this will cascade and drop triggers/indexes automatically)
DROP TABLE IF EXISTS data_managers CASCADE;

-- Create data_managers table
CREATE TABLE data_managers (
    code TEXT PRIMARY KEY,
    description TEXT NOT NULL,
    recognition_manager_code TEXT,
    registration_manager_code TEXT,
    extension_data_present BOOLEAN DEFAULT FALSE,
    extension_data_element_count INTEGER,
    extension_data TEXT,
    fetched_created_at TIMESTAMPTZ DEFAULT NOW(),
    fetched_updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Create index on code for faster lookups (though it's already the primary key)
CREATE INDEX idx_data_managers_code ON data_managers(code);

-- Create or replace function to automatically update fetched_updated_at timestamp
-- (Using OR REPLACE in case function already exists from other tables)
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.fetched_updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Create trigger to automatically update fetched_updated_at on row updates
CREATE TRIGGER update_data_managers_updated_at
    BEFORE UPDATE ON data_managers
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Grant necessary permissions (adjust as needed for your Supabase setup)
-- GRANT SELECT, INSERT, UPDATE ON data_managers TO authenticated;
-- GRANT SELECT ON data_managers TO anon;
