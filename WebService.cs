using System;

namespace CS422
{
	public abstract class WebService 
	{
		
		public abstract void Handler(WebRequest req);

		public abstract string ServiceURI
		{
			get; 
		}
	}
}

