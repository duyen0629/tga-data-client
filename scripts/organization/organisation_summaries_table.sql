-- Drop existing objects if they exist (in reverse order of dependencies)
DROP TABLE IF EXISTS organisation_summaries CASCADE;

-- Create organisation_summaries table
-- This table stores OrganisationSearchResultItem data from OrganisationService
CREATE TABLE organisation_summaries (
    code TEXT PRIMARY KEY,
    data_manager_code TEXT,
    has_active_registration BOOLEAN DEFAULT FALSE,
    legal_person_name TEXT,
    trading_name TEXT,
    updated_date TIMESTAMPTZ,
    is_legacy_data BOOLEAN DEFAULT FALSE,
    registration_status TEXT,
    extension_data_present BOOLEAN DEFAULT FALSE,
    extension_data_element_count INTEGER,
    extension_data TEXT,
    fetched_created_at TIMESTAMPTZ DEFAULT NOW(),
    fetched_updated_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_organisation_summaries_updated_date ON organisation_summaries(updated_date);
CREATE INDEX idx_organisation_summaries_data_manager_code ON organisation_summaries(data_manager_code);

CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.fetched_updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER update_organisation_summaries_updated_at
    BEFORE UPDATE ON organisation_summaries
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();
