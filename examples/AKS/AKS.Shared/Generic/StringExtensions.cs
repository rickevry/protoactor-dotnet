namespace AKS.Shared
{
    public static class StringExtensions
    {
	    public static string ToPascalCase(this string value)
	    {
		    if (string.IsNullOrWhiteSpace(value))
		    {
			    return value;
		    }

		    if (value.Length > 1)
		    {
			    value = char.ToUpperInvariant(value[0]) + value.Substring(1);
			    return value;
		    }
		    
			value = char.ToUpperInvariant(value[0]).ToString();
			return value;
	    }
    }
}
