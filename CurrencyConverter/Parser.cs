using System;
using System.Globalization;

namespace CurrencyConverter;

public class Parser
{
    /// <summary>
    ///     Parses a string in the format "number source to target", where "number" and "to target" parts are optional.
    ///     Space between number and source is also optional.
    /// </summary>
    /// <param name="input">The string to parse</param>
    /// <returns>Parse result, or null if the string cannot be parsed</returns>
    public static ParseResult? Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var i = 0;
        double? value = null;

        // Step 1: Extract the number part if present (including commas for thousand separators)
        var hasDigit = false;
        var hasDecimalPoint = false;
        var numberStart = i;

        while (i < input.Length)
        {
            var c = input[i];
            if (char.IsDigit(c))
            {
                hasDigit = true;
                i++;
            }
            else if (c == '.' && !hasDecimalPoint)
            {
                hasDecimalPoint = true;
                i++;
            }
            else if (c == ',' && hasDigit)
            {
                // Allow commas as thousand separators
                i++;
            }
            else
            {
                break;
            }
        }

        // If we found digits, parse the number
        if (hasDigit)
        {
            var numberStr =
                input.Substring(numberStart, i - numberStart).Replace(",", ""); // Remove commas for parsing
            if (double.TryParse(numberStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedValue))
                value = parsedValue;
            else
                // If we can't parse what looks like a number, the input is invalid
                return null;
        }

        // Step 2: Skip whitespace after the number
        while (i < input.Length && char.IsWhiteSpace(input[i])) i++;

        // Step 3: Extract the source part
        var sourceStart = i;
        while (i < input.Length && char.IsLetterOrDigit(input[i])) i++;

        if (i == sourceStart) // No source part
            return null;

        var source = input.Substring(sourceStart, i - sourceStart);

        // Step 4: Check for "to target" part or just a second word
        string? target = null;

        // Skip whitespace after source
        while (i < input.Length && char.IsWhiteSpace(input[i])) i++;

        // First check for explicit "to" keyword
        if (i + 2 <= input.Length &&
            ((i + 2 == input.Length && input.Substring(i).Equals("to", StringComparison.OrdinalIgnoreCase)) ||
             (i + 2 < input.Length && input.Substring(i, 2).Equals("to", StringComparison.OrdinalIgnoreCase) &&
              char.IsWhiteSpace(input[i + 2]))))
        {
            // If "to" is the last part of the input, we're done
            if (i + 2 == input.Length) return new ParseResult(value, source, null);

            i += 2; // Skip "to"

            // Skip whitespace after "to"
            while (i < input.Length && char.IsWhiteSpace(input[i])) i++;
        }

        // If there's still more content, try to parse it as target
        if (i < input.Length)
        {
            // Extract target part (whether after "to" or just a second word)
            var targetStart = i;
            while (i < input.Length && char.IsLetterOrDigit(input[i])) i++;

            if (i > targetStart) // Has target part
                target = input.Substring(targetStart, i - targetStart);
        }

        // Check if reached the end of string (allow trailing whitespace)
        while (i < input.Length && char.IsWhiteSpace(input[i])) i++;

        // If there are remaining characters, the format doesn't match
        if (i < input.Length)
            return null;

        return new ParseResult(value, source, target);
    }

    // Record with primary constructor, Value is now nullable
    public record ParseResult(double? Value, string Source, string? Target)
    {
        public override string ToString()
        {
            var valueStr = Value.HasValue ? Value.ToString() : "null";
            return Target != null
                ? $"Value: {valueStr}, Source: '{Source}', Target: '{Target}'"
                : $"Value: {valueStr}, Source: '{Source}', Target: null";
        }
    }
}