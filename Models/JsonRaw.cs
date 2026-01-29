namespace TgaGateway2.Models
{
    /// <summary>
    /// Wrapper for raw JSON values to be written without string escaping.
    /// </summary>
    public sealed class JsonRaw
    {
        public JsonRaw(string value)
        {
            Value = value;
        }

        public string Value { get; }
    }
}
