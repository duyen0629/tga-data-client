-- Drop existing objects if they exist (in reverse order of dependencies)
-- Drop table first (this will cascade and drop triggers/indexes automatically)
DROP TABLE IF EXISTS validation_codes CASCADE;

-- Create validation_codes table
CREATE TABLE validation_codes (
    code TEXT NOT NULL,
    sub_code TEXT,
    message TEXT,
    extension_data_present BOOLEAN DEFAULT FALSE,
    extension_data_element_count INTEGER,
    extension_data TEXT,
    fetched_created_at TIMESTAMPTZ DEFAULT NOW(),
    fetched_updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Create index on code for faster lookups
CREATE INDEX idx_validation_codes_code ON validation_codes(code);
CREATE INDEX idx_validation_codes_sub_code ON validation_codes(sub_code);

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
CREATE TRIGGER update_validation_codes_updated_at
    BEFORE UPDATE ON validation_codes
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Grant necessary permissions (adjust as needed for your Supabase setup)
-- GRANT SELECT, INSERT, UPDATE ON validation_codes TO authenticated;
-- GRANT SELECT ON validation_codes TO anon;
