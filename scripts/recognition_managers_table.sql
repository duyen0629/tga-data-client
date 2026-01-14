-- Drop existing objects if they exist (in reverse order of dependencies)
DROP TRIGGER IF EXISTS update_recognition_managers_updated_at ON recognition_managers;
DROP INDEX IF EXISTS idx_recognition_managers_code;
DROP TABLE IF EXISTS recognition_managers;
DROP FUNCTION IF EXISTS update_updated_at_column();

-- Create recognition_managers table
CREATE TABLE recognition_managers (
    code TEXT PRIMARY KEY,
    description TEXT NOT NULL,
    short_name TEXT NOT NULL,
    extension_data_present BOOLEAN DEFAULT FALSE,
    extension_data_element_count INTEGER,
    extension_data TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Create index on code for faster lookups (though it's already the primary key)
CREATE INDEX idx_recognition_managers_code ON recognition_managers(code);

-- Create function to automatically update updated_at timestamp
CREATE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Create trigger to automatically update updated_at on row updates
CREATE TRIGGER update_recognition_managers_updated_at
    BEFORE UPDATE ON recognition_managers
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Grant necessary permissions (adjust as needed for your Supabase setup)
-- GRANT SELECT, INSERT, UPDATE ON recognition_managers TO authenticated;
-- GRANT SELECT ON recognition_managers TO anon;
