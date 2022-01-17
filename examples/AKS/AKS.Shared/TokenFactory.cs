using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AKS.Shared.Shared.Interface;

namespace AKS.Shared.Shared
{
	public class TokenFactory : ITokenFactory
	{
		public CancellationToken Get(TimeSpan timeSpan)
		{
			return new CancellationTokenSource(timeSpan).Token;
		}

		public CancellationToken GetDefault()
		{
			return new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token;
		}
	}
}
