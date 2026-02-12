# TgaGateway2 - Training Component Service with Supabase Integration

This application connects to the Training.gov.au Training Component Service to retrieve Recognition Managers data and saves it to Supabase.

## Setup Instructions

### 1. Supabase Configuration

1. Create a Supabase project at https://supabase.com
2. Go to your project settings and copy:
   - **Project URL** (e.g., `https://xxxxxxxxxxxxx.supabase.co`)
   - **Anon/Public Key** (found in Settings > API)

### 2. Create Database Tables

Run the SQL scripts in `scripts/` in your Supabase SQL Editor to create the required tables.

### 3. Apply anon-only RLS policies

Run `scripts/supabase/anon_only_rls_policies.sql` after table creation. This script:
- Enables and forces RLS on all project tables
- Grants `SELECT`, `INSERT`, `UPDATE`, `DELETE` to the `anon` role
- Creates `anon`-only policies for read/write/delete
- Blocks `authenticated` role access (no matching policies)

Important: Supabase `service_role` keys can bypass RLS by design. Keep using the anon key in `App.config`.

### 4. Configure Application

Update `App.config` with your Supabase credentials:

```xml
<appSettings>
    <add key="SupabaseUrl" value="YOUR_SUPABASE_URL" />
    <add key="SupabaseKey" value="YOUR_SUPABASE_ANON_KEY" />
</appSettings>
```

Replace:
- `YOUR_SUPABASE_URL` with your Supabase project URL
- `YOUR_SUPABASE_ANON_KEY` with your Supabase anon/public key

### 5. Build and Run

1. Build the project in Visual Studio
2. Run the application
3. The app will:
   - Connect to Training Component Service
   - Fetch Recognition Managers
   - Display them in the console
   - Save them to Supabase automatically

## Database Schema

The `recognition_managers` table has the following structure:

### Main Data Fields
- `code` (TEXT, PRIMARY KEY) - Recognition Manager code
- `description` (TEXT, NOT NULL) - Full description
- `short_name` (TEXT, NOT NULL) - Short name

### Extension Data Fields (WCF Version Tolerance)
- `extension_data_present` (BOOLEAN) - Indicates if ExtensionData contains any data
- `extension_data_element_count` (INTEGER) - Number of extension XML elements found
- `extension_data` (TEXT) - Serialized XML extension elements (if present)

### Timestamps
- `created_at` (TIMESTAMPTZ) - Record creation timestamp
- `updated_at` (TIMESTAMPTZ) - Last update timestamp

### About ExtensionData

`ExtensionData` is a WCF (Windows Communication Foundation) feature that allows the service to handle extra XML elements from newer versions of the service contract. This provides version tolerance - if the service adds new fields in the future, they'll be stored in `ExtensionData` rather than causing deserialization errors.

In most cases, `ExtensionData` will be empty. It only contains data if:
- The service contract has been updated with new fields
- Your generated client classes are from an older version of the WSDL
- The server sends extra XML elements not in your current contract

The application automatically detects and saves any ExtensionData if present.

## Features

- Fetches Recognition Managers from Training Component Service
- Upserts data to Supabase (inserts new records or updates existing ones based on `code`)
- Error handling with graceful fallback
- Console output for debugging

## Troubleshooting

### "Supabase URL and Key must be configured" Error
- Make sure you've updated `App.config` with your Supabase credentials
- Check that the keys are in the `appSettings` section

### "Supabase API error" Messages
- Verify your Supabase URL and key are correct
- Check that the `recognition_managers` table exists in your database
- Ensure Row Level Security (RLS) is configured correctly if enabled

### Connection Issues
- Check your internet connection
- Verify the Supabase project is active
- Check Supabase logs in the dashboard

## Notes

- The application uses upsert (merge) functionality, so running it multiple times will update existing records rather than create duplicates
- Records are identified by the `code` field (primary key)
- The `updated_at` timestamp is automatically set on each save
