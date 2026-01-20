-- Drop existing objects if they exist (in reverse order of dependencies)
DROP TABLE IF EXISTS classification_purposes CASCADE;

-- Create classification_purposes table
-- This table stores ClassificationPurpose data from TGA (Training.gov.au) service
-- Class: ClassificationPurpose from TgaTraining.cs
CREATE TABLE classification_purposes (
    -- Primary key
    purpose_code TEXT PRIMARY KEY,

    -- Purpose details
    description TEXT NOT NULL,

    -- Extension data
    extension_data_present BOOLEAN DEFAULT FALSE,
    extension_data_element_count INTEGER,
    extension_data TEXT,

    -- Timestamps (fetch metadata)
    fetched_created_at TIMESTAMPTZ DEFAULT NOW(),
    fetched_updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Create indexes for faster lookups
CREATE INDEX idx_classification_purposes_purpose_code ON classification_purposes(purpose_code);

-- Create or replace function to automatically update updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.fetched_updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Create trigger to automatically update updated_at on row updates
CREATE TRIGGER update_classification_purposes_updated_at
    BEFORE UPDATE ON classification_purposes
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Grant necessary permissions (adjust as needed for your Supabase setup)
-- GRANT SELECT, INSERT, UPDATE ON classification_purposes TO authenticated;
-- GRANT SELECT ON classification_purposes TO anon;
