using System;

namespace AKS.Shared
{
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ActorAttribute : Attribute
    {
		public ActorAttribute(string kind)
	    {
		    this.Kind = kind;
	    }
	    public string Kind { get; set; }
    }
}
