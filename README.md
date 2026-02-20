# TgaGateway2 - Training.gov.au Gateway with Supabase Integration

This application fetches data from the [Training.gov.au web services](https://training.gov.au/support/connecting-your-system-traininggovau-apis-1) and saves it to Supabase.

## Data Sources

The application connects to three SOAP web service endpoints from Training.gov.au (sandbox environment):

| Endpoint | Purpose |
|----------|---------|
| [OrganisationService.svc](https://ws.sandbox.training.gov.au/Deewr.Tga.WebServices/OrganisationService.svc) | Organisation data (RTOs, contacts, addresses, etc.) |
| [TrainingComponentService.svc](https://ws.sandbox.training.gov.au/Deewr.Tga.Webservices/TrainingComponentService.svc) | Training components (qualifications, units, recognition managers, etc.) |
| [ClassificationService.svc](https://ws.sandbox.training.gov.au/Deewr.Tga.Webservices/ClassificationService.svc) | Classification schemes and purposes |

Each endpoint generates WCF client code from its WSDL: service interfaces, client classes, and data types. This step has already been done for this project. The three generated files are kept in separate folders to avoid duplication errors (shared types like `ValidationFault` appear in multiple WSDLs):

- **TgaOrganization/TgaOrg.cs** → `OrganisationServiceClient`, `IOrganisationService`
- **TgaTrainingComponent/TgaTraining.cs** → `TrainingComponentServiceClient`, `ITrainingComponentService`
- **TgaClassification/TgaClass.cs** → `ClassificationServiceClient`, `IClassificationService`

## Prerequisites

The application requires these three WCF client files. They have already been generated from the Training.gov.au service WSDLs and are included in the project. If you need to regenerate them, use `svcutil.exe` (from the Windows SDK or Visual Studio Developer Command Prompt):

```powershell
# Organisation Service
svcutil.exe https://ws.sandbox.training.gov.au/Deewr.Tga.WebServices/OrganisationService.svc?wsdl /out:TgaOrganization\TgaOrg.cs /namespace:*,training.gov.au.services

# Training Component Service
svcutil.exe https://ws.sandbox.training.gov.au/Deewr.Tga.Webservices/TrainingComponentService.svc?wsdl /out:TgaTrainingComponent\TgaTraining.cs /namespace:*,training.gov.au.services

# Classification Service
svcutil.exe https://ws.sandbox.training.gov.au/Deewr.Tga.Webservices/ClassificationService.svc?wsdl /out:TgaClassification\TgaClass.cs /namespace:*,training.gov.au.services
```

Run each command from the project root. Merge any generated `output.config` bindings into `App.config` if needed. The sandbox endpoints may require credentials—check [Training.gov.au API documentation](https://training.gov.au/support/connecting-your-system-traininggovau-apis-1) for access details.

**Important:** The three WSDLs share many types (e.g. `ValidationFault`, `Contact`, `SearchResult`). Generating separately creates duplicates. This project has manually removed duplicate type definitions from `TgaClass.cs` and `TgaOrg.cs`—those files keep only types unique to each service; shared types come from `TgaTraining.cs`. If you regenerate, you will need to re-apply these duplicate removals. Alternatively, try generating all three WSDLs in one svcutil call to get a single file with shared types: `svcutil.exe url1 url2 url3 /out:TgaAllServices.cs`

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
   - Connect to all three Training.gov.au services (Organisation, Training Component, Classification)
   - Fetch data from each endpoint
   - Display progress in the console
   - Save data to Supabase automatically

### About ExtensionData

`ExtensionData` is a WCF (Windows Communication Foundation) feature that allows the service to handle extra XML elements from newer versions of the service contract. This provides version tolerance - if the service adds new fields in the future, they'll be stored in `ExtensionData` rather than causing deserialization errors.

In most cases, `ExtensionData` will be empty. It only contains data if:
- The service contract has been updated with new fields
- Your generated client classes are from an older version of the WSDL
- The server sends extra XML elements not in your current contract

The application automatically detects and saves any ExtensionData if present.

## Features

- Fetches data from all three Training.gov.au services (Organisation, Training Component, Classification)
- Upserts data to Supabase (inserts new records or updates existing ones based on primary keys)
- Error handling with graceful fallback
- Console output for debugging

## Notes

- The application uses upsert (merge) functionality, so running it multiple times will update existing records rather than create duplicates
- Records are identified by the `code` field (primary key)
- The `updated_at` timestamp is automatically set on each save
