-- Migration: add usage_recommendation to training_component_documents (from training_component_summaries).
-- Run if the table already exists without this column.

ALTER TABLE training_component_documents
    ADD COLUMN IF NOT EXISTS usage_recommendation TEXT;

COMMENT ON COLUMN training_component_documents.usage_recommendation IS 'From training_component_summaries.usage_recommendation';
