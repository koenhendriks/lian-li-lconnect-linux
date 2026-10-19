using System;

namespace LConnect3cs.Products.Ene6K77Fan;

internal class SLInfinityStore : StoreBase<SLInfinityController>
{
	private static readonly Lazy<SLInfinityStore> lazy = new Lazy<SLInfinityStore>(() => new SLInfinityStore());

	public static SLInfinityStore Instance => lazy.Value;
}
