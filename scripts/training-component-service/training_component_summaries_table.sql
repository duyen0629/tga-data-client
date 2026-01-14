-- Drop existing objects if they exist (in reverse order of dependencies)
DROP TABLE IF EXISTS training_component_summaries CASCADE;

-- Create training_component_summaries table
-- This table stores TrainingComponentSummary data from TGA (Training.gov.au) service
-- Class: TrainingComponentSummary from TgaTraining.cs
CREATE TABLE training_component_summaries (
    -- Primary key
    code TEXT PRIMARY KEY,
    
    -- Basic information
    title TEXT,
    component_type TEXT NOT NULL,
    
    -- Status flags
    is_confidential BOOLEAN DEFAULT FALSE,
    is_current BOOLEAN,
    is_legacy_data BOOLEAN,
    
    -- Status information
    currency_status TEXT,
    usage_recommendation TEXT,
    
    -- Dates (stored as TIMESTAMPTZ)
    created_date TIMESTAMPTZ,
    updated_date TIMESTAMPTZ,
    
    -- Extension data
    extension_data_present BOOLEAN DEFAULT FALSE,
    extension_data_element_count INTEGER,
    extension_data TEXT,
    
    -- Timestamps
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Create indexes for faster lookups
CREATE INDEX idx_training_component_summaries_component_type ON training_component_summaries(component_type);
CREATE INDEX idx_training_component_summaries_is_current ON training_component_summaries(is_current);
CREATE INDEX idx_training_component_summaries_updated_date ON training_component_summaries(updated_date);
CREATE INDEX idx_training_component_summaries_title ON training_component_summaries(title);

-- Create or replace function to automatically update updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Create trigger to automatically update updated_at on row updates
CREATE TRIGGER update_training_component_summaries_updated_at
    BEFORE UPDATE ON training_component_summaries
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Grant necessary permissions (adjust as needed for your Supabase setup)
-- GRANT SELECT, INSERT, UPDATE ON training_component_summaries TO authenticated;
-- GRANT SELECT ON training_component_summaries TO anon;
