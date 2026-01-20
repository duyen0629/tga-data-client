-- Drop existing objects if they exist (in reverse order of dependencies)
DROP TABLE IF EXISTS nrt_classification_scheme_values CASCADE;

-- Create nrt_classification_scheme_values table
-- Child table for NRT scheme values (many rows per scheme_code)
CREATE TABLE nrt_classification_scheme_values (
    classification_value_key TEXT PRIMARY KEY,
    scheme_code TEXT NOT NULL,
    value TEXT NOT NULL,
    name TEXT NOT NULL,
    description TEXT NOT NULL,
    display_order INTEGER,
    action_on_entity TEXT,
    start_date TEXT,
    end_date TEXT,
    extension_data_present BOOLEAN DEFAULT FALSE,
    extension_data_element_count INTEGER,
    extension_data TEXT,
    fetched_created_at TIMESTAMPTZ DEFAULT NOW(),
    fetched_updated_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_nrt_classification_scheme_values_scheme_code ON nrt_classification_scheme_values(scheme_code);

CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.fetched_updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER update_nrt_classification_scheme_values_updated_at
    BEFORE UPDATE ON nrt_classification_scheme_values
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();
