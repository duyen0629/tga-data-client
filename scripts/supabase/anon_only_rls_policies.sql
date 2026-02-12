-- Apply anon-only access policies across all project tables.
-- Run this after creating/updating tables.
--
-- Notes:
-- 1) This allows only the `anon` role to read/write rows through PostgREST.
-- 2) `authenticated` will be blocked because no policies are created for it.
-- 3) The `service_role` key has BYPASSRLS in Supabase and can still bypass RLS.

DO $$
DECLARE
    table_name TEXT;
    target_tables TEXT[] := ARRAY[
        'training_component_documents',
        'training_component_item_status',
        'organisation_summaries',
        'nrt_classification_scheme_values',
        'rto_classification_scheme_values',
        'rto_classification_schemes',
        'nrt_classification_schemes',
        'lookups',
        'classification_purposes',
        'classification_scheme_values',
        'classification_schemes',
        'deleted_training_components',
        'unit_grid_entries',
        'release_components',
        'release_files',
        'completion_mappings',
        'usage_recommendations',
        'currency_periods',
        'mappings',
        'classifications',
        'contacts',
        'releases',
        'recognition_manager_assignments',
        'data_manager_assignments',
        'address_states',
        'contact_roles',
        'validation_codes',
        'data_managers',
        'training_component_summaries',
        'recognition_managers'
    ];
BEGIN
    FOREACH table_name IN ARRAY target_tables
    LOOP
        -- Skip tables that are not created yet.
        IF to_regclass(format('public.%I', table_name)) IS NULL THEN
            RAISE NOTICE 'Skipping missing table: %.%', 'public', table_name;
            CONTINUE;
        END IF;

        -- Ensure RLS is active.
        EXECUTE format('ALTER TABLE IF EXISTS public.%I ENABLE ROW LEVEL SECURITY;', table_name);
        EXECUTE format('ALTER TABLE IF EXISTS public.%I FORCE ROW LEVEL SECURITY;', table_name);

        -- Restrict table privileges to anon only.
        EXECUTE format('REVOKE ALL ON TABLE public.%I FROM anon, authenticated;', table_name);
        EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE public.%I TO anon;', table_name);

        -- Recreate anon policies idempotently.
        EXECUTE format('DROP POLICY IF EXISTS anon_select ON public.%I;', table_name);
        EXECUTE format('DROP POLICY IF EXISTS anon_insert ON public.%I;', table_name);
        EXECUTE format('DROP POLICY IF EXISTS anon_update ON public.%I;', table_name);
        EXECUTE format('DROP POLICY IF EXISTS anon_delete ON public.%I;', table_name);

        EXECUTE format('CREATE POLICY anon_select ON public.%I FOR SELECT TO anon USING (true);', table_name);
        EXECUTE format('CREATE POLICY anon_insert ON public.%I FOR INSERT TO anon WITH CHECK (true);', table_name);
        EXECUTE format('CREATE POLICY anon_update ON public.%I FOR UPDATE TO anon USING (true) WITH CHECK (true);', table_name);
        EXECUTE format('CREATE POLICY anon_delete ON public.%I FOR DELETE TO anon USING (true);', table_name);
    END LOOP;
END $$;
