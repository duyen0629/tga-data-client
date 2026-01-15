-- Drop existing objects if they exist (in reverse order of dependencies)
-- Drop table first (this will cascade and drop triggers/indexes automatically)
DROP TABLE IF EXISTS address_states CASCADE;

-- Create address_states table
CREATE TABLE address_states (
    code TEXT PRIMARY KEY,
    abbreviation TEXT,
    description TEXT,
    extension_data_present BOOLEAN DEFAULT FALSE,
    extension_data_element_count INTEGER,
    extension_data TEXT,
    fetched_created_at TIMESTAMPTZ DEFAULT NOW(),
    fetched_updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Create index on code for faster lookups
CREATE INDEX idx_address_states_code ON address_states(code);

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
CREATE TRIGGER update_address_states_updated_at
    BEFORE UPDATE ON address_states
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Grant necessary permissions (adjust as needed for your Supabase setup)
-- GRANT SELECT, INSERT, UPDATE ON address_states TO authenticated;
-- GRANT SELECT ON address_states TO anon;
