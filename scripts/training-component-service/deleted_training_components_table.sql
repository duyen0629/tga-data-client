-- Drop existing objects if they exist (in reverse order of dependencies)
DROP TABLE IF EXISTS deleted_training_components CASCADE;

-- Create deleted_training_components table
-- This table stores DeletedTrainingComponent data from TGA (Training.gov.au) service
-- Class: DeletedTrainingComponent from TgaTraining.cs
CREATE TABLE deleted_training_components (
    -- Composite primary key
    national_code TEXT NOT NULL,
    operation TEXT NOT NULL,
    updated_date TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (national_code, operation, updated_date),

    -- Extension data
    extension_data_present BOOLEAN DEFAULT FALSE,
    extension_data_element_count INTEGER,
    extension_data TEXT,

    -- Timestamps (fetch metadata)
    fetched_created_at TIMESTAMPTZ DEFAULT NOW(),
    fetched_updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Create indexes for faster lookups
CREATE INDEX idx_deleted_training_components_national_code ON deleted_training_components(national_code);
CREATE INDEX idx_deleted_training_components_updated_date ON deleted_training_components(updated_date);

-- Create or replace function to automatically update updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.fetched_updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Create trigger to automatically update updated_at on row updates
CREATE TRIGGER update_deleted_training_components_updated_at
    BEFORE UPDATE ON deleted_training_components
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Grant necessary permissions (adjust as needed for your Supabase setup)
-- GRANT SELECT, INSERT, UPDATE ON deleted_training_components TO authenticated;
-- GRANT SELECT ON deleted_training_components TO anon;
