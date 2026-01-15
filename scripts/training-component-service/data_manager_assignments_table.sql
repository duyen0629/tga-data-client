-- One training component can have multiple data managers
DROP TABLE IF EXISTS data_manager_assignments CASCADE;

CREATE TABLE data_manager_assignments (
    training_component_code TEXT NOT NULL,
    data_manager_code TEXT NOT NULL,
    action_on_entity TEXT,
    start_date TEXT,
    end_date TEXT NULL,
    extension_data_present BOOLEAN DEFAULT FALSE,
    extension_data_element_count INTEGER,
    extension_data TEXT,
    fetched_created_at TIMESTAMPTZ DEFAULT NOW(),
    fetched_updated_at TIMESTAMPTZ DEFAULT NOW(),
    PRIMARY KEY (training_component_code, data_manager_code, start_date)
);

CREATE INDEX IF NOT EXISTS idx_data_mgr_assignments_component_code ON data_manager_assignments(training_component_code);
CREATE INDEX IF NOT EXISTS idx_data_mgr_assignments_manager_code ON data_manager_assignments(data_manager_code);

CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.fetched_updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS update_data_manager_assignments_updated_at ON data_manager_assignments;
CREATE TRIGGER update_data_manager_assignments_updated_at
    BEFORE UPDATE ON data_manager_assignments
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

