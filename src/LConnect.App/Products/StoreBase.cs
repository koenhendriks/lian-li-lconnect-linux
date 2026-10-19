using System.ComponentModel;
using System.Runtime.CompilerServices;
using LConnect3cs.Utils;

namespace LConnect3cs.Products;

internal class StoreBase<T> : INotifyPropertyChanged where T : Controller
{
	protected readonly ObservableDictionary<string, T> controllers = new ObservableDictionary<string, T>();

	protected int numberOfController;

	public ObservableDictionary<string, T> Controllers => controllers;

	public int NumberOfController
	{
		get
		{
			return numberOfController;
		}
		protected set
		{
			numberOfController = value;
			onPropertyChanged("NumberOfController");
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	protected StoreBase()
	{
		controllers.OnDictionaryCountChanged += (object _, ObservableDictionary<string, T>.DictionaryCountChangedEventArgs _) =>
		{
			updateNumberOfController();
		};
		updateNumberOfController();
		void updateNumberOfController()
		{
			NumberOfController = controllers?.Count ?? 0;
		}
	}

	protected void onPropertyChanged([CallerMemberName] string name = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
