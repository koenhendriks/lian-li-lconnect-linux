using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LConnect3cs.Utils;

internal abstract class ViewModelBase : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler PropertyChanged;

	protected void onPropertyChanged([CallerMemberName] string name = null)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
public abstract class ViewModelBase<T>
{
	public T Model { get; private set; }

	public ViewModelBase(T model)
	{
		Model = model;
	}
}
