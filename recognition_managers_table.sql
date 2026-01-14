-- SQL script to create the recognition_managers table in Supabase
-- Run this in your Supabase SQL Editor

CREATE TABLE IF NOT EXISTS recognition_managers (
    code TEXT PRIMARY KEY,
    description TEXT NOT NULL,
    short_name TEXT NOT NULL,
    extension_data_present BOOLEAN DEFAULT FALSE,
    extension_data_element_count INTEGER,
    extension_data TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Create an index on code for faster lookups (already primary key, but explicit)
CREATE INDEX IF NOT EXISTS idx_recognition_managers_code ON recognition_managers(code);

-- Optional: Create updated_at trigger to automatically update timestamp on row updates
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ language 'plpgsql';

-- Drop trigger if exists to avoid errors on re-run
DROP TRIGGER IF EXISTS update_recognition_managers_updated_at ON recognition_managers;

-- Create trigger
CREATE TRIGGER update_recognition_managers_updated_at 
    BEFORE UPDATE ON recognition_managers 
    FOR EACH ROW 
    EXECUTE FUNCTION update_updated_at_column();

-- Enable Row Level Security (RLS) if needed
-- ALTER TABLE recognition_managers ENABLE ROW LEVEL SECURITY;

-- Example policy to allow all operations with valid API key (adjust based on your security needs)
-- CREATE POLICY "Allow all operations with service role" ON recognition_managers
--     FOR ALL USING (true);
