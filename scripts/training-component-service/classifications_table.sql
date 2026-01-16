-- Each training component → zero or many classifications
-- Each classification record is just a tag/label for that component

DROP TABLE IF EXISTS classifications CASCADE;

-- Create classifications table
CREATE TABLE classifications (
    classification_key TEXT PRIMARY KEY,
    training_component_code TEXT NOT NULL,
    purpose_code TEXT,
    scheme_code TEXT,
    value_code TEXT,
    action_on_entity TEXT,
    start_date TEXT,
    end_date TEXT,
    extension_data_present BOOLEAN DEFAULT FALSE,
    extension_data_element_count INTEGER,
    extension_data TEXT,
    fetched_created_at TIMESTAMPTZ DEFAULT NOW(),
    fetched_updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Create indexes for faster lookups
CREATE INDEX idx_classifications_component_code ON classifications(training_component_code);
CREATE INDEX idx_classifications_purpose_code ON classifications(purpose_code);
CREATE INDEX idx_classifications_scheme_code ON classifications(scheme_code);
CREATE INDEX idx_classifications_value_code ON classifications(value_code);

-- Create or replace function to automatically update fetched_updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.fetched_updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Create trigger to automatically update fetched_updated_at on row updates
CREATE TRIGGER update_classifications_updated_at
    BEFORE UPDATE ON classifications
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Grant necessary permissions (adjust as needed for your Supabase setup)
-- GRANT SELECT, INSERT, UPDATE ON classifications TO authenticated;
-- GRANT SELECT ON classifications TO anon;
