using System;
using System.Data;

namespace SpotlightClean.Logic
{
    public static class Calculator
    {
        public static string? Evaluate(string expression)
        {
            try
            {
                // Using DataTable to evaluate the expression for simplicity and safety.
                var dt = new DataTable();
                var result = dt.Compute(expression, "");
                return result.ToString();
            }
            catch
            {
                return null;
            }
        }
    }
}