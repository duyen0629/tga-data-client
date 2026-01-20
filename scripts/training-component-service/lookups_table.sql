-- Drop existing objects if they exist (in reverse order of dependencies)
DROP TABLE IF EXISTS lookups CASCADE;

-- Create lookups table
-- This table stores Lookup data from TGA (Training.gov.au) service
-- Class: Lookup from TgaTraining.cs
CREATE TABLE lookups (
    -- Primary key
    lookup_key TEXT PRIMARY KEY,

    -- Lookup context
    lookup_name TEXT NOT NULL,

    -- Lookup values
    code TEXT NOT NULL,
    description TEXT,

    -- Extension data
    extension_data_present BOOLEAN DEFAULT FALSE,
    extension_data_element_count INTEGER,
    extension_data TEXT,

    -- Timestamps (fetch metadata)
    fetched_created_at TIMESTAMPTZ DEFAULT NOW(),
    fetched_updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Create indexes for faster lookups
CREATE INDEX idx_lookups_lookup_name ON lookups(lookup_name);
CREATE INDEX idx_lookups_code ON lookups(code);

-- Create or replace function to automatically update updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.fetched_updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Create trigger to automatically update updated_at on row updates
CREATE TRIGGER update_lookups_updated_at
    BEFORE UPDATE ON lookups
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Grant necessary permissions (adjust as needed for your Supabase setup)
-- GRANT SELECT, INSERT, UPDATE ON lookups TO authenticated;
-- GRANT SELECT ON lookups TO anon;
