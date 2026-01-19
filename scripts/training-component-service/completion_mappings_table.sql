-- One training component → zero or many completion mappings
DROP TABLE IF EXISTS completion_mappings CASCADE;

-- Create completion_mappings table
CREATE TABLE completion_mappings (
    completion_mapping_key TEXT PRIMARY KEY,
    training_component_code TEXT NOT NULL,
    code TEXT,
    is_mandatory BOOLEAN,
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
CREATE INDEX idx_completion_mappings_component_code ON completion_mappings(training_component_code);
CREATE INDEX idx_completion_mappings_code ON completion_mappings(code);

-- Create or replace function to automatically update fetched_updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.fetched_updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Create trigger to automatically update fetched_updated_at on row updates
CREATE TRIGGER update_completion_mappings_updated_at
    BEFORE UPDATE ON completion_mappings
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Grant necessary permissions (adjust as needed for your Supabase setup)
-- GRANT SELECT, INSERT, UPDATE ON completion_mappings TO authenticated;
-- GRANT SELECT ON completion_mappings TO anon;
