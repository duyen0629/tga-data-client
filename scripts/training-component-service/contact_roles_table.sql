-- Drop existing objects if they exist (in reverse order of dependencies)
-- Drop table first (this will cascade and drop triggers/indexes automatically)
DROP TABLE IF EXISTS contact_roles CASCADE;

-- Create contact_roles table
CREATE TABLE contact_roles (
    role TEXT PRIMARY KEY,
    description TEXT,
    allow_group_contact BOOLEAN,
    allow_multiple_current BOOLEAN,
    is_implicit BOOLEAN,
    required_training_component_types TEXT,
    extension_data_present BOOLEAN DEFAULT FALSE,
    extension_data_element_count INTEGER,
    extension_data TEXT,
    fetched_created_at TIMESTAMPTZ DEFAULT NOW(),
    fetched_updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Create index on role for faster lookups
CREATE INDEX idx_contact_roles_role ON contact_roles(role);

-- Create or replace function to automatically update fetched_updated_at timestamp
-- (Using OR REPLACE in case function already exists from other tables)
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.fetched_updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Create trigger to automatically update fetched_updated_at on row updates
CREATE TRIGGER update_contact_roles_updated_at
    BEFORE UPDATE ON contact_roles
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Grant necessary permissions (adjust as needed for your Supabase setup)
-- GRANT SELECT, INSERT, UPDATE ON contact_roles TO authenticated;
-- GRANT SELECT ON contact_roles TO anon;
