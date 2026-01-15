using System;
using System.Diagnostics;
using System.Threading.Tasks;
using TgaGateway2.Services;
using training.gov.au.services;

namespace TgaGateway2.Handlers.TrainingComponentService
{
    /// <summary>
    /// Handler for AddressStates operations - fetching and saving to database
    /// </summary>
    public static class AddressStateHandler
    {
        /// <summary>
        /// Fetches all address states from TGA service, displays them, and saves to Supabase
        /// </summary>
        public static async Task<AddressStates[]> ProcessAddressStates(
            AddressStateService addressStateService,
            SupabaseService supabaseService)
        {
            Console.WriteLine("=== Getting Address States ===");
            var addressStates = addressStateService.GetAddressStates();

            Console.WriteLine(" Count of Address States:" + addressStates.Length);
            if (addressStates != null && addressStates.Length > 0)
            {
                foreach (var state in addressStates)
                {
                    Console.WriteLine($"Code: {state.Code}");
                    Console.WriteLine($"Abbreviation: {state.Abbreviation}");
                    Console.WriteLine($"Description: {state.Description}");
                    Console.WriteLine();
                }

                Console.WriteLine("=== Saving Address States to Supabase ===");
                try
                {
                    var saveStopwatch = Stopwatch.StartNew();
                    await supabaseService.SaveToSupabase(addressStates, "address_states");
                    saveStopwatch.Stop();

                    var originalColor = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"\n✓ Successfully saved {addressStates.Length} Address States to Supabase!");
                    Console.WriteLine($"Time taken to save: {saveStopwatch.Elapsed}\n");
                    Console.ForegroundColor = originalColor;
                }
                catch (Exception supabaseEx)
                {
                    Console.WriteLine($"ERROR: Failed to save to Supabase: {supabaseEx.Message}");
                    if (supabaseEx.InnerException != null)
                    {
                        Console.WriteLine($"Inner Exception: {supabaseEx.InnerException.Message}");
                    }
                    Console.WriteLine("Continuing with rest of the application...\n");
                }

                return addressStates;
            }

            Console.WriteLine("No address states found.\n");
            return null;
        }
    }
}
