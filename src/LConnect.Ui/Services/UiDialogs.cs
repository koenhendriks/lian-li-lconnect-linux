using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace LConnectUi.Services;

// File/folder pickers for view models. Defaults to the main window's StorageProvider; tests replace the delegates.
public static class UiDialogs
{
	public static Func<string, IReadOnlyList<(string Name, string[] Patterns)>, Task<string>> OpenFile { get; set; } = defaultOpenFile;

	public static Func<string, string, string, Task<string>> SaveFile { get; set; } = defaultSaveFile;

	public static Func<string, Task<string>> PickFolder { get; set; } = defaultPickFolder;

	public static TopLevel TopLevel
	{
		get
		{
			if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow })
			{
				return mainWindow;
			}
			return AppRuntime.MainWindow;
		}
	}

	private static async Task<string> defaultOpenFile(string title, IReadOnlyList<(string Name, string[] Patterns)> filters)
	{
		IStorageProvider storageProvider = TopLevel?.StorageProvider;
		if (storageProvider == null)
		{
			return null;
		}
		IReadOnlyList<IStorageFile> readOnlyList = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
		{
			Title = title,
			AllowMultiple = false,
			FileTypeFilter = filters?.Select(((string Name, string[] Patterns) f) => new FilePickerFileType(f.Name)
			{
				Patterns = f.Patterns
			}).ToList()
		});
		return readOnlyList.FirstOrDefault()?.TryGetLocalPath();
	}

	private static async Task<string> defaultSaveFile(string title, string suggestedName, string extension)
	{
		IStorageProvider storageProvider = TopLevel?.StorageProvider;
		if (storageProvider == null)
		{
			return null;
		}
		return (await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
		{
			Title = title,
			SuggestedFileName = suggestedName,
			DefaultExtension = extension,
			ShowOverwritePrompt = true,
			FileTypeChoices = new FilePickerFileType[1]
			{
				new FilePickerFileType(extension.ToUpperInvariant())
				{
					Patterns = new string[1] { "*." + extension }
				}
			}
		}))?.TryGetLocalPath();
	}

	private static async Task<string> defaultPickFolder(string title)
	{
		IStorageProvider storageProvider = TopLevel?.StorageProvider;
		if (storageProvider == null)
		{
			return null;
		}
		return (await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
		{
			Title = title,
			AllowMultiple = false
		})).FirstOrDefault()?.TryGetLocalPath();
	}
}
