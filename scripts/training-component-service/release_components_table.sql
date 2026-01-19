-- One training component release → zero or many release components
DROP TABLE IF EXISTS release_components CASCADE;

-- Create release_components table
CREATE TABLE release_components (
    release_component_key TEXT PRIMARY KEY,
    training_component_code TEXT NOT NULL,
    release_number TEXT,
    release_date TEXT,
    release_currency TEXT,
    component_code TEXT,
    component_title TEXT,
    component_type TEXT,
    component_release_number TEXT,
    component_release_date TEXT,
    component_release_currency TEXT,
    extension_data_present BOOLEAN DEFAULT FALSE,
    extension_data_element_count INTEGER,
    extension_data TEXT,
    fetched_created_at TIMESTAMPTZ DEFAULT NOW(),
    fetched_updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Create indexes for faster lookups
CREATE INDEX idx_release_components_component_code ON release_components(training_component_code);
CREATE INDEX idx_release_components_release_number ON release_components(release_number);

-- Create or replace function to automatically update fetched_updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.fetched_updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Create trigger to automatically update fetched_updated_at on row updates
CREATE TRIGGER update_release_components_updated_at
    BEFORE UPDATE ON release_components
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Grant necessary permissions (adjust as needed for your Supabase setup)
-- GRANT SELECT, INSERT, UPDATE ON release_components TO authenticated;
-- GRANT SELECT ON release_components TO anon;
