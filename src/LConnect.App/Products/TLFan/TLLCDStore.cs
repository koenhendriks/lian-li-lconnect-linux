using System;

namespace LConnect3cs.Products.TLFan;

internal class TLLCDStore : StoreBase<TLLCDController>
{
	private static readonly Lazy<TLLCDStore> lazy = new Lazy<TLLCDStore>(() => new TLLCDStore());

	public static TLLCDStore Instance => lazy.Value;
}
