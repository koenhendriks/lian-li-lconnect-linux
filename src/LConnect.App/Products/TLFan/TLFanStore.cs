using System;

namespace LConnect3cs.Products.TLFan;

internal class TLFanStore : StoreBase<TLFanController>
{
	private static readonly Lazy<TLFanStore> lazy = new Lazy<TLFanStore>(() => new TLFanStore());

	public static TLFanStore Instance => lazy.Value;
}
