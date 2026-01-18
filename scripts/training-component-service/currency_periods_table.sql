-- One training component → zero or many currency periods
DROP TABLE IF EXISTS currency_periods CASCADE;

-- Create currency_periods table
CREATE TABLE currency_periods (
    currency_period_key TEXT PRIMARY KEY,
    training_component_code TEXT NOT NULL,
    authority TEXT,
    end_comment TEXT,
    end_reason_code TEXT,
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
CREATE INDEX idx_currency_periods_component_code ON currency_periods(training_component_code);
CREATE INDEX idx_currency_periods_authority ON currency_periods(authority);

-- Create or replace function to automatically update fetched_updated_at timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.fetched_updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Create trigger to automatically update fetched_updated_at on row updates
CREATE TRIGGER update_currency_periods_updated_at
    BEFORE UPDATE ON currency_periods
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Grant necessary permissions (adjust as needed for your Supabase setup)
-- GRANT SELECT, INSERT, UPDATE ON currency_periods TO authenticated;
-- GRANT SELECT ON currency_periods TO anon;
