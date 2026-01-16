-- Drop existing objects if they exist (in reverse order of dependencies)
DROP TABLE IF EXISTS mappings CASCADE;

-- Create mappings table
CREATE TABLE mappings (
    mapping_key TEXT PRIMARY KEY,
    training_component_code TEXT NOT NULL,
    code TEXT,
    is_equivalent BOOLEAN,
    maps_to_code TEXT,
    maps_to_title TEXT,
    notes TEXT,
    title TEXT,
    extension_data_present BOOLEAN DEFAULT FALSE,
    extension_data_element_count INTEGER,
    extension_data TEXT,
    fetched_created_at TIMESTAMPTZ DEFAULT NOW(),
    fetched_updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Create indexes for faster lookups
CREATE INDEX idx_mappings_component_code ON mappings(training_component_code);
CREATE INDEX idx_mappings_code ON mappings(code);
CREATE INDEX idx_mappings_maps_to_code ON mappings(maps_to_code);

-- Create or replace function to automatically update fetched_updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.fetched_updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Create trigger to automatically update fetched_updated_at on row updates
CREATE TRIGGER update_mappings_updated_at
    BEFORE UPDATE ON mappings
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Grant necessary permissions (adjust as needed for your Supabase setup)
-- GRANT SELECT, INSERT, UPDATE ON mappings TO authenticated;
-- GRANT SELECT ON mappings TO anon;
