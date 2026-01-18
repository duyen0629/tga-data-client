-- One training component → zero or many usage recommendations
DROP TABLE IF EXISTS usage_recommendations CASCADE;

-- Create usage_recommendations table
CREATE TABLE usage_recommendations (
    usage_recommendation_key TEXT PRIMARY KEY,
    training_component_code TEXT NOT NULL,
    state TEXT,
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
CREATE INDEX idx_usage_recommendations_component_code ON usage_recommendations(training_component_code);
CREATE INDEX idx_usage_recommendations_state ON usage_recommendations(state);

-- Create or replace function to automatically update fetched_updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.fetched_updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Create trigger to automatically update fetched_updated_at on row updates
CREATE TRIGGER update_usage_recommendations_updated_at
    BEFORE UPDATE ON usage_recommendations
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Grant necessary permissions (adjust as needed for your Supabase setup)
-- GRANT SELECT, INSERT, UPDATE ON usage_recommendations TO authenticated;
-- GRANT SELECT ON usage_recommendations TO anon;
