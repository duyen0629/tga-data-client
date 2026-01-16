-- A component can have many contacts
-- A contact can appear across multiple components

DROP TABLE IF EXISTS contacts CASCADE;

-- Create contacts table
CREATE TABLE contacts (
    contact_key TEXT PRIMARY KEY,
    training_component_code TEXT NOT NULL,
    role_code TEXT,
    type_code TEXT,
    first_name TEXT,
    last_name TEXT,
    organisation_name TEXT,
    email TEXT,
    phone TEXT,
    mobile TEXT,
    fax TEXT,
    group_name TEXT,
    job_title TEXT,
    title TEXT,
    postal_country_code TEXT,
    postal_line1 TEXT,
    postal_line2 TEXT,
    postal_suburb TEXT,
    postal_state_code TEXT,
    postal_state_overseas TEXT,
    postal_postcode TEXT,
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
CREATE INDEX idx_contacts_component_code ON contacts(training_component_code);
CREATE INDEX idx_contacts_role_code ON contacts(role_code);
CREATE INDEX idx_contacts_type_code ON contacts(type_code);

-- Create or replace function to automatically update fetched_updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.fetched_updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Create trigger to automatically update fetched_updated_at on row updates
CREATE TRIGGER update_contacts_updated_at
    BEFORE UPDATE ON contacts
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Grant necessary permissions (adjust as needed for your Supabase setup)
-- GRANT SELECT, INSERT, UPDATE ON contacts TO authenticated;
-- GRANT SELECT ON contacts TO anon;
