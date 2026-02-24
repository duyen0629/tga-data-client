-- Migration: add component_type to training_component_documents (from training_component_summaries).
-- Run this if the table already exists and you want to add the column without recreating the table.

ALTER TABLE training_component_documents
    ADD COLUMN IF NOT EXISTS component_type TEXT;

COMMENT ON COLUMN training_component_documents.component_type IS 'From training_component_summaries.component_type';

CREATE INDEX IF NOT EXISTS idx_training_component_documents_component_type
    ON training_component_documents(component_type);
