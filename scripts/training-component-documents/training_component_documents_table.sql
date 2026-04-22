-- One training component release → one merged document record (Complete + AssessmentRequirements)
DROP TABLE IF EXISTS training_component_documents CASCADE;

-- Create training_component_documents table
CREATE TABLE training_component_documents (
    training_component_code TEXT NOT NULL,
    release_number TEXT NOT NULL,
    component_type TEXT, -- from training_component_summaries
    usage_recommendation TEXT, -- from training_component_summaries
    title TEXT,
    source_files JSONB, -- e.g., { complete: { relative_path, generated_date }, assessment_requirements: { ... } }
    content_json JSONB, -- merged, display-ready sections
    raw_xml TEXT, -- raw XML file contents
    process_error TEXT, -- error message if processing failed
    parsed_at TIMESTAMPTZ,
    fetched_created_at TIMESTAMPTZ DEFAULT NOW(),
    fetched_updated_at TIMESTAMPTZ DEFAULT NOW(),
    PRIMARY KEY (training_component_code, release_number)
);

-- Create indexes for faster lookups
CREATE INDEX idx_training_component_documents_component_code ON training_component_documents(training_component_code);
CREATE INDEX idx_training_component_documents_release_number ON training_component_documents(release_number);
CREATE INDEX idx_training_component_documents_component_type ON training_component_documents(component_type);

-- Create or replace function to automatically update fetched_updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.fetched_updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Create trigger to automatically update fetched_updated_at on row updates
CREATE TRIGGER update_training_component_documents_updated_at
    BEFORE UPDATE ON training_component_documents
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Grant necessary permissions (adjust as needed for your Supabase setup)
-- GRANT SELECT, INSERT, UPDATE ON training_component_documents TO authenticated;
-- GRANT SELECT ON training_component_documents TO anon;
