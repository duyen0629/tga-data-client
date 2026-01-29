-- Per-user completion state for document items (checkboxes)
DROP TABLE IF EXISTS training_component_item_status CASCADE;

CREATE TABLE training_component_item_status (
    user_id TEXT NOT NULL,
    training_component_code TEXT NOT NULL,
    release_number TEXT NOT NULL,
    item_id TEXT NOT NULL, -- stable identifier stored in content_json items
    section_key TEXT,
    checked BOOLEAN DEFAULT FALSE,
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    PRIMARY KEY (user_id, training_component_code, release_number, item_id)
);

-- Create indexes for faster lookups
CREATE INDEX idx_training_component_item_status_user ON training_component_item_status(user_id);
CREATE INDEX idx_training_component_item_status_component ON training_component_item_status(training_component_code);
CREATE INDEX idx_training_component_item_status_release ON training_component_item_status(release_number);

-- Grant necessary permissions (adjust as needed for your Supabase setup)
-- GRANT SELECT, INSERT, UPDATE ON training_component_item_status TO authenticated;
-- GRANT SELECT ON training_component_item_status TO anon;
