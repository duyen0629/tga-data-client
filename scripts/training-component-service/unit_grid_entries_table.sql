-- One training component release → zero or many unit grid entries
DROP TABLE IF EXISTS unit_grid_entries CASCADE;

-- Create unit_grid_entries table
CREATE TABLE unit_grid_entries (
    unit_grid_entry_key TEXT PRIMARY KEY,
    training_component_code TEXT NOT NULL,
    release_number TEXT,
    release_date TEXT,
    release_currency TEXT,
    unit_code TEXT,
    unit_title TEXT,
    extension_data_present BOOLEAN DEFAULT FALSE,
    extension_data_element_count INTEGER,
    extension_data TEXT,
    fetched_created_at TIMESTAMPTZ DEFAULT NOW(),
    fetched_updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Create indexes for faster lookups
CREATE INDEX idx_unit_grid_entries_component_code ON unit_grid_entries(training_component_code);
CREATE INDEX idx_unit_grid_entries_unit_code ON unit_grid_entries(unit_code);

-- Create or replace function to automatically update fetched_updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.fetched_updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Create trigger to automatically update fetched_updated_at on row updates
CREATE TRIGGER update_unit_grid_entries_updated_at
    BEFORE UPDATE ON unit_grid_entries
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Grant necessary permissions (adjust as needed for your Supabase setup)
-- GRANT SELECT, INSERT, UPDATE ON unit_grid_entries TO authenticated;
-- GRANT SELECT ON unit_grid_entries TO anon;
