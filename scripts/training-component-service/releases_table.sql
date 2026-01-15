-- One training component can have 0, 1, or multiple releases
DROP TABLE IF EXISTS releases CASCADE;

CREATE TABLE releases (
    training_component_code TEXT NOT NULL,
    release_number TEXT NOT NULL,
    release_date TEXT,
    currency TEXT,
    approval_process TEXT,
    isc_approval_date TEXT,
    ministerial_agreement_date TEXT,
    nqc_endorsement_date TEXT,
    components_count INTEGER,
    files_count INTEGER,
    unit_grid_count INTEGER,
    extension_data_present BOOLEAN DEFAULT FALSE,
    extension_data_element_count INTEGER,
    extension_data TEXT,
    fetched_created_at TIMESTAMPTZ DEFAULT NOW(),
    fetched_updated_at TIMESTAMPTZ DEFAULT NOW(),
    PRIMARY KEY (training_component_code, release_number)
);

-- Create indexes for faster lookups
CREATE INDEX idx_releases_component_code ON releases(training_component_code);
CREATE INDEX idx_releases_release_number ON releases(release_number);

-- Create or replace function to automatically update fetched_updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.fetched_updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Create trigger to automatically update fetched_updated_at on row updates
CREATE TRIGGER update_releases_updated_at
    BEFORE UPDATE ON releases
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Grant necessary permissions (adjust as needed for your Supabase setup)
-- GRANT SELECT, INSERT, UPDATE ON releases TO authenticated;
-- GRANT SELECT ON releases TO anon;
