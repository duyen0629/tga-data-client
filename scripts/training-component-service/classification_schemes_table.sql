-- Drop existing objects if they exist (in reverse order of dependencies)
DROP TABLE IF EXISTS classification_schemes CASCADE;

-- Create classification_schemes table
-- This table stores NrtClassificationSchemeResult data from TGA (Training.gov.au) service
-- Class: NrtClassificationSchemeResult from TgaTraining.cs
CREATE TABLE classification_schemes (
    -- Primary key
    scheme_code TEXT PRIMARY KEY,

    -- Scheme details
    name TEXT NOT NULL,
    description TEXT NOT NULL,
    allow_multiple_values BOOLEAN DEFAULT FALSE,
    is_protected BOOLEAN DEFAULT FALSE,
    applies_to_component_types TEXT,
    required_for_component_types TEXT,
    classification_values_count INTEGER DEFAULT 0,

    -- Extension data
    extension_data_present BOOLEAN DEFAULT FALSE,
    extension_data_element_count INTEGER,
    extension_data TEXT,

    -- Timestamps (fetch metadata)
    fetched_created_at TIMESTAMPTZ DEFAULT NOW(),
    fetched_updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Create indexes for faster lookups
CREATE INDEX idx_classification_schemes_scheme_code ON classification_schemes(scheme_code);

-- Create or replace function to automatically update updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.fetched_updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Create trigger to automatically update updated_at on row updates
CREATE TRIGGER update_classification_schemes_updated_at
    BEFORE UPDATE ON classification_schemes
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Grant necessary permissions (adjust as needed for your Supabase setup)
-- GRANT SELECT, INSERT, UPDATE ON classification_schemes TO authenticated;
-- GRANT SELECT ON classification_schemes TO anon;
