-- Drop existing objects if they exist (in reverse order of dependencies)
DROP TABLE IF EXISTS classification_scheme_values CASCADE;

-- Create classification_scheme_values table
-- This table stores classification scheme value data from TGA (Training.gov.au) service
CREATE TABLE classification_scheme_values (
    -- Primary key
    classification_value_key TEXT PRIMARY KEY,

    -- Scheme mapping
    scheme_code TEXT NOT NULL,

    -- Value details
    value TEXT NOT NULL,
    name TEXT NOT NULL,
    description TEXT NOT NULL,
    display_order INTEGER,
    action_on_entity TEXT,
    start_date TEXT,
    end_date TEXT,

    -- Extension data
    extension_data_present BOOLEAN DEFAULT FALSE,
    extension_data_element_count INTEGER,
    extension_data TEXT,

    -- Timestamps (fetch metadata)
    fetched_created_at TIMESTAMPTZ DEFAULT NOW(),
    fetched_updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Create indexes for faster lookups
CREATE INDEX idx_classification_scheme_values_scheme_code ON classification_scheme_values(scheme_code);

-- Create or replace function to automatically update updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.fetched_updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Create trigger to automatically update updated_at on row updates
CREATE TRIGGER update_classification_scheme_values_updated_at
    BEFORE UPDATE ON classification_scheme_values
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Grant necessary permissions (adjust as needed for your Supabase setup)
-- GRANT SELECT, INSERT, UPDATE ON classification_scheme_values TO authenticated;
-- GRANT SELECT ON classification_scheme_values TO anon;
