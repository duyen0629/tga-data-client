-- Drop existing objects if they exist (in reverse order of dependencies)
DROP TABLE IF EXISTS nrt_classification_schemes CASCADE;

-- Create nrt_classification_schemes table
-- Parent table for NRT classification schemes (one row per scheme_code)
CREATE TABLE nrt_classification_schemes (
    scheme_code TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    description TEXT NOT NULL,
    allow_multiple_values BOOLEAN DEFAULT FALSE,
    is_protected BOOLEAN DEFAULT FALSE,
    applies_to_component_types TEXT,
    required_for_component_types TEXT,
    classification_values_count INTEGER DEFAULT 0,
    extension_data_present BOOLEAN DEFAULT FALSE,
    extension_data_element_count INTEGER,
    extension_data TEXT,
    fetched_created_at TIMESTAMPTZ DEFAULT NOW(),
    fetched_updated_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_nrt_classification_schemes_scheme_code ON nrt_classification_schemes(scheme_code);

CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.fetched_updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER update_nrt_classification_schemes_updated_at
    BEFORE UPDATE ON nrt_classification_schemes
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();
